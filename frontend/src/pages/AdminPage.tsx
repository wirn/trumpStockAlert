function AdminPage() {
  return (
    <main className="app-shell">
      <section className="hero admin-hero">
        <div>
          <p className="eyebrow">TrumpStockAlert</p>
          <h1>Admin</h1>
          <p className="hero-copy">
            Manual tools for collector, analysis and system checks will be available here.
          </p>
        </div>
      </section>

      <section className="admin-grid" aria-label="Admin placeholders">
        <AdminCard title="Collector" description="Manual collector controls and diagnostics will live here." />
        <AdminCard title="Analysis" description="Analyzer tools, model settings and re-run actions will live here." />
        <AdminCard title="System status" description="Health checks, API status and database checks will live here." />
      </section>
    </main>
  );
}

function AdminCard({ title, description }: { title: string; description: string }) {
  return (
    <article className="admin-card">
      <h2>{title}</h2>
      <p>{description}</p>
    </article>
  );
}

export default AdminPage;
