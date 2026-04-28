import { type CSSProperties, useEffect, useMemo, useState } from 'react';
import { getAnalyses, getTruthPosts, runAnalysis, runCollectorTest } from '../api/client';
import type { AnalysisRunResult, CollectorRunTestResult, PostAnalysis, TruthPost } from '../types/api';

type Filter = 'all' | 'high' | 'not-analyzed' | 'negative' | 'positive' | 'uncertain-neutral';

const filters: Array<{ id: Filter; label: string }> = [
  { id: 'all', label: 'All' },
  { id: 'high', label: 'High impact' },
  { id: 'not-analyzed', label: 'Not analyzed' },
  { id: 'positive', label: 'Positive' },
  { id: 'negative', label: 'Negative' },
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
  const [collectorResult, setCollectorResult] = useState<CollectorRunTestResult | null>(null);

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
      <section className="page-heading">
        <div>
          <p className="eyebrow">Analytics / Dashboard</p>
          <h1>Market Impact Dashboard</h1>
          <p className="hero-copy">Saved Truth Social posts paired with local AI analysis, ranked by potential market impact.</p>
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
        <StatCard label="Total collected" value={stats.totalPosts} meta="Stored posts" />
        <StatCard label="Analyzed posts" value={stats.analyzedPosts} meta={`${stats.analysisCompletion}% complete`} />
        <StatCard label="High impact" value={stats.highImpactPosts} meta="Score >= 70" tone="hot" />
        <StatCard label="Latest analysis" value={stats.latestAnalyzedAt ? formatRelativeTime(stats.latestAnalyzedAt) : 'Not yet'} meta="API status: active" />
        <StatCard label="Avg impact score" value={stats.averageImpactScore} meta="Analyzed posts" progress={stats.averageImpactScore} />
      </section>

      <section className="toolbar" aria-label="Dashboard controls">
        <div className="filter-group" aria-label="Filters">
          {filters.map((item) => (
            <button
              type="button"
              key={item.id}
              className={filter === item.id ? `filter active ${item.id}` : `filter ${item.id}`}
              onClick={() => setFilter(item.id)}
            >
              {item.label}
            </button>
          ))}
        </div>
        <div className="sort-label">
          <span>Sort by:</span>
          <strong>Highest score</strong>
        </div>
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

function CollectorRunOutput({ result }: { result: CollectorRunTestResult }) {
  return (
    <details className={result.success ? 'collector-output success-output' : 'collector-output error-output'} open>
      <summary>
        Collector test {result.success ? 'succeeded' : 'failed'} - exit code {result.exitCode}
      </summary>
      <dl className="collector-meta">
        <div>
          <dt>Finished</dt>
          <dd>{formatDate(result.timestamp)}</dd>
        </div>
        <div>
          <dt>Fetched</dt>
          <dd>{result.fetchedPosts ?? 'n/a'}</dd>
        </div>
        <div>
          <dt>Saved</dt>
          <dd>{result.savedPosts ?? 'n/a'}</dd>
        </div>
      </dl>
      <p>{result.message}</p>
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

  const averageImpactScore = analyzed.length
    ? Math.round(analyzed.reduce((sum, post) => sum + (post.analysis?.marketImpactScore ?? 0), 0) / analyzed.length)
    : 0;

  return {
    totalPosts: posts.length,
    analyzedPosts: analyzed.length,
    highImpactPosts: analyzed.filter((post) => (post.analysis?.marketImpactScore ?? 0) >= 70).length,
    latestAnalyzedAt,
    analysisCompletion: posts.length ? Math.round((analyzed.length / posts.length) * 100) : 0,
    averageImpactScore,
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

function StatCard({
  label,
  value,
  meta,
  tone,
  progress,
}: {
  label: string;
  value: string | number;
  meta: string;
  tone?: 'hot';
  progress?: number;
}) {
  return (
    <article className={tone === 'hot' ? 'stat-card hot' : 'stat-card'}>
      <span>{label}</span>
      <strong>{value}</strong>
      {progress === undefined ? <small>{meta}</small> : <span className="progress-meter" style={{ '--progress': `${progress}%` } as CSSProperties} />}
    </article>
  );
}

function PostCard({ post }: { post: TruthPost }) {
  const analysis = post.analysis;
  const affectedAssets = parseAffectedAssets(analysis?.affectedAssetsJson);
  const direction = analysis?.direction.toLowerCase() ?? 'pending';
  const score = analysis?.marketImpactScore ?? 0;

  return (
    <article className={`post-card ${direction}`}>
      <div className="post-body">
        <div className="post-meta">
          <span className="source-badge" aria-hidden="true">TS</span>
          <span>{post.source}</span>
          <span>Posted: {formatDate(post.createdAt)}</span>
          <span>ID: {post.externalId}</span>
        </div>

        <p className="content">{post.content}</p>
        {analysis ? (
          <div className="analysis-panel">
            <div className="asset-row">
              <span className={`direction ${direction}`}>{analysis.direction} direction</span>
              <span className="asset confidence">Confidence: {analysis.confidence ?? 'n/a'}%</span>
              {affectedAssets.map((asset) => (
                <span className="asset" key={asset}>{asset}</span>
              ))}
            </div>
          </div>
        ) : (
          <div className="analysis-panel muted-panel">
            <span className="direction pending">Not analyzed</span>
            <span className="asset">Run analysis to score this post</span>
          </div>
        )}
      </div>

      <aside className="score-column">
        <span>Impact score</span>
        <div className={`score-ring ${direction}`} style={{ '--score': `${score}%` } as CSSProperties}>
          <strong>{analysis ? score : '--'}</strong>
          <small>/ 100</small>
        </div>
      </aside>

      <footer className="post-footer">
        <p>
          <span>AI reasoning:</span>{' '}
          {analysis ? analysis.reasoning : 'Analysis is pending for this post.'}
        </p>
        <div className="post-footer-actions">
          {analysis && <span className="analyzer-badge">Analyzer: {analysis.analyzerVersion}</span>}
          {post.imageUrls[0] && (
            <a href={post.imageUrls[0]} target="_blank" rel="noreferrer" className="media-link">
              Image
            </a>
          )}
        </div>
      </footer>
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

function formatRelativeTime(value: string): string {
  const deltaSeconds = Math.max(0, Math.round((Date.now() - new Date(value).getTime()) / 1000));
  if (deltaSeconds < 60) return `${deltaSeconds}s ago`;

  const deltaMinutes = Math.round(deltaSeconds / 60);
  if (deltaMinutes < 60) return `${deltaMinutes}m ago`;

  const deltaHours = Math.round(deltaMinutes / 60);
  if (deltaHours < 24) return `${deltaHours}h ago`;

  return formatDate(value);
}

export default DashboardPage;
