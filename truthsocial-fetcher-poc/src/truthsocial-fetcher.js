import { mkdir, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { chromium } from 'playwright';
import { collectRawPosts, dedupePosts, normalizeRawPost } from './normalize.js';

const BASE_URL = 'https://truthsocial.com';
const DEFAULT_DEBUG_PATH = 'output/debug-page.html';

export async function fetchLatestPosts(options) {
  const browser = await chromium.launch({
    headless: options.headless,
    args: [
      '--disable-blink-features=AutomationControlled',
      '--disable-dev-shm-usage',
      '--no-sandbox'
    ]
  });

  try {
    const context = await browser.newContext({
      locale: 'en-US',
      timezoneId: 'America/New_York',
      userAgent:
        'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/146.0.0.0 Safari/537.36',
      viewport: { width: 1365, height: 900 },
      extraHTTPHeaders: {
        'Accept-Language': 'en-US,en;q=0.9'
      }
    });

    await context.addInitScript(() => {
      Object.defineProperty(navigator, 'webdriver', {
        get: () => undefined
      });
    });

    const networkPosts = [];
    const page = await context.newPage();
    page.setDefaultTimeout(45000);
    page.on('response', async (response) => {
      const url = response.url();
      if (!url.includes('/api/v1/accounts/') && !url.includes('/api/v1/timelines/')) {
        return;
      }

      const contentType = response.headers()['content-type'] ?? '';
      if (!contentType.includes('json')) {
        return;
      }

      try {
        collectRawPosts(await response.json(), networkPosts);
      } catch {
        // Ignore non-JSON or partial responses.
      }
    });

    const profileUrl = `${BASE_URL}/@${options.username}`;
    await page.goto(profileUrl, { waitUntil: 'domcontentloaded' });
    await page.waitForLoadState('networkidle', { timeout: 45000 }).catch(() => {});

    const posts = networkPosts.map((raw) => normalizeRawPost(raw, options.username)).filter(Boolean);
    posts.push(...(await fetchFromBrowserState(page, options.username, options.maxPosts)));

    if (posts.length === 0) {
      await scrollForNetworkPosts(page);
      posts.push(...networkPosts.map((raw) => normalizeRawPost(raw, options.username)).filter(Boolean));
    }

    const uniquePosts = dedupePosts(posts).slice(0, options.maxPosts);
    if (uniquePosts.length === 0) {
      await writeDebugSnapshot(page);
      throw new Error(
        `No public posts were found for @${options.username}. The page may be blocked or the network payload changed.`
      );
    }

    return uniquePosts;
  } finally {
    await browser.close();
  }
}

export async function writePostsJson(posts, outputPath) {
  await mkdir(path.dirname(outputPath), { recursive: true });
  const json = `${JSON.stringify(posts, null, 2)}\n`;
  await writeFile(outputPath, json, 'utf8');
  return json;
}

async function fetchFromBrowserState(page, author, limit) {
  const rawPosts = await page.evaluate(() => {
    const seen = new Map();

    const visit = (value) => {
      if (!value || typeof value !== 'object') {
        return;
      }

      if (Array.isArray(value)) {
        for (const item of value) {
          visit(item);
        }
        return;
      }

      if (typeof value.id === 'string' && typeof value.created_at === 'string') {
        seen.set(value.id, value);
      }

      for (const child of Object.values(value)) {
        visit(child);
      }
    };

    for (const script of document.querySelectorAll('script[type="application/json"], script#__NEXT_DATA__')) {
      try {
        visit(JSON.parse(script.textContent || ''));
      } catch {
        // Ignore non-state JSON scripts.
      }
    }

    for (const key of Object.keys(window)) {
      if (!key.startsWith('__') && !key.toLowerCase().includes('initial')) {
        continue;
      }

      try {
        visit(window[key]);
      } catch {
        // Some window properties throw on access.
      }
    }

    return [...seen.values()];
  });

  return rawPosts.map((raw) => normalizeRawPost(raw, author)).filter(Boolean).slice(0, limit);
}

async function scrollForNetworkPosts(page) {
  for (let i = 0; i < 5; i += 1) {
    await page.mouse.wheel(0, 1400);
    await page.waitForTimeout(1200);
  }
}

async function writeDebugSnapshot(page) {
  const debugPath = process.env.DEBUG_HTML_PATH ?? DEFAULT_DEBUG_PATH;
  await mkdir(path.dirname(debugPath), { recursive: true });
  await writeFile(debugPath, await page.content(), 'utf8');
}
