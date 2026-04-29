const SOURCE = 'truthsocial';
const BASE_URL = 'https://truthsocial.com';

export function normalizeRawPost(raw, author, collectedAt = new Date()) {
  const externalId = stringOrNull(raw?.id);
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
    cleanText(raw.quote?.text) ||
    cleanText(raw.quote?.title) ||
    cleanHtml(raw.reblog?.content) ||
    cleanText(raw.reblog?.text) ||
    cleanText(raw.reblog?.title) ||
    '[No text content]';

  const url = stringOrNull(raw.url) || `${BASE_URL}/@${author}/posts/${externalId}`;

  return {
    source: SOURCE,
    author,
    externalId,
    url,
    content,
    createdAt,
    collectedAt: collectedAt.toISOString(),
    raw
  };
}

export function dedupePosts(posts) {
  const byId = new Map();
  for (const post of posts) {
    if (!byId.has(post.externalId)) {
      byId.set(post.externalId, post);
    }
  }

  return [...byId.values()].sort((left, right) => right.createdAt.localeCompare(left.createdAt));
}

export function collectRawPosts(value, posts) {
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
