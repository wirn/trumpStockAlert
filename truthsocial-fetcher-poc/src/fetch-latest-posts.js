import { readFetchOptions } from './config.js';
import { fetchLatestPosts, writePostsJson } from './truthsocial-fetcher.js';

const options = readFetchOptions();
const posts = await fetchLatestPosts(options);
const json = await writePostsJson(posts, options.outputPath);
process.stdout.write(json);
