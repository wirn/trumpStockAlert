import { readSaveOptions } from './config.js';
import { savePostsToBackend, shouldExitWithFailure } from './backend-store.js';
import { fetchLatestPosts, writePostsJson } from './truthsocial-fetcher.js';

const options = readSaveOptions();
const posts = await fetchLatestPosts(options);
await writePostsJson(posts, options.outputPath);

const summary = await savePostsToBackend(posts, options);
for (const failure of summary.failures) {
  console.error(
    JSON.stringify({
      message: 'Failed to save post.',
      externalId: failure.externalId,
      statusCode: failure.statusCode,
      responseBody: failure.responseBody,
      error: failure.error
    })
  );
}

console.log(
  JSON.stringify(
    {
      fetchedPosts: summary.fetchedPosts,
      savedPosts: summary.savedPosts,
      skippedPosts: summary.skippedPosts,
      failedPosts: summary.failedPosts
    },
    null,
    2
  )
);

if (shouldExitWithFailure(summary)) {
  process.exitCode = 1;
}
