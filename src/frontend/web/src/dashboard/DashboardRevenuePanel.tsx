import { useQuery } from "@tanstack/react-query";
import { getDashboardRevenue, type DashboardRevenue } from "../api";
import { programTypeLabel } from "../programs/programTypes";

const MONTHS = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

/** Format a decimal amount as currency. The amount is already to the cent from the server. */
function money(amount: number, currency: string): string {
  try {
    return new Intl.NumberFormat(undefined, { style: "currency", currency }).format(amount);
  } catch {
    return `${currency} ${amount.toFixed(2)}`; // unknown currency code → plain fallback
  }
}

/** One headline figure. */
function Stat({ label, value, muted }: { label: string; value: string; muted?: boolean }) {
  return (
    <div className="rev-stat">
      <div className="rev-stat-label">{label}</div>
      <div className={`rev-stat-value${muted ? " muted" : ""}`}>{value}</div>
    </div>
  );
}

/** The admin dashboard "Revenue" tab (GET /api/dashboard/revenue): headline figures (collected net of
 *  refunds, this month, outstanding receivable, refunded), the per-delivery-type breakdown, and a
 *  six-month collected trend. All figures are platform revenue (deposits), not preceptor honorarium. */
export function DashboardRevenuePanel() {
  const rev = useQuery<DashboardRevenue>({ queryKey: ["dashboard-revenue"], queryFn: getDashboardRevenue });

  if (rev.isLoading) return <section className="dash-card state">Loading revenue…</section>;
  if (rev.isError) {
    return <section className="dash-card state">Couldn’t load revenue: {(rev.error as Error).message}</section>;
  }
  const data = rev.data;
  if (!data) return null;

  const fmt = (n: number) => money(n, data.currency);
  const trendMax = Math.max(1, ...data.monthlyTrend.map((m) => m.amount));
  const byType = [...data.byProgramType].sort((a, b) => b.amount - a.amount);

  return (
    <section className="dash-card">
      <h2 className="dash-title">Revenue</h2>

      <div className="rev-stats">
        <Stat label="Collected (net of refunds)" value={fmt(data.collected)} />
        <Stat label="Collected this month" value={fmt(data.collectedThisMonth)} />
        <Stat label="Outstanding receivable" value={fmt(data.outstandingReceivable)} />
        <Stat label="Refunded" value={fmt(data.refunded)} muted />
      </div>

      <div className="rev-cols">
        <div className="rev-block">
          <h3 className="rev-block-title">By program type</h3>
          {byType.length === 0 ? (
            <div className="rev-empty">No collected revenue yet.</div>
          ) : (
            <ul className="rev-type-list">
              {byType.map((t) => (
                <li key={t.type} className="rev-type-row">
                  <span>{programTypeLabel(t.type)}</span>
                  <span className="rev-type-amount">{fmt(t.amount)}</span>
                </li>
              ))}
            </ul>
          )}
        </div>

        <div className="rev-block">
          <h3 className="rev-block-title">Collected — last 6 months</h3>
          <ul className="rev-trend">
            {data.monthlyTrend.map((m) => (
              <li key={`${m.year}-${m.month}`} className="rev-trend-row">
                <span className="rev-trend-label">{MONTHS[m.month - 1]} {m.year}</span>
                <span className="rev-trend-bar-track">
                  <span className="rev-trend-bar" style={{ width: `${(m.amount / trendMax) * 100}%` }} />
                </span>
                <span className="rev-trend-amount">{fmt(m.amount)}</span>
              </li>
            ))}
          </ul>
        </div>
      </div>
    </section>
  );
}
