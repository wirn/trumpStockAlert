export const DEFAULT_USERNAME = 'realDonaldTrump';
export const DEFAULT_MAX_POSTS = 10;
export const DEFAULT_OUTPUT_PATH = 'output/latest-posts.json';

export function readFetchOptions(env = process.env) {
  return {
    username: cleanUsername(env.TRUTH_SOCIAL_USERNAME ?? DEFAULT_USERNAME),
    maxPosts: parsePositiveInt(env.MAX_POSTS, DEFAULT_MAX_POSTS),
    outputPath: env.OUTPUT_PATH ?? DEFAULT_OUTPUT_PATH,
    headless: (env.HEADLESS ?? 'true').toLowerCase() !== 'false'
  };
}

export function readSaveOptions(env = process.env) {
  const fetchOptions = readFetchOptions(env);
  const backendBaseUrl = requiredUrl(env.BackendBaseUrl, 'BackendBaseUrl');
  const apiKey = requiredText(env.Collector__ApiKey, 'Collector__ApiKey');

  return {
    ...fetchOptions,
    backendBaseUrl,
    apiKey
  };
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

function requiredText(value, name) {
  if (typeof value !== 'string' || !value.trim()) {
    throw new Error(`${name} is required.`);
  }

  return value.trim();
}

function requiredUrl(value, name) {
  const text = requiredText(value, name).replace(/\/+$/, '');
  try {
    return new URL(text).toString().replace(/\/+$/, '');
  } catch {
    throw new Error(`${name} must be a valid URL.`);
  }
}
