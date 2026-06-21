import { useState } from "react";
import { useMe } from "../useMe";
import { useDashboard } from "./useDashboard";
import { Tabs } from "../components/Tabs";
import { rotationStatusLabel } from "../rotations/rotationStatuses";
import { programFamilyCount } from "../programs/programTypes";

const WEEKDAYS = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];
const MONTHS = [
  "January", "February", "March", "April", "May", "June",
  "July", "August", "September", "October", "November", "December"
];

/** Format a YYYY-MM-DD wire date for display without a timezone shift (parse the parts directly). */
function formatDate(iso: string): string {
  const [y, m, d] = iso.split("-").map(Number);
  if (!y || !m || !d) return iso;
  return new Date(y, m - 1, d).toLocaleDateString(undefined, { year: "numeric", month: "short", day: "numeric" });
}

/** Monday-first month grid: a flat array of day numbers (or null for padding cells). */
function monthCells(year: number, month: number): (number | null)[] {
  const startDow = (new Date(year, month, 1).getDay() + 6) % 7; // Mon = 0
  const days = new Date(year, month + 1, 0).getDate();
  const cells: (number | null)[] = Array(startDow).fill(null);
  for (let d = 1; d <= days; d++) cells.push(d);
  while (cells.length % 7 !== 0) cells.push(null);
  return cells;
}

/** A blue circle badge (the LiveScore "big number"). */
function Circle({ value }: { value: number | string }) {
  return <span className="score-circle">{value}</span>;
}
/** A small blue circle badge used in the breakdown rows. */
function Badge({ value }: { value: number | string }) {
  return <span className="score-badge">{value}</span>;
}
/** A blue rounded pill holding a single big number. */
function Pill({ value }: { value: number | string }) {
  return <span className="score-pill">{value}</span>;
}

/** Admin dashboard — cloned to the live app: an Upcoming-Starts calendar + table, a tab bar, and the
 *  Today's-LiveScore / LiveScore metric cards. Totals, the per-type program breakdown, the rotation
 *  pipeline, and the day's movement all come from the API (GET /api/dashboard). "Issues Reported" is
 *  0 until the issues subsystem exists. */
export function DashboardPage() {
  const { user } = useMe();
  const dash = useDashboard();
  const today = new Date();
  const [cal, setCal] = useState({ year: today.getFullYear(), month: today.getMonth() });
  const [tab, setTab] = useState(0);

  if (user && !user.isAdmin) {
    return <div className="dash-card state">You need the Admin role to view the dashboard.</div>;
  }
  if (dash.isLoading) return <div className="dash-card state">Loading the dashboard…</div>;
  if (dash.isError) return <div className="dash-card state">Couldn’t load the dashboard: {(dash.error as Error).message}</div>;
  const data = dash.data;
  if (!data) return null;

  const byStatus = (s: string) => data.rotationsByStatus.find((x) => x.status === s)?.count ?? 0;
  const t = data.today;
  // The Rotations-Cycle circle is the sum of its (disjoint) breakdown buckets.
  const cycleTotal = t.rotationsStarting + t.rotationsInProgress + t.rotationsCompleting + t.rotationsCancelled;

  // Days in the shown month that have an upcoming rotation start (pink calendar dots).
  const eventDays = new Set(
    data.upcomingStarts
      .map((u) => u.startDate.split("-").map(Number))
      .filter(([y, m]) => y === cal.year && m - 1 === cal.month)
      .map(([, , d]) => d)
  );
  const cells = monthCells(cal.year, cal.month);
  const isToday = (d: number) =>
    d === today.getDate() && cal.month === today.getMonth() && cal.year === today.getFullYear();
  const step = (delta: number) =>
    setCal((c) => {
      const m = c.month + delta;
      if (m < 0) return { year: c.year - 1, month: 11 };
      if (m > 11) return { year: c.year + 1, month: 0 };
      return { year: c.year, month: m };
    });

  return (
    <div className="dash">
      <section className="dash-card">
        <h2 className="dash-title">Upcoming starts</h2>
        <div className="cal-head">
          <button className="cal-nav" onClick={() => step(-1)} aria-label="Previous month">‹</button>
          <div className="cal-month">{MONTHS[cal.month]} {cal.year}</div>
          <button className="cal-nav" onClick={() => step(1)} aria-label="Next month">›</button>
        </div>
        <div className="cal-grid">
          {WEEKDAYS.map((w) => <div key={w} className="cal-weekday">{w}</div>)}
          {cells.map((d, i) => (
            <div key={i} className="cal-cell">
              {d && (
                <div className={`cal-day${eventDays.has(d) ? " has-event" : ""}${isToday(d) ? " today" : ""}`}>
                  {d}
                </div>
              )}
            </div>
          ))}
        </div>

        <table className="dash-upcoming">
          <thead>
            <tr><th>Student</th><th>Specialty</th><th>Starts</th><th>Status</th></tr>
          </thead>
          <tbody>
            {data.upcomingStarts.length === 0 ? (
              <tr><td colSpan={4} className="muted">No upcoming rotations.</td></tr>
            ) : (
              data.upcomingStarts.map((u) => (
                <tr key={u.id}>
                  <td>{u.studentName}</td>
                  <td>{u.specialtyName}</td>
                  <td>{formatDate(u.startDate)}</td>
                  <td><span className="badge">{rotationStatusLabel(u.status)}</span></td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </section>

      <Tabs labels={["Results", "ToDo's", "Campaign", "Reports", "Revenue"]} active={tab} onChange={setTab} />

      {tab !== 0 ? (
        <section className="dash-card state">This section is coming soon.</section>
      ) : (
        <>
          <section className="dash-card">
            <h2 className="dash-title">Today's LiveScore</h2>
            <div className="score-grid">
              <div className="score-metric">
                <div className="score-metric-title">New Programs Added</div>
                <div className="score-row">
                  <Circle value={t.newPrograms} />
                  <ul className="score-breakdown">
                    <li>InPerson <Badge value={programFamilyCount(t.newProgramsByType, "InPerson")} /></li>
                    <li>Consultation <Badge value={programFamilyCount(t.newProgramsByType, "Consultation")} /></li>
                    <li>TeleRotation <Badge value={programFamilyCount(t.newProgramsByType, "TeleRotation")} /></li>
                  </ul>
                </div>
              </div>
              <div className="score-metric">
                <div className="score-pill-row"><div className="score-metric-title">New Students Registered</div><Pill value={t.newStudents} /></div>
                <div className="score-pill-row"><div className="score-metric-title">New Preceptors Approved</div><Pill value={t.newPreceptors} /></div>
                <div className="score-pill-row"><div className="score-metric-title">Issues Reported</div><Pill value={t.issuesReported} /></div>
              </div>
              <div className="score-metric">
                <div className="score-metric-title">Rotations Cycle</div>
                <div className="score-row">
                  <Circle value={cycleTotal} />
                  <ul className="score-breakdown">
                    <li>Starting <Badge value={t.rotationsStarting} /></li>
                    <li>In Progress <Badge value={t.rotationsInProgress} /></li>
                    <li>Completing <Badge value={t.rotationsCompleting} /></li>
                    <li>Canceled <Badge value={t.rotationsCancelled} /></li>
                  </ul>
                </div>
                <div className="score-metric-title">
                  Waiting for Approval <Badge value={byStatus("Pending")} />
                </div>
              </div>
            </div>
          </section>

          <section className="dash-card">
            <h2 className="dash-title">LiveScore</h2>
            <div className="score-grid">
              <div className="score-metric">
                <div className="score-metric-title">Total Programs</div>
                <div className="score-row">
                  <Circle value={data.programs} />
                  {/* The legacy/Figma breakdown shows only these three families; Dental (if any) folds
                      out, so the circle can exceed the sum of the three rows by the Dental count. */}
                  <ul className="score-breakdown">
                    <li>InPerson <Badge value={programFamilyCount(data.programsByType, "InPerson")} /></li>
                    <li>Consultation <Badge value={programFamilyCount(data.programsByType, "Consultation")} /></li>
                    <li>TeleRotation <Badge value={programFamilyCount(data.programsByType, "TeleRotation")} /></li>
                  </ul>
                </div>
              </div>
              <div className="score-metric">
                <div className="score-pill-row"><div className="score-metric-title">Total Students</div><Pill value={data.students} /></div>
                <div className="score-pill-row"><div className="score-metric-title">Total Preceptors</div><Pill value={data.preceptors} /></div>
                <div className="score-pill-row"><div className="score-metric-title">Total Specialties</div><Pill value={data.specialties} /></div>
              </div>
              <div className="score-metric">
                <div className="score-metric-title">Total Rotations</div>
                <div className="score-row">
                  <Circle value={data.rotations} />
                  <ul className="score-breakdown">
                    <li>In progress <Badge value={byStatus("NotStarted")} /></li>
                    <li>Active <Badge value={byStatus("Active")} /></li>
                    <li>Completed <Badge value={byStatus("Completed")} /></li>
                  </ul>
                </div>
              </div>
            </div>
          </section>
        </>
      )}
    </div>
  );
}
