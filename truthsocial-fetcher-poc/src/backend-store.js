export async function savePostsToBackend(posts, options) {
  const summary = {
    fetchedPosts: posts.length,
    savedPosts: 0,
    skippedPosts: 0,
    failedPosts: 0,
    failures: []
  };

  for (const post of posts) {
    try {
      const result = await postTruthPost(post, options);
      if (result.status === 'saved') {
        summary.savedPosts += 1;
      } else if (result.status === 'skipped') {
        summary.skippedPosts += 1;
      } else {
        summary.failedPosts += 1;
        summary.failures.push({
          externalId: post.externalId,
          statusCode: result.statusCode,
          responseBody: result.responseBody
        });
      }
    } catch (error) {
      summary.failedPosts += 1;
      summary.failures.push({
        externalId: post.externalId,
        error: error instanceof Error ? error.message : String(error)
      });
    }
  }

  return summary;
}

export async function postTruthPost(post, options) {
  const response = await fetch(`${options.backendBaseUrl}/api/truth-posts`, {
    method: 'POST',
    headers: {
      'content-type': 'application/json',
      'x-api-key': options.apiKey
    },
    body: JSON.stringify(post)
  });

  const responseBody = await response.text();
  return classifySaveResponse(response.status, responseBody);
}

export function classifySaveResponse(statusCode, responseBody = '') {
  if (statusCode === 201) {
    return { status: 'saved', statusCode, responseBody };
  }

  if (statusCode === 200 || statusCode === 409) {
    return { status: 'skipped', statusCode, responseBody };
  }

  return { status: 'failed', statusCode, responseBody };
}

export function shouldExitWithFailure(summary) {
  return summary.fetchedPosts > 0
    && summary.failedPosts === summary.fetchedPosts
    && summary.savedPosts === 0
    && summary.skippedPosts === 0;
}
