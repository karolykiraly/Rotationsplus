import { useMe } from "../useMe";
import { useDashboard } from "./useDashboard";
import { rotationStatusLabel } from "../rotations/rotationStatuses";

/** Format a YYYY-MM-DD wire date for display without a timezone shift (parse the parts directly). */
function formatDate(iso: string): string {
  const [y, m, d] = iso.split("-").map(Number);
  if (!y || !m || !d) return iso;
  return new Date(y, m - 1, d).toLocaleDateString(undefined, { year: "numeric", month: "short", day: "numeric" });
}

export function DashboardPage() {
  const { user } = useMe();
  const dash = useDashboard();

  if (user && !user.isAdmin) {
    return <div className="card state">You need the Admin role to view the dashboard.</div>;
  }

  const data = dash.data;

  const stats = data
    ? [
        { label: "Students", value: data.students },
        { label: "Programs", value: data.programs },
        { label: "Preceptors", value: data.preceptors },
        { label: "Specialties", value: data.specialties },
        { label: "Rotations", value: data.rotations }
      ]
    : [];

  return (
    <>
      <div className="page-head">
        <div>
          <h2>Dashboard</h2>
          <p>An overview of the marketplace and the rotation pipeline.</p>
        </div>
      </div>

      {dash.isLoading && <div className="card state">Loading the dashboard…</div>}
      {dash.isError && <div className="card state">Couldn’t load the dashboard: {(dash.error as Error).message}</div>}

      {data && (
        <>
          <div className="stat-grid">
            {stats.map((s) => (
              <div key={s.label} className="stat-card">
                <div className="stat-value">{s.value}</div>
                <div className="stat-label">{s.label}</div>
              </div>
            ))}
          </div>

          <div className="dash-cols">
            <div className="card dash-panel">
              <h3>Rotations by status</h3>
              {data.rotationsByStatus.length === 0 ? (
                <div className="state">No rotations yet.</div>
              ) : (
                <ul className="status-list">
                  {data.rotationsByStatus.map((s) => (
                    <li key={s.status}>
                      <span className="badge">{rotationStatusLabel(s.status)}</span>
                      <span className="status-count">{s.count}</span>
                    </li>
                  ))}
                </ul>
              )}
            </div>

            <div className="card dash-panel">
              <h3>Upcoming starts</h3>
              {data.upcomingStarts.length === 0 ? (
                <div className="state">No upcoming rotations.</div>
              ) : (
                <table className="data">
                  <thead>
                    <tr><th>Student</th><th>Specialty</th><th>Starts</th><th>Status</th></tr>
                  </thead>
                  <tbody>
                    {data.upcomingStarts.map((u) => (
                      <tr key={u.id}>
                        <td>{u.studentName}</td>
                        <td>{u.specialtyName}</td>
                        <td>{formatDate(u.startDate)}</td>
                        <td><span className="badge">{rotationStatusLabel(u.status)}</span></td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </div>
          </div>
        </>
      )}
    </>
  );
}
