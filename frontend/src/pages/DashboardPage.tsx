import { useEffect, useMemo, useState } from 'react';
import { getAnalyses, getApiBaseUrl, getTruthPosts, runAnalysis, runCollectorTest } from '../api/client';
import type { AnalysisRunResult, CollectorTestRunResult, PostAnalysis, TruthPost } from '../types/api';

type Filter = 'all' | 'high' | 'not-analyzed' | 'negative' | 'positive' | 'uncertain-neutral';

const filters: Array<{ id: Filter; label: string }> = [
  { id: 'all', label: 'All' },
  { id: 'high', label: 'High impact' },
  { id: 'not-analyzed', label: 'Not analyzed' },
  { id: 'negative', label: 'Negative' },
  { id: 'positive', label: 'Positive' },
  { id: 'uncertain-neutral', label: 'Uncertain / Neutral' },
];

function DashboardPage() {
  const [posts, setPosts] = useState<TruthPost[]>([]);
  const [filter, setFilter] = useState<Filter>('all');
  const [loading, setLoading] = useState(true);
  const [running, setRunning] = useState(false);
  const [collectorRunning, setCollectorRunning] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [runResult, setRunResult] = useState<AnalysisRunResult | null>(null);
  const [collectorResult, setCollectorResult] = useState<CollectorTestRunResult | null>(null);

  async function refreshData() {
    setLoading(true);
    setError(null);

    try {
      const [postData, analysisData] = await Promise.all([getTruthPosts(), getAnalyses()]);
      setPosts(mergeAnalyses(postData, analysisData));
    } catch (refreshError) {
      setError(refreshError instanceof Error ? refreshError.message : 'Failed to load dashboard data.');
    } finally {
      setLoading(false);
    }
  }

  async function handleRunAnalysis() {
    setRunning(true);
    setError(null);

    try {
      const result = await runAnalysis();
      setRunResult(result);
      await refreshData();
    } catch (analysisError) {
      setError(analysisError instanceof Error ? analysisError.message : 'Failed to run analysis.');
    } finally {
      setRunning(false);
    }
  }

  async function handleRunCollectorTest() {
    setCollectorRunning(true);
    setError(null);
    setCollectorResult(null);

    try {
      const result = await runCollectorTest();
      setCollectorResult(result);

      if (result.success) {
        await refreshData();
      }
    } catch (collectorError) {
      setError(collectorError instanceof Error ? collectorError.message : 'Failed to run collector test.');
    } finally {
      setCollectorRunning(false);
    }
  }

  useEffect(() => {
    void refreshData();
  }, []);

  const stats = useMemo(() => getStats(posts), [posts]);
  const visiblePosts = useMemo(() => filterAndSortPosts(posts, filter), [posts, filter]);

  return (
    <main className="app-shell">
      <section className="hero">
        <div>
          <p className="eyebrow">TrumpStockAlert</p>
          <h1>Market Impact Dashboard</h1>
          <p className="hero-copy">
            Saved Truth Social posts paired with local AI analysis, ranked by potential market impact.
          </p>
          <p className="api-note">API: {getApiBaseUrl()}</p>
        </div>
        <div className="actions">
          <button type="button" className="button secondary" onClick={() => void refreshData()} disabled={loading || running || collectorRunning}>
            {loading ? 'Refreshing...' : 'Refresh data'}
          </button>
          <button type="button" className="button secondary" onClick={() => void handleRunCollectorTest()} disabled={loading || running || collectorRunning}>
            {collectorRunning ? 'Running collector...' : 'Run collector test'}
          </button>
          <button type="button" className="button primary" onClick={() => void handleRunAnalysis()} disabled={loading || running || collectorRunning}>
            {running ? 'Running...' : 'Run analysis'}
          </button>
        </div>
      </section>

      {error && <div className="notice error">{error}</div>}
      {runResult && <div className="notice success">{runResult.message}</div>}
      {collectorResult && <CollectorRunOutput result={collectorResult} />}

      <section className="stats-grid" aria-label="Dashboard summary">
        <StatCard label="Total posts" value={stats.totalPosts} />
        <StatCard label="Analyzed posts" value={stats.analyzedPosts} />
        <StatCard label="High impact" value={stats.highImpactPosts} tone="hot" />
        <StatCard label="Latest analyzed" value={stats.latestAnalyzedAt ? formatDate(stats.latestAnalyzedAt) : 'Not yet'} />
      </section>

      <section className="toolbar" aria-label="Filters">
        {filters.map((item) => (
          <button
            type="button"
            key={item.id}
            className={filter === item.id ? 'filter active' : 'filter'}
            onClick={() => setFilter(item.id)}
          >
            {item.label}
          </button>
        ))}
      </section>

      {loading ? (
        <section className="empty-state">Loading posts and analyses...</section>
      ) : visiblePosts.length === 0 ? (
        <section className="empty-state">No posts match this filter yet.</section>
      ) : (
        <section className="post-list">
          {visiblePosts.map((post) => (
            <PostCard key={post.id} post={post} />
          ))}
        </section>
      )}
    </main>
  );
}

function CollectorRunOutput({ result }: { result: CollectorTestRunResult }) {
  return (
    <details className={result.success ? 'collector-output success-output' : 'collector-output error-output'} open>
      <summary>
        Collector test {result.success ? 'succeeded' : 'failed'} · exit code {result.exitCode}
      </summary>
      <dl className="collector-meta">
        <div>
          <dt>Started</dt>
          <dd>{formatDate(result.startedAt)}</dd>
        </div>
        <div>
          <dt>Finished</dt>
          <dd>{formatDate(result.finishedAt)}</dd>
        </div>
      </dl>
      {result.stdout && (
        <>
          <h3>stdout</h3>
          <pre>{result.stdout}</pre>
        </>
      )}
      {result.stderr && (
        <>
          <h3>stderr</h3>
          <pre>{result.stderr}</pre>
        </>
      )}
    </details>
  );
}

function mergeAnalyses(posts: TruthPost[], analyses: PostAnalysis[]): TruthPost[] {
  const analysesByPostId = new Map(analyses.map((analysis) => [analysis.postId, analysis]));
  return posts.map((post) => ({
    ...post,
    analysis: post.analysis ?? analysesByPostId.get(post.id) ?? null,
  }));
}

function getStats(posts: TruthPost[]) {
  const analyzed = posts.filter((post) => post.analysis);
  const latestAnalyzedAt = analyzed
    .map((post) => post.analysis?.analyzedAt)
    .filter((date): date is string => Boolean(date))
    .sort((a, b) => new Date(b).getTime() - new Date(a).getTime())[0];

  return {
    totalPosts: posts.length,
    analyzedPosts: analyzed.length,
    highImpactPosts: analyzed.filter((post) => (post.analysis?.marketImpactScore ?? 0) >= 70).length,
    latestAnalyzedAt,
  };
}

function filterAndSortPosts(posts: TruthPost[], filter: Filter): TruthPost[] {
  return posts
    .filter((post) => {
      const analysis = post.analysis;
      const direction = analysis?.direction.toLowerCase();

      if (filter === 'high') return (analysis?.marketImpactScore ?? 0) >= 70;
      if (filter === 'not-analyzed') return !analysis;
      if (filter === 'negative') return direction === 'negative';
      if (filter === 'positive') return direction === 'positive';
      if (filter === 'uncertain-neutral') return direction === 'uncertain' || direction === 'neutral';
      return true;
    })
    .sort((left, right) => {
      const scoreDelta = (right.analysis?.marketImpactScore ?? -1) - (left.analysis?.marketImpactScore ?? -1);
      if (scoreDelta !== 0) return scoreDelta;
      return new Date(right.createdAt).getTime() - new Date(left.createdAt).getTime();
    });
}

function StatCard({ label, value, tone }: { label: string; value: string | number; tone?: 'hot' }) {
  return (
    <article className={tone === 'hot' ? 'stat-card hot' : 'stat-card'}>
      <span>{label}</span>
      <strong>{value}</strong>
    </article>
  );
}

function PostCard({ post }: { post: TruthPost }) {
  const analysis = post.analysis;
  const affectedAssets = parseAffectedAssets(analysis?.affectedAssetsJson);

  return (
    <article className="post-card">
      <div className="score-column">
        {analysis ? (
          <>
            <span className={analysis.marketImpactScore >= 70 ? 'score high' : 'score'}>
              {analysis.marketImpactScore}
            </span>
            <span className={`direction ${analysis.direction.toLowerCase()}`}>{analysis.direction}</span>
          </>
        ) : (
          <>
            <span className="score muted">--</span>
            <span className="direction pending">Not analyzed</span>
          </>
        )}
      </div>

      <div className="post-body">
        <div className="post-meta">
          <span>{post.source}</span>
          <span>External ID: {post.externalId}</span>
          <span>Posted: {formatDate(post.createdAt)}</span>
        </div>

        <p className="content">{post.content}</p>

        <div className={post.hasImage ? 'media-status has-media' : 'media-status'}>
          <span>{post.hasImage ? 'Contains image' : 'No image'}</span>
          {post.imageUrls[0] && (
            <a href={post.imageUrls[0]} target="_blank" rel="noreferrer">
              Open image URL
            </a>
          )}
        </div>

        {analysis ? (
          <div className="analysis-panel">
            <p className="reasoning">{analysis.reasoning}</p>
            <div className="asset-row">
              {affectedAssets.map((asset) => (
                <span className="asset" key={asset}>{asset}</span>
              ))}
            </div>
            <dl className="detail-grid">
              <div>
                <dt>Confidence</dt>
                <dd>{analysis.confidence ?? 'n/a'}</dd>
              </div>
              <div>
                <dt>Analyzed</dt>
                <dd>{formatDate(analysis.analyzedAt)}</dd>
              </div>
              <div>
                <dt>Analyzer</dt>
                <dd>{analysis.analyzerVersion}</dd>
              </div>
            </dl>
          </div>
        ) : (
          <div className="analysis-panel muted-panel">Run analysis to score this post.</div>
        )}

        <div className="post-timestamps">
          <span>Collected: {formatDate(post.collectedAt)}</span>
          <span>Saved: {formatDate(post.savedAtUtc)}</span>
        </div>
      </div>
    </article>
  );
}

function parseAffectedAssets(raw?: string | null): string[] {
  if (!raw) return ['US equities'];

  try {
    const parsed = JSON.parse(raw) as unknown;
    if (Array.isArray(parsed)) {
      return parsed.filter((asset): asset is string => typeof asset === 'string' && asset.trim().length > 0);
    }
  } catch {
    return [raw];
  }

  return ['US equities'];
}

function formatDate(value: string): string {
  return new Intl.DateTimeFormat(undefined, {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(new Date(value));
}

export default DashboardPage;
