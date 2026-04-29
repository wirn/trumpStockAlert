import assert from 'node:assert/strict';
import { classifySaveResponse, shouldExitWithFailure } from '../src/backend-store.js';
import { collectRawPosts, dedupePosts, normalizeRawPost } from '../src/normalize.js';

const tests = [
  ['normalizes Truth Social HTML content to backend shape', () => {
    const post = normalizeRawPost(
      {
        id: '123',
        created_at: '2026-04-29T08:05:38.645Z',
        url: 'https://truthsocial.com/@realDonaldTrump/123',
        content: '<p>Hello&nbsp;&amp; goodbye</p>'
      },
      'realDonaldTrump',
      new Date('2026-04-29T12:00:00.000Z')
    );

    assert.equal(post.source, 'truthsocial');
    assert.equal(post.author, 'realDonaldTrump');
    assert.equal(post.externalId, '123');
    assert.equal(post.content, 'Hello & goodbye');
    assert.equal(post.createdAt, '2026-04-29T08:05:38.645Z');
    assert.equal(post.collectedAt, '2026-04-29T12:00:00.000Z');
  }],
  ['uses safe fallback when a post has no text content', () => {
    const post = normalizeRawPost(
      {
        id: '124',
        created_at: '2026-04-29T08:05:38.645Z',
        content: '<p></p>'
      },
      'realDonaldTrump',
      new Date('2026-04-29T12:00:00.000Z')
    );

    assert.equal(post.content, '[No text content]');
  }],
  ['collects raw posts from nested payloads and dedupes by external id', () => {
    const rawPosts = [];
    collectRawPosts(
      {
        page: {
          statuses: [
            { id: '1', created_at: '2026-04-29T08:00:00.000Z', content: '<p>one</p>' },
            { id: '1', created_at: '2026-04-29T08:00:00.000Z', content: '<p>one duplicate</p>' },
            { id: '2', created_at: '2026-04-29T09:00:00.000Z', content: '<p>two</p>' }
          ]
        }
      },
      rawPosts
    );

    const posts = dedupePosts(rawPosts.map((raw) => normalizeRawPost(raw, 'realDonaldTrump')));
    assert.equal(posts.length, 2);
    assert.deepEqual(posts.map((post) => post.externalId), ['2', '1']);
  }],
  ['classifies backend save responses', () => {
    assert.equal(classifySaveResponse(201).status, 'saved');
    assert.equal(classifySaveResponse(200).status, 'skipped');
    assert.equal(classifySaveResponse(409).status, 'skipped');
    assert.equal(classifySaveResponse(400).status, 'failed');
  }],
  ['exits non-zero only when every fetched post failed to save', () => {
    assert.equal(
      shouldExitWithFailure({ fetchedPosts: 2, savedPosts: 0, skippedPosts: 0, failedPosts: 2 }),
      true
    );
    assert.equal(
      shouldExitWithFailure({ fetchedPosts: 2, savedPosts: 1, skippedPosts: 0, failedPosts: 1 }),
      false
    );
    assert.equal(
      shouldExitWithFailure({ fetchedPosts: 2, savedPosts: 0, skippedPosts: 2, failedPosts: 0 }),
      false
    );
  }]
];

for (const [name, test] of tests) {
  test();
  console.log(`ok - ${name}`);
}

console.log(`${tests.length} tests passed.`);
