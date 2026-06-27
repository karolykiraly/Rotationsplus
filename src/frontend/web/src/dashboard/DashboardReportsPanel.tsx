import { useQuery } from "@tanstack/react-query";
import { getDashboardReports, type DashboardReports } from "../api";

const MONTHS = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

/** One headline figure. */
function Stat({ label, value }: { label: string; value: string }) {
  return (
    <div className="rev-stat">
      <div className="rev-stat-label">{label}</div>
      <div className="rev-stat-value">{value}</div>
    </div>
  );
}

/** The admin dashboard "Reports" tab (GET /api/dashboard/reports): the booking-conversion funnel, a
 *  six-month student + preceptor registration trend, and the busiest specialties by rotation volume.
 *  Read-only operational analytics. */
export function DashboardReportsPanel() {
  const rep = useQuery<DashboardReports>({ queryKey: ["dashboard-reports"], queryFn: getDashboardReports });

  if (rep.isLoading) return <section className="dash-card state">Loading reports…</section>;
  if (rep.isError) {
    return <section className="dash-card state">Couldn’t load reports: {(rep.error as Error).message}</section>;
  }
  const data = rep.data;
  if (!data) return null;

  const conversion = data.totalStudents > 0
    ? Math.round((data.studentsWithBooking / data.totalStudents) * 100)
    : 0;
  const regMax = Math.max(1, ...data.registrations.map((m) => m.students + m.preceptors));
  const specMax = Math.max(1, ...data.topSpecialties.map((s) => s.rotationCount));

  return (
    <section className="dash-card">
      <h2 className="dash-title">Reports</h2>

      <div className="rev-stats">
        <Stat label="Total students" value={String(data.totalStudents)} />
        <Stat label="Students who booked" value={`${data.studentsWithBooking} (${conversion}%)`} />
        <Stat label="Total rotations" value={String(data.totalRotations)} />
      </div>

      <div className="rev-cols">
        <div className="rev-block">
          <h3 className="rev-block-title">Registrations — last 6 months</h3>
          <ul className="rev-trend">
            {data.registrations.map((m) => (
              <li key={`${m.year}-${m.month}`} className="rev-trend-row">
                <span className="rev-trend-label">{MONTHS[m.month - 1]} {m.year}</span>
                <span className="rev-trend-bar-track">
                  <span className="rev-trend-bar" style={{ width: `${((m.students + m.preceptors) / regMax) * 100}%` }} />
                </span>
                <span className="rev-trend-amount">{m.students}s · {m.preceptors}p</span>
              </li>
            ))}
          </ul>
        </div>

        <div className="rev-block">
          <h3 className="rev-block-title">Busiest specialties</h3>
          {data.topSpecialties.length === 0 ? (
            <div className="rev-empty">No rotations yet.</div>
          ) : (
            <ul className="rev-trend">
              {data.topSpecialties.map((s) => (
                <li key={s.specialtyName} className="rev-trend-row">
                  <span className="rev-trend-label rep-spec-label">{s.specialtyName}</span>
                  <span className="rev-trend-bar-track">
                    <span className="rev-trend-bar" style={{ width: `${(s.rotationCount / specMax) * 100}%` }} />
                  </span>
                  <span className="rev-trend-amount">{s.rotationCount}</span>
                </li>
              ))}
            </ul>
          )}
        </div>
      </div>
    </section>
  );
}
