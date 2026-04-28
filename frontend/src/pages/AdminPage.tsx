function AdminPage() {
  return (
    <main className="app-shell">
      <section className="page-heading admin-heading">
        <div>
          <p className="eyebrow">Developer Console</p>
          <h1>Admin Control Panel</h1>
          <p className="hero-copy">Infrastructure and worker management terminal</p>
        </div>
        <button type="button" className="button secondary">System reset</button>
      </section>

      <section className="admin-grid" aria-label="Admin controls">
        <div className="admin-actions-grid">
          <AdminCard icon="T1" code="TEST_SUITE_01" title="Run Collector Test" description="Execute diagnostic fetch on primary data streams." action="Run test" />
          <AdminCard icon="AI" code="MODEL_INFERENCE" title="Trigger AI Analysis" description="Force immediate re-processing of latest posts." action="Execute AI" tone="tertiary" />
          <AdminCard icon="DB" code="POSTGRES_MASTER" title="Refresh Data Store" description="Sync local metadata with production replicas." action="Sync DB" />
          <AdminCard icon="RM" code="REDIS_FLUSH" title="Purge Cache" description="Clear all L1/L2 application and session caches." action="Purge all" tone="danger" />
        </div>

        <aside className="admin-side-panels">
          <section className="terminal-panel stat-panel">
            <PanelHeader title="Latest Run Status" />
            <div className="run-status-grid">
              <Metric label="Total processed" value="150" />
              <Metric label="Skipped" value="12" />
              <Metric label="Failed" value="2" tone="danger" />
              <Metric label="Duration" value="45s" />
            </div>
          </section>

          <section className="terminal-panel">
            <PanelHeader title="Environment Info" />
            <dl className="environment-list">
              <div>
                <dt>Analyzer provider</dt>
                <dd>OpenAI (gpt-4o)</dd>
              </div>
              <div>
                <dt>API Base URL</dt>
                <dd>https://api.trumpstockalert.ai/v2</dd>
              </div>
              <div>
                <dt>Database</dt>
                <dd>production-cluster-01 <span>Live</span></dd>
              </div>
            </dl>
          </section>
        </aside>
      </section>

      <section className="console-panel" aria-label="System logs">
        <div className="console-header">
          <div className="window-controls" aria-hidden="true">
            <span />
            <span />
            <span />
          </div>
          <strong>System Logs - Main Process</strong>
          <span>Streaming_live</span>
        </div>
        <div className="console-body" role="log">
          <LogLine time="2023-10-27 14:22:01" level="INFO" message="Initializing environment variables..." />
          <LogLine time="2023-10-27 14:22:03" level="INFO" message="Establishing connection to production-cluster-01..." />
          <LogLine time="2023-10-27 14:22:05" level="SUCCESS" message="Database connected. Latency: 14ms." />
          <LogLine time="2023-10-27 14:22:10" level="INFO" message="Starting collector process (worker_id: x7f2)..." />
          <LogLine time="2023-10-27 14:22:18" level="SUCCESS" message="Fetched 15 new posts." />
          <LogLine time="2023-10-27 14:22:20" level="AI" message="Analyzing post 0x9f3 with provider OpenAI..." />
          <LogLine time="2023-10-27 14:22:28" level="WARN" message="API rate limit threshold at 72% for key: TS_SEC_01" />
          <LogLine time="2023-10-27 14:22:32" level="SUCCESS" message="Run completed in 45.2 seconds." />
          <LogLine time="2023-10-27 14:22:45" level="WAIT" message="Listening for next event loop..." />
        </div>
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
  tone,
}: {
  icon: string;
  code: string;
  title: string;
  description: string;
  action: string;
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
      <button type="button" className="button primary">{action}</button>
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

function LogLine({ time, level, message }: { time: string; level: string; message: string }) {
  return (
    <p className={`log-line ${level.toLowerCase()}`}>
      <time>[{time}]</time>
      <strong>[{level}]</strong>
      <span>{message}</span>
    </p>
  );
}

export default AdminPage;
