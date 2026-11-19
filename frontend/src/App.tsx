import { useEffect, useMemo, useState, type FormEvent, type ReactNode } from "react";
import {
  Activity, AlertCircle, ArrowUpRight, Building2, CalendarDays, CheckCircle2,
  ChevronDown, ChevronRight, CircleDollarSign, ClipboardList, CreditCard,
  FileBarChart2, FileText, Eye, Landmark, Layers3, LayoutDashboard, LogOut,
  Menu, RefreshCw, Search, ShieldCheck, Users, Wallet, X
} from "lucide-react";
import {
  Area, AreaChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis
} from "recharts";
import {
  API_BASE_URL, ApiError, apiRequest, clearSession, getPaged, getRole,
  hasSession, saveSession, type Paged
} from "./api";

type Screen = "dashboard" | "properties" | "units" | "residents" | "leases" | "invoices" | "payments" | "ledger" | "audit";
type Row = Record<string, any>;
type Column = { key: string; title: string; render?: (row: Row) => ReactNode; align?: "right" };

const enumLabels: Record<string, Record<string, string>> = {
  propertyType: { "1": "Residential", "2": "Commercial", "3": "Mixed use", "4": "Multi-family", "5": "Single family" },
  property: { "1": "Active", "2": "Inactive", "3": "Archived" },
  unitType: { "1": "Apartment", "2": "Studio", "3": "Office", "4": "Retail", "5": "House", "6": "Villa", "7": "Storage", "8": "Other" },
  unit: { "1": "Active", "2": "Under maintenance", "3": "Inactive" },
  resident: { "1": "Active", "2": "Inactive", "3": "Archived", "4": "Blacklisted" },
  lease: { "1": "Draft", "2": "Pending approval", "3": "Active", "4": "Expired", "5": "Terminated", "6": "Cancelled" },
  invoice: { "1": "Draft", "2": "Issued", "3": "Partially paid", "4": "Paid", "5": "Overdue", "6": "Void" },
  payment: { "1": "Pending", "2": "Processing", "3": "Succeeded", "4": "Failed", "5": "Cancelled", "6": "Partially refunded", "7": "Refunded" },
  provider: { "1": "Manual", "2": "Stripe", "3": "Paymob", "4": "Adyen", "5": "PayPal", "6": "Bank", "7": "Simulated" },
  ledger: { "1": "Draft", "2": "Posted" }
};

function text(value: any, map?: string): string {
  if (value === null || value === undefined || value === "") return "—";
  if (map && enumLabels[map]?.[String(value)]) return enumLabels[map][String(value)];
  if (typeof value === "boolean") return value ? "Yes" : "No";
  const raw = String(value);
  if (/^[0-9a-f]{8}-[0-9a-f-]{27,}$/i.test(raw)) return raw.slice(0, 8).toUpperCase() + "…";
  return raw.replace(/([a-z])([A-Z])/g, "$1 $2");
}

function date(value: any): string {
  if (!value) return "—";
  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime())
    ? String(value)
    : parsed.toLocaleDateString("en-GB", { day: "2-digit", month: "short", year: "numeric" });
}

function money(value: any, currency = "EGP"): string {
  const amount = Number(value || 0);
  try {
    return new Intl.NumberFormat("en-US", { style: "currency", currency: /^[A-Z]{3}$/.test(currency) ? currency : "EGP", maximumFractionDigits: 2 }).format(amount);
  } catch {
    return amount.toLocaleString("en-US", { maximumFractionDigits: 2 }) + " " + currency;
  }
}

function initials(value: string): string {
  return (value || "PM").split(/[\\s@._-]+/).filter(Boolean).slice(0, 2).map((part) => part[0]).join("").toUpperCase();
}

function statusTone(value: any, map?: string): string {
  const key = (map && enumLabels[map]?.[String(value)] || String(value ?? "")).replace(/\\s+/g, "").toLowerCase();
  if (["active", "paid", "succeeded", "posted", "processed", "matched"].includes(key)) return "good";
  if (["pending", "draft", "pendingapproval", "partiallypaid", "partiallyrefunded", "processing", "undermaintenance", "issued"].includes(key)) return "warn";
  if (["failed", "overdue", "blacklisted", "terminated", "expired"].includes(key)) return "bad";
  return "neutral";
}

function Badge({ value, map }: { value: any; map?: string }) {
  return <span className={"status-badge " + statusTone(value, map)}><i />{text(value, map)}</span>;
}

function errorText(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.status === 0) return "Could not reach the API. Check the API URL and CORS configuration.";
    if (error.status === 403) return "Your current role does not have permission to view this resource.";
    if (error.status === 404) return "This read endpoint was not found in the configured API.";
    return error.message;
  }
  return error instanceof Error ? error.message : "An unexpected error occurred.";
}

function App() {
  const [authenticated, setAuthenticated] = useState(hasSession());
  const [screen, setScreen] = useState<Screen>("dashboard");
  const [sidebarOpen, setSidebarOpen] = useState(false);
  const [reloadKey, setReloadKey] = useState(0);

  useEffect(() => {
    const expire = () => setAuthenticated(false);
    window.addEventListener("pm-session-expired", expire);
    return () => window.removeEventListener("pm-session-expired", expire);
  }, []);

  const logout = () => {
    clearSession();
    setAuthenticated(false);
    setScreen("dashboard");
  };

  if (!authenticated) return <Login onSuccess={() => { setAuthenticated(true); setScreen("dashboard"); }} />;

  const go = (next: Screen) => {
    setScreen(next);
    setSidebarOpen(false);
  };

  return (
    <div className="app-shell">
      <aside className={"sidebar " + (sidebarOpen ? "sidebar-open" : "")}>
        <div className="brand-lockup">
          <div className="brand-mark"><Building2 size={21} /></div>
          <div><strong>Property<span>OS</span></strong><small>MANAGEMENT SUITE</small></div>
          <button className="icon-button sidebar-close" onClick={() => setSidebarOpen(false)} aria-label="Close menu"><X size={18} /></button>
        </div>
        <div className="workspace-switch">
          <div className="workspace-icon">PM</div>
          <div className="workspace-copy"><strong>Workspace</strong><span>Property portfolio</span></div>
        </div>
        <nav className="side-nav">
          <p className="nav-caption">WORKSPACE</p>
          <NavButton active={screen === "dashboard"} icon={LayoutDashboard} label="Overview" onClick={() => go("dashboard")} />
          <p className="nav-caption nav-spaced">PORTFOLIO</p>
          <NavButton active={screen === "properties"} icon={Building2} label="Properties" onClick={() => go("properties")} />
          <NavButton active={screen === "units"} icon={Layers3} label="Units" onClick={() => go("units")} />
          <NavButton active={screen === "residents"} icon={Users} label="Residents" onClick={() => go("residents")} />
          <NavButton active={screen === "leases"} icon={ClipboardList} label="Leases" onClick={() => go("leases")} />
          <p className="nav-caption nav-spaced">FINANCE</p>
          <NavButton active={screen === "invoices"} icon={FileText} label="Invoices" onClick={() => go("invoices")} />
          <NavButton active={screen === "payments"} icon={CreditCard} label="Payments" onClick={() => go("payments")} />
          {["Owner", "Admin", "PropertyManager"].includes(getRole()) && <>
            <NavButton active={screen === "ledger"} icon={Landmark} label="General ledger" onClick={() => go("ledger")} />
          </>}
          {["Owner", "Admin"].includes(getRole()) && <NavButton active={screen === "audit"} icon={ShieldCheck} label="Audit trail" onClick={() => go("audit")} />}
        </nav>
        <div className="sidebar-footer">
          <div className="read-only-note"><Eye size={15} /><span>Read-only workspace</span></div>
          <div className="profile-row">
            <div className="avatar">{initials(getRole() || "PM")}</div>
            <div className="profile-copy"><strong>{getRole() || "Member"}</strong><span>Organization account</span></div>
            <button className="icon-button logout-button" onClick={logout} title="Sign out" aria-label="Sign out"><LogOut size={17} /></button>
          </div>
        </div>
      </aside>

      {sidebarOpen && <button className="mobile-scrim" aria-label="Close navigation" onClick={() => setSidebarOpen(false)} />}
      <main className="main-shell">
        <header className="topbar">
          <div className="topbar-left">
            <button className="icon-button mobile-menu" onClick={() => setSidebarOpen(true)} aria-label="Open navigation"><Menu size={20} /></button>
            <div className="breadcrumb"><span>Workspace</span><ChevronRight size={14} /><strong>{screenTitle(screen)}</strong></div>
          </div>
          <div className="topbar-right">
            <div className="live-indicator"><span /> Connected session</div>
            <div className="topbar-date"><CalendarDays size={15} />{new Date().toLocaleDateString("en-US", { month: "short", day: "numeric", year: "numeric" })}</div>
            <button className="icon-button refresh-button" onClick={() => setReloadKey((v) => v + 1)} title="Refresh view" aria-label="Refresh view"><RefreshCw size={17} /></button>
          </div>
        </header>
        <div className="page-content">
          {screen === "dashboard" && <Dashboard key={reloadKey} onNavigate={go} />}
          {screen === "properties" && <DataPage key={screen + reloadKey} title="Properties" subtitle="Your real estate portfolio at a glance." endpoint="/api/v1/properties" kind="properties" />}
          {screen === "units" && <UnitsPage key={screen + reloadKey} />}
          {screen === "residents" && <DataPage key={screen + reloadKey} title="Residents" subtitle="Resident directory and account overview." endpoint="/api/v1/residents" kind="residents" />}
          {screen === "leases" && <DataPage key={screen + reloadKey} title="Lease agreements" subtitle="Lease terms, lifecycle status, and occupancy records." endpoint="/api/v1/leases" kind="leases" />}
          {screen === "invoices" && <DataPage key={screen + reloadKey} title="Invoices" subtitle="Billing history, outstanding balances, and due dates." endpoint="/api/v1/invoices" kind="invoices" />}
          {screen === "payments" && <DataPage key={screen + reloadKey} title="Payments" subtitle="Payment history, allocation, and refund status." endpoint="/api/v1/payments" kind="payments" />}
          {screen === "ledger" && <DataPage key={screen + reloadKey} title="General ledger" subtitle="Journal entries and balanced financial movements." endpoint="/api/v1/ledger/entries" kind="ledger" />}
          {screen === "audit" && <DataPage key={screen + reloadKey} title="Financial audit trail" subtitle="A traceable history of financial system activity." endpoint="/api/v1/ledger/audit-logs" kind="audit" />}
          <footer className="page-footer"><span>PropertyManagement</span><span>Read-only view · Live data from your .NET API</span></footer>
        </div>
      </main>
    </div>
  );
}

function NavButton({ active, icon: Icon, label, onClick }: { active: boolean; icon: any; label: string; onClick: () => void }) {
  return <button className={"nav-item " + (active ? "nav-active" : "")} onClick={onClick}><Icon size={18} /><span>{label}</span>{active && <i />}</button>;
}

function screenTitle(screen: Screen) {
  const titles: Record<Screen, string> = {
    dashboard: "Overview", properties: "Properties", units: "Units", residents: "Residents",
    leases: "Leases", invoices: "Invoices", payments: "Payments", ledger: "General ledger", audit: "Audit trail"
  };
  return titles[screen];
}

function Login({ onSuccess }: { onSuccess: () => void }) {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [organizationId, setOrganizationId] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  async function submit(event: FormEvent) {
    event.preventDefault();
    setBusy(true);
    setError("");
    try {
      const session = await apiRequest<any>("/api/v1/Auth/login", {
        method: "POST",
        body: JSON.stringify({ email, password, organizationId: organizationId.trim() || null })
      }, false);
      if (!session?.accessToken || !session?.refreshToken) throw new Error("The API returned an incomplete login response.");
      saveSession(session);
      onSuccess();
    } catch (err) {
      setError(errorText(err));
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="login-shell">
      <div className="login-visual">
        <div className="login-brand"><div className="brand-mark"><Building2 size={22} /></div><strong>Property<span>OS</span></strong></div>
        <div className="login-message">
          <div className="eyebrow light-eyebrow">PROPERTY MANAGEMENT PLATFORM</div>
          <h1>Your portfolio.<br /><em>In perspective.</em></h1>
          <p>A clear view of your properties, residents, leases, and financial performance — all in one place.</p>
          <div className="login-feature-list">
            <span><CheckCircle2 size={16} /> Portfolio visibility</span>
            <span><CheckCircle2 size={16} /> Financial overview</span>
            <span><CheckCircle2 size={16} /> Organization-aware access</span>
          </div>
        </div>
        <div className="login-visual-foot"><span>PROPERTY MANAGEMENT</span><span>01 / 01</span></div>
      </div>
      <div className="login-panel">
        <div className="login-form-wrap">
          <div className="login-kicker">WELCOME BACK</div>
          <h2>Sign in to your workspace</h2>
          <p className="muted login-intro">Use your PropertyManagement account credentials to continue.</p>
          <form onSubmit={submit} className="login-form">
            <label>Email address<input type="email" autoComplete="username" value={email} onChange={(e) => setEmail(e.target.value)} placeholder="you@company.com" required /></label>
            <label>Password<input type="password" autoComplete="current-password" value={password} onChange={(e) => setPassword(e.target.value)} placeholder="Enter your password" required /></label>
            <label>Organization ID <span className="optional-label">OPTIONAL</span><input value={organizationId} onChange={(e) => setOrganizationId(e.target.value)} placeholder="Required when choosing a specific organization" /></label>
            {error && <div className="form-error"><AlertCircle size={16} />{error}</div>}
            <button className="primary-button login-submit" type="submit" disabled={busy}>{busy ? <><span className="spinner" /> Signing in…</> : <>Sign in <ChevronRight size={17} /></>}</button>
          </form>
          <div className="api-config"><span className="api-config-dot" /> API URL <strong>{API_BASE_URL}</strong></div>
          <div className="login-readonly"><ShieldCheck size={15} /> Read-only access. Records cannot be changed from this interface.</div>
        </div>
      </div>
    </div>
  );
}

const columns: Record<string, Column[]> = {
  properties: [
    { key: "name", title: "Property" }, { key: "address", title: "Address" },
    { key: "type", title: "Type", render: (r) => text(r.type, "propertyType") },
    { key: "status", title: "Status", render: (r) => <Badge value={r.status} map="property" /> },
    { key: "createdAt", title: "Added", render: (r) => date(r.createdAt) }
  ],
  residents: [
    { key: "displayName", title: "Resident", render: (r) => <div className="person-cell"><div className="table-avatar">{initials(r.displayName || r.fullName)}</div><div><strong>{r.displayName || r.fullName || "Resident"}</strong><small>{r.email || "No email provided"}</small></div></div> },
    { key: "phone", title: "Phone" }, { key: "status", title: "Status", render: (r) => <Badge value={r.status} map="resident" /> },
    { key: "emergencyContactsCount", title: "Emergency contacts", align: "right" },
    { key: "createdAtUtc", title: "Added", render: (r) => date(r.createdAtUtc) }
  ],
  leases: [
    { key: "id", title: "Lease reference", render: (r) => <span className="reference">LS-{String(r.id).slice(0, 8).toUpperCase()}</span> },
    { key: "propertyId", title: "Property", render: (r) => text(r.propertyId) }, { key: "unitId", title: "Unit", render: (r) => text(r.unitId) },
    { key: "startDate", title: "Start date", render: (r) => date(r.startDate) }, { key: "endDate", title: "End date", render: (r) => date(r.endDate) },
    { key: "rentAmount", title: "Rent", render: (r) => money(r.rentAmount, r.currency || "EGP"), align: "right" },
    { key: "status", title: "Status", render: (r) => <Badge value={r.status} map="lease" /> }
  ],
  invoices: [
    { key: "invoiceNumber", title: "Invoice", render: (r) => <span className="reference">{r.invoiceNumber || "Draft invoice"}</span> },
    { key: "billingPeriodStart", title: "Billing period", render: (r) => date(r.billingPeriodStart) + " – " + date(r.billingPeriodEnd) },
    { key: "dueDate", title: "Due date", render: (r) => date(r.dueDate) },
    { key: "total", title: "Total", render: (r) => money(r.total, r.currency), align: "right" },
    { key: "amountPaid", title: "Paid", render: (r) => money(r.amountPaid, r.currency), align: "right" },
    { key: "balanceDue", title: "Balance due", render: (r) => <strong className={Number(r.balanceDue) > 0 ? "amount-due" : ""}>{money(r.balanceDue, r.currency)}</strong>, align: "right" },
    { key: "status", title: "Status", render: (r) => <Badge value={r.status} map="invoice" /> }
  ],
  payments: [
    { key: "paymentNumber", title: "Payment", render: (r) => <span className="reference">{r.paymentNumber || "PAY-" + String(r.id).slice(0, 8).toUpperCase()}</span> },
    { key: "createdAtUtc", title: "Date", render: (r) => date(r.createdAtUtc) },
    { key: "provider", title: "Provider", render: (r) => text(r.provider, "provider") },
    { key: "amount", title: "Amount", render: (r) => money(r.amount, r.currency), align: "right" },
    { key: "allocatedAmount", title: "Allocated", render: (r) => money(r.allocatedAmount, r.currency), align: "right" },
    { key: "unappliedAmount", title: "Unapplied", render: (r) => money(r.unappliedAmount, r.currency), align: "right" },
    { key: "status", title: "Status", render: (r) => <Badge value={r.status} map="payment" /> }
  ],
  ledger: [
    { key: "entryNumber", title: "Journal entry", render: (r) => <span className="reference">{r.entryNumber}</span> },
    { key: "entryDateUtc", title: "Entry date", render: (r) => date(r.entryDateUtc) },
    { key: "sourceType", title: "Source" }, { key: "description", title: "Description" },
    { key: "totalDebits", title: "Debits", render: (r) => money(r.totalDebits, r.currency), align: "right" },
    { key: "totalCredits", title: "Credits", render: (r) => money(r.totalCredits, r.currency), align: "right" },
    { key: "status", title: "Status", render: (r) => <Badge value={r.status} map="ledger" /> }
  ],
  audit: [
    { key: "occurredAtUtc", title: "Timestamp", render: (r) => date(r.occurredAtUtc) },
    { key: "action", title: "Action" }, { key: "entityType", title: "Entity" },
    { key: "entityId", title: "Entity reference", render: (r) => text(r.entityId) },
    { key: "actorUserId", title: "Actor", render: (r) => text(r.actorUserId) },
    { key: "correlationId", title: "Correlation", render: (r) => text(r.correlationId) }
  ]
};

function DataPage({ title, subtitle, endpoint, kind }: { title: string; subtitle: string; endpoint: string; kind: string }) {
  const [items, setItems] = useState<Row[]>([]);
  const [page, setPage] = useState(1);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [loadingMore, setLoadingMore] = useState(false);
  const [error, setError] = useState("");
  const [search, setSearch] = useState("");

  useEffect(() => {
    let active = true;
    setLoading(true);
    setError("");
    getPaged<Row>(endpoint, 1, 50)
      .then((result) => {
        if (!active) return;
        setItems(result.items || []);
        setTotal(result.totalCount ?? result.items?.length ?? 0);
        setPage(1);
      })
      .catch((err) => { if (active) setError(errorText(err)); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [endpoint]);

  async function loadMore() {
    setLoadingMore(true);
    try {
      const result = await getPaged<Row>(endpoint, page + 1, 50);
      setItems((current) => [...current, ...(result.items || [])]);
      setPage((current) => current + 1);
      setTotal(result.totalCount ?? total);
    } catch (err) {
      setError(errorText(err));
    } finally {
      setLoadingMore(false);
    }
  }

  const visible = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return items;
    return items.filter((row) => Object.values(row).some((v) => String(v ?? "").toLowerCase().includes(q)));
  }, [items, search]);

  return (
    <section className="content-section">
      <PageHeading title={title} subtitle={subtitle} count={total} countLabel="records" />
      <div className="toolbar">
        <div className="search-box"><Search size={17} /><input value={search} onChange={(e) => setSearch(e.target.value)} placeholder={"Search " + title.toLowerCase() + "…"} /></div>
        <div className="toolbar-meta"><span className="data-dot" /> API data <button className="subtle-button" onClick={() => window.location.reload()}><RefreshCw size={14} /> Refresh</button></div>
      </div>
      {error && <ErrorPanel message={error} />}
      <DataTable columns={columns[kind] || []} items={visible} loading={loading} />
      {!loading && !error && items.length > 0 && <div className="table-bottom"><span>Showing <strong>{visible.length}</strong> of <strong>{total}</strong> records</span>{items.length < total && <button className="outline-button" onClick={loadMore} disabled={loadingMore}>{loadingMore ? "Loading…" : "Load more"} <ChevronDown size={15} /></button>}</div>}
      {!loading && !error && items.length === 0 && <EmptyState title="Nothing to display yet" description="The API returned no records for this organization." />}
    </section>
  );
}

function UnitsPage() {
  const [properties, setProperties] = useState<Row[]>([]);
  const [propertyId, setPropertyId] = useState("");
  const [items, setItems] = useState<Row[]>([]);
  const [loadingProperties, setLoadingProperties] = useState(true);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    getPaged<Row>("/api/v1/properties", 1, 100)
      .then((result) => {
        const list = result.items || [];
        setProperties(list);
        if (list.length) setPropertyId(list[0].id);
      })
      .catch((err) => setError(errorText(err)))
      .finally(() => setLoadingProperties(false));
  }, []);

  useEffect(() => {
    if (!propertyId) { setItems([]); return; }
    let active = true;
    setLoading(true);
    setError("");
    getPaged<Row>("/api/v1/properties/" + propertyId + "/units", 1, 100)
      .then((result) => { if (active) setItems(result.items || []); })
      .catch((err) => { if (active) setError(errorText(err)); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [propertyId]);

  const unitColumns: Column[] = [
    { key: "unitNumber", title: "Unit" }, { key: "type", title: "Type", render: (r) => text(r.type, "unitType") },
    { key: "floorNumber", title: "Floor" }, { key: "bedrooms", title: "Beds" }, { key: "bathrooms", title: "Baths" },
    { key: "squareMeters", title: "Area", render: (r) => r.squareMeters ? r.squareMeters + " m²" : "—" },
    { key: "marketRent", title: "Market rent", render: (r) => money(r.marketRent), align: "right" },
    { key: "status", title: "Status", render: (r) => <Badge value={r.status} map="unit" /> }
  ];

  return (
    <section className="content-section">
      <PageHeading title="Units" subtitle="Explore the units belonging to each property." count={items.length} countLabel="units" />
      <div className="toolbar">
        <div className="property-select-wrap"><Building2 size={17} /><select value={propertyId} onChange={(e) => setPropertyId(e.target.value)} disabled={loadingProperties || properties.length === 0}><option value="">{loadingProperties ? "Loading properties…" : "Select a property"}</option>{properties.map((p) => <option value={p.id} key={p.id}>{p.name}</option>)}</select></div>
        <div className="toolbar-meta"><span className="data-dot" /> API data</div>
      </div>
      {error && <ErrorPanel message={error} />}
      <DataTable columns={unitColumns} items={items} loading={loading || loadingProperties} />
      {!loading && !loadingProperties && items.length === 0 && !error && <EmptyState title="No units found" description="This property has no units returned by the API." />}
    </section>
  );
}

function Dashboard({ onNavigate }: { onNavigate: (screen: Screen) => void }) {
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [data, setData] = useState<{
    properties: Paged<Row>; residents: Paged<Row>; leases: Paged<Row>; invoices: Paged<Row>; payments: Paged<Row>
  } | null>(null);

  useEffect(() => {
    let active = true;
    Promise.all([
      getPaged<Row>("/api/v1/properties", 1, 100),
      getPaged<Row>("/api/v1/residents", 1, 100),
      getPaged<Row>("/api/v1/leases", 1, 100),
      getPaged<Row>("/api/v1/invoices", 1, 100),
      getPaged<Row>("/api/v1/payments", 1, 100)
    ]).then(([properties, residents, leases, invoices, payments]) => {
      if (active) setData({ properties, residents, leases, invoices, payments });
    }).catch((err) => { if (active) setError(errorText(err)); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, []);

  const metrics = useMemo(() => {
    if (!data) return [];
    const outstanding = data.invoices.items.reduce((sum, r) => sum + Number(r.balanceDue || 0), 0);
    const collected = data.payments.items.filter((r) => Number(r.status) === 3 || String(r.status).toLowerCase() === "succeeded").reduce((sum, r) => sum + Number(r.amount || 0), 0);
    return [
      { label: "Properties", value: data.properties.totalCount, helper: "Registered properties", icon: Building2, tone: "blue", target: "properties" as Screen },
      { label: "Residents", value: data.residents.totalCount, helper: "Resident profiles", icon: Users, tone: "violet", target: "residents" as Screen },
      { label: "Active leases", value: data.leases.items.filter((r) => Number(r.status) === 3 || String(r.status).toLowerCase() === "active").length, helper: "Within latest 100 leases", icon: ClipboardList, tone: "green", target: "leases" as Screen },
      { label: "Outstanding balance", value: money(outstanding), helper: "Latest 100 invoices", icon: Wallet, tone: "amber", target: "invoices" as Screen },
      { label: "Collected payments", value: money(collected), helper: "Latest 100 payments", icon: CircleDollarSign, tone: "teal", target: "payments" as Screen }
    ];
  }, [data]);

  const chartData = useMemo(() => {
    const months = Array.from({ length: 6 }, (_, index) => {
      const d = new Date();
      d.setDate(1);
      d.setMonth(d.getMonth() - (5 - index));
      return { key: d.getFullYear() + "-" + d.getMonth(), month: d.toLocaleDateString("en-US", { month: "short" }), invoiced: 0, received: 0 };
    });
    if (!data) return months;
    const map = new Map<string, number>(months.map((m, i) => [m.key, i] as const));
    data.invoices.items.forEach((r) => {
      const stamp = r.issuedAtUtc || r.createdAtUtc;
      if (!stamp) return;
      const d = new Date(stamp);
      const index = map.get(d.getFullYear() + "-" + d.getMonth());
      if (index !== undefined) months[index].invoiced += Number(r.total || 0);
    });
    data.payments.items.forEach((r) => {
      const stamp = r.receivedAtUtc || r.createdAtUtc;
      if (!stamp || !(Number(r.status) === 3 || String(r.status).toLowerCase() === "succeeded")) return;
      const d = new Date(stamp);
      const index = map.get(d.getFullYear() + "-" + d.getMonth());
      if (index !== undefined) months[index].received += Number(r.amount || 0);
    });
    return months;
  }, [data]);

  return (
    <section className="content-section">
      <div className="page-heading dashboard-heading">
        <div><div className="eyebrow">PORTFOLIO OVERVIEW</div><h1>Good to see you<span>.</span></h1><p className="muted">Here’s what’s happening across your property portfolio.</p></div>
        <div className="heading-date"><CalendarDays size={16} />{new Date().toLocaleDateString("en-US", { weekday: "long", month: "long", day: "numeric" })}</div>
      </div>
      {error && <ErrorPanel message={error} />}
      {loading && !data ? <LoadingState /> : data && <>
        <div className="metrics-grid">{metrics.map((m) => <button className="metric-card" key={m.label} onClick={() => onNavigate(m.target)}><div className={"metric-icon " + m.tone}><m.icon size={20} /></div><div className="metric-label">{m.label}</div><div className="metric-value">{m.value}</div><div className="metric-helper">{m.helper}</div></button>)}</div>
        <div className="dashboard-grid">
          <div className="panel chart-panel">
            <div className="panel-heading"><div><h3>Financial activity</h3><p>Invoice value vs successful payments</p></div><span className="panel-period">Last 6 months</span></div>
            <div className="chart-legend"><span><i className="legend-invoiced" /> Invoiced</span><span><i className="legend-paid" /> Received</span></div>
            <div className="chart-wrap"><ResponsiveContainer width="100%" height="100%"><AreaChart data={chartData} margin={{ top: 12, right: 10, left: -15, bottom: 0 }}>
              <defs><linearGradient id="invoiceFill" x1="0" y1="0" x2="0" y2="1"><stop offset="0%" stopColor="#5269d7" stopOpacity={0.18} /><stop offset="100%" stopColor="#5269d7" stopOpacity={0} /></linearGradient><linearGradient id="receivedFill" x1="0" y1="0" x2="0" y2="1"><stop offset="0%" stopColor="#27a98b" stopOpacity={0.16} /><stop offset="100%" stopColor="#27a98b" stopOpacity={0} /></linearGradient></defs>
              <CartesianGrid stroke="#edf0f5" vertical={false} /><XAxis dataKey="month" axisLine={false} tickLine={false} tick={{ fill: "#8b93a7", fontSize: 11 }} /><YAxis axisLine={false} tickLine={false} tick={{ fill: "#8b93a7", fontSize: 11 }} tickFormatter={(v) => v >= 1000 ? (v / 1000).toFixed(0) + "k" : v} />
              <Tooltip formatter={(v: any) => money(v)} contentStyle={{ border: "1px solid #edf0f5", borderRadius: 12 }} />
              <Area type="monotone" dataKey="invoiced" name="Invoiced" stroke="#5269d7" strokeWidth={2.5} fill="url(#invoiceFill)" /><Area type="monotone" dataKey="received" name="Received" stroke="#27a98b" strokeWidth={2.5} fill="url(#receivedFill)" />
            </AreaChart></ResponsiveContainer></div>
            <div className="chart-footnote">Calculated from the latest 100 invoices and payments returned by the API.</div>
          </div>
          <div className="panel quick-panel"><div className="panel-heading"><div><h3>Quick navigation</h3><p>Explore your workspace</p></div></div>
            <QuickLink icon={Building2} title="Properties" detail="Browse real estate portfolio" onClick={() => onNavigate("properties")} />
            <QuickLink icon={Users} title="Residents" detail="View resident directory" onClick={() => onNavigate("residents")} />
            <QuickLink icon={ClipboardList} title="Leases" detail="Review lease agreements" onClick={() => onNavigate("leases")} />
            <QuickLink icon={FileText} title="Invoices" detail="Check billing and balances" onClick={() => onNavigate("invoices")} />
            <QuickLink icon={CreditCard} title="Payments" detail="Explore payment records" onClick={() => onNavigate("payments")} />
          </div>
        </div>
        <div className="panel recent-panel"><div className="panel-heading"><div><h3>Recent payments</h3><p>Latest payment records from your organization</p></div><button className="text-link" onClick={() => onNavigate("payments")}>View all <ArrowUpRight size={15} /></button></div><DataTable columns={columns.payments.slice(0, 5)} items={data.payments.items.slice(0, 5)} loading={false} compact /></div>
      </>}
    </section>
  );
}

function QuickLink({ icon: Icon, title, detail, onClick }: { icon: any; title: string; detail: string; onClick: () => void }) {
  return <button className="quick-link" onClick={onClick}><div className="quick-icon"><Icon size={17} /></div><span><strong>{title}</strong><small>{detail}</small></span><ChevronRight size={16} /></button>;
}

function PageHeading({ title, subtitle, count, countLabel }: { title: string; subtitle: string; count?: number; countLabel?: string }) {
  return <div className="page-heading"><div><div className="eyebrow">PROPERTY MANAGEMENT</div><h1>{title}</h1><p className="muted">{subtitle}</p></div>{count !== undefined && <div className="record-count"><strong>{count.toLocaleString()}</strong><span>{countLabel || "records"}</span></div>}</div>;
}

function DataTable({ columns: cols, items, loading, compact = false }: { columns: Column[]; items: Row[]; loading: boolean; compact?: boolean }) {
  if (loading) return <LoadingState />;
  return <div className={"table-shell " + (compact ? "table-compact" : "")}><table><thead><tr>{cols.map((col) => <th key={col.key} className={col.align === "right" ? "align-right" : ""}>{col.title}</th>)}</tr></thead><tbody>
    {items.map((row, index) => <tr key={row.id || row.entryNumber || row.entityId || index}>{cols.map((col) => <td key={col.key} className={col.align === "right" ? "align-right" : ""}>{col.render ? col.render(row) : text(row[col.key])}</td>)}</tr>)}
    {items.length === 0 && <tr><td colSpan={cols.length}><div className="empty-table">No records found</div></td></tr>}
  </tbody></table></div>;
}

function LoadingState() {
  return <div className="loading-state"><span className="spinner dark-spinner" />Loading records from the API…</div>;
}

function ErrorPanel({ message }: { message: string }) {
  return <div className="error-panel"><AlertCircle size={17} /><div><strong>Unable to load data</strong><span>{message}</span></div></div>;
}

function EmptyState({ title, description }: { title: string; description: string }) {
  return <div className="empty-state"><div className="empty-icon"><FileBarChart2 size={24} /></div><h3>{title}</h3><p>{description}</p></div>;
}

export default App;
