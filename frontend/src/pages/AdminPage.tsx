import { useEffect, useRef, useState } from 'react';
import { getApiBaseUrl, getTruthPosts, runAnalysis, runCollectorTestMode } from '../api/client';
import type { AnalysisRunResult, CollectorRunTestResult, TruthPost } from '../types/api';

type AsyncStatus = 'idle' | 'loading' | 'success' | 'error';

function AdminPage() {
  const [posts, setPosts] = useState<TruthPost[]>([]);
  const [postsStatus, setPostsStatus] = useState<AsyncStatus>('idle');
  const [postsMessage, setPostsMessage] = useState('Posts have not been loaded yet.');
  const [analysisStatus, setAnalysisStatus] = useState<AsyncStatus>('idle');
  const [analysisMessage, setAnalysisMessage] = useState('Analysis has not been run from this console yet.');
  const [analysisResult, setAnalysisResult] = useState<AnalysisRunResult | null>(null);
  const [collectorStatus, setCollectorStatus] = useState<AsyncStatus>('idle');
  const [collectorMessage, setCollectorMessage] = useState('Collector test has not been run from this console yet.');
  const [collectorResult, setCollectorResult] = useState<CollectorRunTestResult | null>(null);
  const statusRef = useRef<HTMLDivElement>(null);

  async function refreshPosts({ announce = true }: { announce?: boolean } = {}) {
    setPostsStatus('loading');
    if (announce) {
      setPostsMessage('Loading Truth Social posts...');
    }

    try {
      const data = await getTruthPosts();
      setPosts(data);
      setPostsStatus('success');
      setPostsMessage(data.length === 0 ? 'No Truth Social posts were returned.' : `Loaded ${data.length} Truth Social posts.`);
    } catch (error) {
      setPostsStatus('error');
      setPostsMessage(error instanceof Error ? error.message : 'Failed to load Truth Social posts.');
    }
  }

  async function handleRunAnalysis() {
    setAnalysisStatus('loading');
    setAnalysisMessage('Running analysis...');
    setAnalysisResult(null);

    try {
      const result = await runAnalysis();
      setAnalysisResult(result);
      setAnalysisStatus('success');
      setAnalysisMessage(result.message || 'Analysis completed successfully.');
      await refreshPosts({ announce: false });
    } catch (error) {
      setAnalysisStatus('error');
      setAnalysisMessage(error instanceof Error ? error.message : 'Failed to run analysis.');
    } finally {
      statusRef.current?.focus();
    }
  }

  async function handleRunCollectorTest() {
    setCollectorStatus('loading');
    setCollectorMessage('Running collector test mode...');
    setCollectorResult(null);

    try {
      const result = await runCollectorTestMode();
      setCollectorResult(result);
      setCollectorStatus(result.success ? 'success' : 'error');
      setCollectorMessage(result.message || (result.success ? 'Collector test completed.' : 'Collector test failed.'));
      if (result.success) {
        await refreshPosts({ announce: false });
      }
    } catch (error) {
      setCollectorStatus('error');
      setCollectorMessage(error instanceof Error ? error.message : 'Failed to run collector test.');
    } finally {
      statusRef.current?.focus();
    }
  }

  useEffect(() => {
    const timeoutId = window.setTimeout(() => {
      void refreshPosts();
    }, 0);

    return () => window.clearTimeout(timeoutId);
  }, []);

  const isPostsLoading = postsStatus === 'loading';
  const isAnalysisRunning = analysisStatus === 'loading';
  const isCollectorRunning = collectorStatus === 'loading';
  const isBusy = isPostsLoading || isAnalysisRunning || isCollectorRunning;
  const analyzedPosts = posts.filter((post) => post.analysis).length;
  const latestAnalyzedAt = posts
    .map((post) => post.analysis?.analyzedAt)
    .filter((value): value is string => Boolean(value))
    .sort((left, right) => new Date(right).getTime() - new Date(left).getTime())[0];

  return (
    <main className="app-shell">
      <section className="page-heading admin-heading">
        <div>
          <p className="eyebrow">Developer Console</p>
          <h1>Admin Control Panel</h1>
          <p className="hero-copy">Manually run backend analysis and inspect the stored Truth Social posts returned by the API.</p>
        </div>
      </section>

      <div className="admin-status-stack" ref={statusRef} tabIndex={-1}>
        <StatusNotice status={collectorStatus} message={collectorMessage} />
        <StatusNotice status={analysisStatus} message={analysisMessage} />
        <StatusNotice status={postsStatus} message={postsMessage} />
      </div>

      <section className="admin-grid" aria-label="Admin controls">
        <div className="admin-actions-grid">
          <AdminCard
            icon="T1"
            code="POST /api/collector/run-test"
            title="Run Collector Test"
            description="Fetch a limited test batch through the collector test-mode flow."
            action={isCollectorRunning ? 'Running...' : 'Run Collector Test'}
            disabled={isBusy}
            onAction={() => void handleRunCollectorTest()}
          />
          <AdminCard
            icon="AI"
            code="POST /api/analysis/run"
            title="Run Analysis Manually"
            description="Trigger analysis for pending posts, then refresh the post list automatically."
            action={isAnalysisRunning ? 'Running...' : 'Run analysis'}
            disabled={isBusy}
            onAction={() => void handleRunAnalysis()}
            tone="tertiary"
          />
          <AdminCard
            icon="TS"
            code="GET /api/truth-posts"
            title="Fetch Truth Social Posts"
            description="Reload saved posts and nested analysis metadata from the backend API."
            action={isPostsLoading ? 'Loading...' : 'Refresh posts'}
            disabled={isBusy}
            onAction={() => void refreshPosts()}
          />
        </div>

        <aside className="admin-side-panels">
          <section className="terminal-panel stat-panel">
            <PanelHeader title="Latest API Status" />
            <div className="run-status-grid">
              <Metric label="Posts loaded" value={posts.length.toString()} />
              <Metric label="Analyzed posts" value={analyzedPosts.toString()} />
              <Metric label="Collector saved" value={formatCount(collectorResult?.savedPosts)} />
              <Metric label="Failed analysis" value={(analysisResult?.failedCount ?? 0).toString()} tone={analysisResult?.failedCount ? 'danger' : undefined} />
              <Metric label="Latest analysis" value={latestAnalyzedAt ? formatDate(latestAnalyzedAt) : 'Not yet'} />
            </div>
          </section>

          <section className="terminal-panel">
            <PanelHeader title="Environment Info" />
            <dl className="environment-list">
              <div>
                <dt>Analysis endpoint</dt>
                <dd>POST /api/analysis/run</dd>
              </div>
              <div>
                <dt>API Base URL</dt>
                <dd>{getApiBaseUrl()}</dd>
              </div>
              <div>
                <dt>Posts endpoint</dt>
                <dd>GET /api/truth-posts <span>Live</span></dd>
              </div>
              <div>
                <dt>Collector test endpoint</dt>
                <dd>POST /api/collector/run-test</dd>
              </div>
            </dl>
          </section>
        </aside>
      </section>

      {collectorResult && (
        <section className="console-panel" aria-labelledby="collector-response-heading">
          <div className="console-header">
            <div className="window-controls" aria-hidden="true">
              <span />
              <span />
              <span />
            </div>
            <strong id="collector-response-heading">Collector Test Result</strong>
            <span>{collectorStatus}</span>
          </div>
          <div className="collector-result">
            <dl className="admin-post-meta">
              <div>
                <dt>Success</dt>
                <dd>{collectorResult.success ? 'Yes' : 'No'}</dd>
              </div>
              <div>
                <dt>Fetched posts</dt>
                <dd>{formatCount(collectorResult.fetchedPosts)}</dd>
              </div>
              <div>
                <dt>Saved posts</dt>
                <dd>{formatCount(collectorResult.savedPosts)}</dd>
              </div>
              <div>
                <dt>Timestamp</dt>
                <dd>{formatDate(collectorResult.timestamp)}</dd>
              </div>
              <div>
                <dt>Exit code</dt>
                <dd>{collectorResult.exitCode}</dd>
              </div>
              <div>
                <dt>Timed out</dt>
                <dd>{collectorResult.timedOut ? 'Yes' : 'No'}</dd>
              </div>
            </dl>
            <p>{collectorResult.message}</p>
            {(collectorResult.stdout || collectorResult.stderr) && (
              <pre className="api-response">{formatCollectorOutput(collectorResult)}</pre>
            )}
          </div>
        </section>
      )}

      {analysisResult && (
        <section className="console-panel" aria-labelledby="analysis-response-heading">
          <div className="console-header">
            <div className="window-controls" aria-hidden="true">
              <span />
              <span />
              <span />
            </div>
            <strong id="analysis-response-heading">Analysis API Response</strong>
            <span>{analysisStatus}</span>
          </div>
          <pre className="api-response">{JSON.stringify(analysisResult, null, 2)}</pre>
        </section>
      )}

      <section className="console-panel" aria-labelledby="truth-posts-heading" aria-busy={isPostsLoading}>
        <div className="console-header">
          <div className="window-controls" aria-hidden="true">
            <span />
            <span />
            <span />
          </div>
          <strong id="truth-posts-heading">Truth Social Posts</strong>
          <span>{postsStatus}</span>
        </div>
        {isPostsLoading ? (
          <div className="empty-state" role="status">Loading posts...</div>
        ) : postsStatus === 'error' ? (
          <div className="empty-state error-output" role="alert">Unable to load posts. {postsMessage}</div>
        ) : posts.length === 0 ? (
          <div className="empty-state">No Truth Social posts were returned by the API.</div>
        ) : (
          <div className="admin-post-list">
            {posts.map((post) => (
              <AdminPostCard key={post.id ?? post.externalId} post={post} />
            ))}
          </div>
        )}
      </section>
    </main>
  );
}

function AdminCard({
  icon,
  code,
  title,
  description,
  action,
  disabled,
  onAction,
  tone,
}: {
  icon: string;
  code: string;
  title: string;
  description: string;
  action: string;
  disabled?: boolean;
  onAction: () => void;
  tone?: 'tertiary' | 'danger';
}) {
  return (
    <article className={tone ? `admin-card ${tone}` : 'admin-card'}>
      <div className="admin-card-top">
        <span aria-hidden="true">{icon}</span>
        <small>{code}</small>
      </div>
      <h2>{title}</h2>
      <p>{description}</p>
      <button type="button" className="button primary" onClick={onAction} disabled={disabled}>{action}</button>
    </article>
  );
}

function PanelHeader({ title }: { title: string }) {
  return (
    <div className="panel-header">
      <h2>{title}</h2>
      <span />
    </div>
  );
}

function Metric({ label, value, tone }: { label: string; value: string; tone?: 'danger' }) {
  return (
    <div className={tone === 'danger' ? 'metric danger' : 'metric'}>
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  );
}

function StatusNotice({ status, message }: { status: AsyncStatus; message: string }) {
  const className = status === 'error'
    ? 'notice error'
    : status === 'success'
      ? 'notice success'
      : 'notice muted-panel';

  return (
    <div className={className} role={status === 'error' ? 'alert' : 'status'} aria-live="polite">
      <strong>{getStatusLabel(status)}:</strong> {message}
    </div>
  );
}

function AdminPostCard({ post }: { post: TruthPost }) {
  const analysis = post.analysis;
  const score = analysis?.marketImpactScore;

  return (
    <article className="admin-post-card">
      <header>
        <div>
          <h3>Post {formatValue(post.id)}</h3>
          <p>{formatValue(post.externalId)}</p>
        </div>
        <span className="direction pending">{analysis ? 'Analyzed' : 'Not analyzed'}</span>
      </header>

      <dl className="admin-post-meta">
        <div>
          <dt>Created</dt>
          <dd>{formatDate(post.createdAt)}</dd>
        </div>
        <div>
          <dt>Analyzed</dt>
          <dd>{analysis?.analyzedAt ? formatDate(analysis.analyzedAt) : 'Not yet'}</dd>
        </div>
        <div>
          <dt>Market impact score</dt>
          <dd>{score === null || score === undefined ? 'Not scored' : `${score} / 100`}</dd>
        </div>
      </dl>

      <p className="admin-post-content">{post.content || getRawText(post.raw) || 'No content returned.'}</p>

      <footer>
        <p>
          <span>Reasoning:</span> {analysis?.reasoning || 'No analysis reasoning available.'}
        </p>
      </footer>
    </article>
  );
}

function getStatusLabel(status: AsyncStatus): string {
  if (status === 'loading') return 'Loading';
  if (status === 'success') return 'Success';
  if (status === 'error') return 'Error';
  return 'Ready';
}

function getRawText(raw: unknown): string | null {
  if (!raw || typeof raw !== 'object') return null;
  const candidate = raw as { content?: unknown; text?: unknown };
  if (typeof candidate.content === 'string') return candidate.content;
  if (typeof candidate.text === 'string') return candidate.text;
  return null;
}

function formatValue(value: unknown): string {
  if (value === null || value === undefined || value === '') return 'n/a';
  return String(value);
}

function formatCount(value?: number | null): string {
  return value === null || value === undefined ? 'n/a' : value.toString();
}

function formatCollectorOutput(result: CollectorRunTestResult): string {
  const sections = [];
  if (result.stdout) sections.push(`stdout\n${result.stdout}`);
  if (result.stderr) sections.push(`stderr\n${result.stderr}`);
  return sections.join('\n\n');
}

function formatDate(value?: string | null): string {
  if (!value) return 'n/a';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;

  return new Intl.DateTimeFormat(undefined, {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(date);
}

export default AdminPage;
