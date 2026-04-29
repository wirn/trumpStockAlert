import { mkdir, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { chromium } from 'playwright';

const SOURCE = 'truthsocial';
const BASE_URL = 'https://truthsocial.com';
const DEFAULT_USERNAME = 'realDonaldTrump';
const DEFAULT_MAX_POSTS = 10;
const DEFAULT_OUTPUT_PATH = 'output/latest-posts.json';
const DEFAULT_DEBUG_PATH = 'output/debug-page.html';

const username = cleanUsername(process.env.TRUTH_SOCIAL_USERNAME ?? DEFAULT_USERNAME);
const maxPosts = parsePositiveInt(process.env.MAX_POSTS, DEFAULT_MAX_POSTS);
const outputPath = process.env.OUTPUT_PATH ?? DEFAULT_OUTPUT_PATH;
const headless = (process.env.HEADLESS ?? 'true').toLowerCase() !== 'false';

const browser = await chromium.launch({
  headless,
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

  const profileUrl = `${BASE_URL}/@${username}`;
  await page.goto(profileUrl, { waitUntil: 'domcontentloaded' });
  await page.waitForLoadState('networkidle', { timeout: 45000 }).catch(() => {});

  const posts = networkPosts.map((raw) => normalizeRawPost(raw, username)).filter(Boolean);
  posts.push(...(await fetchFromBrowserState(page, username, maxPosts)));
  if (posts.length === 0) {
    await scrollForPosts(page);
    posts.push(...networkPosts.map((raw) => normalizeRawPost(raw, username)).filter(Boolean));
    posts.push(...(await fetchFromVisiblePage(page, username, maxPosts)));
  }

  const uniquePosts = dedupePosts(posts).slice(0, maxPosts);
  if (uniquePosts.length === 0) {
    await writeDebugSnapshot(page);
    throw new Error(`No public posts were found for @${username}. The page may be blocked or markup changed.`);
  }

  await mkdir(path.dirname(outputPath), { recursive: true });
  const json = `${JSON.stringify(uniquePosts, null, 2)}\n`;
  await writeFile(outputPath, json, 'utf8');
  process.stdout.write(json);
} finally {
  await browser.close();
}

function collectRawPosts(value, posts) {
  if (!value || typeof value !== 'object') {
    return;
  }

  if (Array.isArray(value)) {
    for (const item of value) {
      collectRawPosts(item, posts);
    }
    return;
  }

  if (typeof value.id === 'string' && typeof value.created_at === 'string') {
    posts.push(value);
  }

  for (const child of Object.values(value)) {
    collectRawPosts(child, posts);
  }
}

async function writeDebugSnapshot(page) {
  const debugPath = process.env.DEBUG_HTML_PATH ?? DEFAULT_DEBUG_PATH;
  await mkdir(path.dirname(debugPath), { recursive: true });
  await writeFile(debugPath, await page.content(), 'utf8');
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
        const content = typeof value.content === 'string' ? value.content : '';
        const url = typeof value.url === 'string' ? value.url : '';
        if (content || url.includes('/posts/')) {
          seen.set(value.id, value);
        }
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

async function scrollForPosts(page) {
  for (let i = 0; i < 5; i += 1) {
    await page.mouse.wheel(0, 1400);
    await page.waitForTimeout(1200);
  }
}

async function fetchFromVisiblePage(page, author, limit) {
  const rawCards = await page.evaluate(() => {
    const anchors = [...document.querySelectorAll('a[href*="/posts/"]')];
    return anchors.map((anchor) => {
      const article = anchor.closest('article') || anchor.closest('[data-testid]') || anchor.parentElement;
      const time = article?.querySelector('time');
      return {
        id: anchor.href.split('/posts/')[1]?.split(/[?#]/)[0],
        url: anchor.href,
        created_at: time?.getAttribute('datetime') || '',
        content: article?.innerText || ''
      };
    });
  });

  return rawCards.map((raw) => normalizeRawPost(raw, author)).filter(Boolean).slice(0, limit);
}

function normalizeRawPost(raw, author) {
  const externalId = stringOrNull(raw.id);
  if (!externalId) {
    return null;
  }

  const createdAt = normalizeDate(raw.created_at);
  if (!createdAt) {
    return null;
  }

  const content =
    cleanHtml(raw.content) ||
    cleanText(raw.text) ||
    cleanText(raw.title) ||
    cleanText(raw.card?.title) ||
    cleanText(raw.card?.description) ||
    cleanHtml(raw.quote?.content) ||
    cleanHtml(raw.reblog?.content) ||
    '[No text content]';

  const url = stringOrNull(raw.url) || `${BASE_URL}/@${author}/posts/${externalId}`;

  return {
    source: SOURCE,
    author,
    externalId,
    url,
    content,
    createdAt,
    collectedAt: new Date().toISOString(),
    raw
  };
}

function dedupePosts(posts) {
  const byId = new Map();
  for (const post of posts) {
    if (!byId.has(post.externalId)) {
      byId.set(post.externalId, post);
    }
  }

  return [...byId.values()].sort((left, right) => right.createdAt.localeCompare(left.createdAt));
}

function cleanUsername(value) {
  const cleaned = String(value).trim().replace(/^@+/, '');
  if (!cleaned) {
    throw new Error('TRUTH_SOCIAL_USERNAME cannot be empty.');
  }

  return cleaned;
}

function parsePositiveInt(value, fallback) {
  const parsed = Number.parseInt(value ?? '', 10);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : fallback;
}

function stringOrNull(value) {
  return typeof value === 'string' && value.trim() ? value.trim() : null;
}

function normalizeDate(value) {
  const text = stringOrNull(value);
  if (!text) {
    return null;
  }

  const date = new Date(text);
  return Number.isNaN(date.getTime()) ? null : date.toISOString();
}

function cleanHtml(value) {
  const text = stringOrNull(value);
  if (!text) {
    return null;
  }

  return cleanText(text.replace(/<[^>]+>/g, ' '));
}

function cleanText(value) {
  const text = stringOrNull(value);
  if (!text) {
    return null;
  }

  const decoded = text
    .replace(/&nbsp;/g, ' ')
    .replace(/&amp;/g, '&')
    .replace(/&lt;/g, '<')
    .replace(/&gt;/g, '>')
    .replace(/&quot;/g, '"')
    .replace(/&#39;/g, "'");

  return decoded.replace(/\s+/g, ' ').trim() || null;
}
