import { mkdir, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { chromium } from 'playwright';
import { collectRawPosts, dedupePosts, normalizeRawPost } from './normalize.js';

const BASE_URL = 'https://truthsocial.com';
const DEBUG_SNAPSHOT_FILE_NAME = 'debug-page.html';
const DEBUG_SCREENSHOT_FILE_NAME = 'debug-page.png';
const DEBUG_DIAGNOSTICS_FILE_NAME = 'diagnostics.json';
const RELEVANT_URL_PATTERN = /truthsocial\.com\/(api|@|users|deck|packs|instance|oauth)|\/api\/v\d+\/|\/api\/v\d+\//i;
const JSON_URL_PATTERN = /\/api\/v\d+\/|\/api\/graphql|\/api\/web|\/api\/pleroma|\/api\/mastodon|\/timeline|\/statuses|\/accounts/i;
const BLOCK_TEXT_PATTERN = /captcha|cloudflare|just a moment|checking your browser|access denied|forbidden|too many requests|temporarily blocked|unusual traffic|verify you are human/i;

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
    const diagnostics = createDiagnostics();
    const page = await context.newPage();
    page.setDefaultTimeout(45000);
    page.on('response', async (response) => {
      const url = response.url();
      if (!isRelevantTruthSocialUrl(url)) {
        return;
      }

      recordResponseDiagnostic(diagnostics, response);

      const contentType = response.headers()['content-type'] ?? '';
      if (!contentType.includes('json') && !isLikelyJsonPayloadUrl(url)) {
        return;
      }

      addUnique(diagnostics.matchedJsonNetworkPaths, pathAndQuery(url), 40);
      try {
        collectRawPosts(await response.json(), networkPosts);
      } catch {
        // Ignore non-JSON or partial responses.
      }
    });

    const profileUrl = `${BASE_URL}/@${options.username}`;
    await loadProfilePage(page, profileUrl);
    let posts = await collectNormalizedPosts(page, networkPosts, options);

    let uniquePosts = dedupePosts(posts).slice(0, options.maxPosts);
    if (uniquePosts.length === 0) {
      diagnostics.retriedReload = true;
      await page.reload({ waitUntil: 'domcontentloaded' });
      await waitForRelevantActivity(page);
      await scrollForNetworkPosts(page);
      posts = await collectNormalizedPosts(page, networkPosts, options);
      uniquePosts = dedupePosts(posts).slice(0, options.maxPosts);
    }

    if (uniquePosts.length === 0) {
      posts.push(...(await fetchFromVisiblePage(page, options.username, options.maxPosts)));
      uniquePosts = dedupePosts(posts).slice(0, options.maxPosts);
    }

    if (uniquePosts.length === 0) {
      await finalizeDiagnostics(page, diagnostics);
      logDiagnostics(diagnostics);
      await writeDebugSnapshot(page, options, diagnostics);
      throw new Error(
        `No public posts were found for @${options.username}. The page may be blocked or the network payload changed.`
      );
    }

    return uniquePosts;
  } finally {
    await browser.close();
  }
}

export function isRelevantTruthSocialUrl(url) {
  return RELEVANT_URL_PATTERN.test(url);
}

export function isLikelyJsonPayloadUrl(url) {
  return JSON_URL_PATTERN.test(url);
}

export function detectBlockIndicators(diagnostics, pageText) {
  const statusCodes = diagnostics.responseStatusCodes.map((entry) => entry.statusCode);
  return {
    has403: statusCodes.includes(403),
    has429: statusCodes.includes(429),
    hasCaptchaOrBlockText: BLOCK_TEXT_PATTERN.test(pageText),
    hasChallengeUrl: diagnostics.finalPageUrl.includes('/challenge')
      || diagnostics.finalPageUrl.includes('captcha')
      || diagnostics.finalPageUrl.includes('cdn-cgi')
  };
}

export async function writePostsJson(posts, outputPath) {
  await mkdir(path.dirname(outputPath), { recursive: true });
  const json = `${JSON.stringify(posts, null, 2)}\n`;
  await writeFile(outputPath, json, 'utf8');
  return json;
}

async function loadProfilePage(page, profileUrl) {
  await page.goto(profileUrl, { waitUntil: 'domcontentloaded' });
  await waitForRelevantActivity(page);
  await scrollForNetworkPosts(page);
}

async function waitForRelevantActivity(page) {
  await Promise.race([
    page.waitForResponse((response) => isRelevantTruthSocialUrl(response.url()), { timeout: 30000 }).catch(() => null),
    page.waitForLoadState('networkidle', { timeout: 45000 }).catch(() => null)
  ]);

  await page.waitForTimeout(2500);
}

async function collectNormalizedPosts(page, networkPosts, options) {
  const posts = networkPosts.map((raw) => normalizeRawPost(raw, options.username)).filter(Boolean);
  posts.push(...(await fetchFromBrowserState(page, options.username, options.maxPosts)));
  return posts;
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

async function fetchFromVisiblePage(page, author, limit) {
  const rawCards = await page.evaluate(() => {
    const anchors = [...document.querySelectorAll('a[href*="/posts/"], a[href*="/@"][href*="/116"]')];
    return anchors.map((anchor) => {
      const article = anchor.closest('article') || anchor.closest('[data-testid]') || anchor.parentElement;
      const time = article?.querySelector('time');
      const href = anchor.href || '';
      return {
        id: href.split('/posts/')[1]?.split(/[?#]/)[0] || href.split('/').pop()?.split(/[?#]/)[0],
        url: href,
        created_at: time?.getAttribute('datetime') || '',
        content: article?.innerText || ''
      };
    });
  });

  return rawCards.map((raw) => normalizeRawPost(raw, author)).filter(Boolean).slice(0, limit);
}

async function scrollForNetworkPosts(page) {
  for (let i = 0; i < 8; i += 1) {
    await page.mouse.wheel(0, 1400);
    await page.waitForTimeout(1500);
  }
}

function createDiagnostics() {
  return {
    finalPageUrl: '',
    pageTitle: '',
    responseStatusCodes: [],
    matchedJsonNetworkPaths: [],
    blockIndicators: {
      has403: false,
      has429: false,
      hasCaptchaOrBlockText: false,
      hasChallengeUrl: false
    },
    retriedReload: false
  };
}

function recordResponseDiagnostic(diagnostics, response) {
  addUniqueObject(
    diagnostics.responseStatusCodes,
    {
      path: pathAndQuery(response.url()),
      statusCode: response.status()
    },
    (entry) => `${entry.statusCode}:${entry.path}`,
    80
  );
}

async function finalizeDiagnostics(page, diagnostics) {
  diagnostics.finalPageUrl = page.url();
  diagnostics.pageTitle = await page.title().catch(() => '');
  const pageText = await page.locator('body').innerText({ timeout: 5000 }).catch(() => '');
  diagnostics.blockIndicators = detectBlockIndicators(diagnostics, `${diagnostics.pageTitle}\n${pageText}`);
}

function logDiagnostics(diagnostics) {
  console.error(JSON.stringify({
    message: 'No Truth Social posts found.',
    finalPageUrl: diagnostics.finalPageUrl,
    pageTitle: diagnostics.pageTitle,
    responseStatusCodes: diagnostics.responseStatusCodes,
    matchedJsonNetworkPaths: diagnostics.matchedJsonNetworkPaths,
    blockIndicators: diagnostics.blockIndicators,
    retriedReload: diagnostics.retriedReload
  }));
}

async function writeDebugSnapshot(page, options, diagnostics) {
  if (!options.writeDebugSnapshot) {
    return;
  }

  await mkdir(options.debugOutputDir, { recursive: true });
  await writeFile(
    path.join(options.debugOutputDir, DEBUG_DIAGNOSTICS_FILE_NAME),
    `${JSON.stringify(diagnostics, null, 2)}\n`,
    'utf8'
  );
  await writeFile(path.join(options.debugOutputDir, DEBUG_SNAPSHOT_FILE_NAME), await page.content(), 'utf8');
  await page.screenshot({ path: path.join(options.debugOutputDir, DEBUG_SCREENSHOT_FILE_NAME), fullPage: true });
}

function pathAndQuery(url) {
  try {
    const parsed = new URL(url);
    return `${parsed.pathname}${parsed.search}`;
  } catch {
    return url;
  }
}

function addUnique(values, value, maxLength) {
  if (!values.includes(value)) {
    values.push(value);
  }

  if (values.length > maxLength) {
    values.splice(0, values.length - maxLength);
  }
}

function addUniqueObject(values, value, keySelector, maxLength) {
  const key = keySelector(value);
  if (!values.some((item) => keySelector(item) === key)) {
    values.push(value);
  }

  if (values.length > maxLength) {
    values.splice(0, values.length - maxLength);
  }
}
