import { useState } from "react";
import { useMe } from "../useMe";
import { useDashboard, useRotationConfirmations } from "./useDashboard";
import { DashboardTodosPanel } from "./DashboardTodosPanel";
import { DashboardRevenuePanel } from "./DashboardRevenuePanel";
import { DashboardReportsPanel } from "./DashboardReportsPanel";
import { DashboardCampaignPanel } from "./DashboardCampaignPanel";
import { Tabs } from "../components/Tabs";
import { programFamilyCount } from "../programs/programTypes";
import type { UpcomingRotation } from "../api";

const WEEKDAYS = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];
const MONTHS = [
  "January", "February", "March", "April", "May", "June",
  "July", "August", "September", "October", "November", "December"
];

const pad2 = (n: number) => String(n).padStart(2, "0");
/** The YYYY-MM-DD wire key for a calendar day, matching the rotation startDate strings. */
const dayKey = (year: number, month: number, day: number) => `${year}-${pad2(month + 1)}-${pad2(day)}`;

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
  const confirmations = useRotationConfirmations();
  const today = new Date();
  const todayKey = dayKey(today.getFullYear(), today.getMonth(), today.getDate());
  const [cal, setCal] = useState({ year: today.getFullYear(), month: today.getMonth() });
  // The day whose rotations the Upcoming-Starts table shows; clicking a calendar day re-selects it
  // (defaults to today, matching the live dashboard's selected-day → day-table behaviour).
  const [selectedDate, setSelectedDate] = useState(todayKey);
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

  // Rotations starting on the selected calendar day — the Upcoming-Starts table's rows.
  const selectedRotations = data.upcomingStarts.filter((u) => u.startDate === selectedDate);
  const toggleDocs = (r: UpcomingRotation) =>
    confirmations.mutate({ id: r.id, documentsApproved: !r.documentsApproved, preceptorConfirmed: r.preceptorConfirmed });
  const togglePreceptor = (r: UpcomingRotation) =>
    confirmations.mutate({ id: r.id, documentsApproved: r.documentsApproved, preceptorConfirmed: !r.preceptorConfirmed });

  // Days in the shown month that have an upcoming rotation start (calendar dots).
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
        <h2 className="dash-title">Upcoming Starts</h2>
        <div className="cal-head">
          <button className="cal-nav" onClick={() => step(-1)} aria-label="Previous month">‹</button>
          <div className="cal-month">{MONTHS[cal.month]} {cal.year}</div>
          <button className="cal-nav" onClick={() => step(1)} aria-label="Next month">›</button>
        </div>
        <div className="cal-grid">
          {WEEKDAYS.map((w) => <div key={w} className="cal-weekday">{w}</div>)}
          {cells.map((d, i) => {
            if (!d) return <div key={i} className="cal-cell" />;
            const key = dayKey(cal.year, cal.month, d);
            const cls = `cal-day${eventDays.has(d) ? " has-event" : ""}${isToday(d) ? " today" : ""}${selectedDate === key ? " selected" : ""}`;
            // Clicking a day selects it → the table below filters to that day's rotations (legacy behaviour).
            return (
              <div key={i} className="cal-cell">
                <button type="button" className={cls} onClick={() => setSelectedDate(key)} aria-pressed={selectedDate === key}>
                  {d}
                </button>
              </div>
            );
          })}
        </div>

        <table className="dash-upcoming">
          <thead>
            <tr>
              <th>Preceptor</th>
              <th>Student</th>
              <th>Documents Approved</th>
              <th>Preceptor Confirmed</th>
              <th>Needs Visa</th>
            </tr>
          </thead>
          <tbody>
            {selectedRotations.length === 0 ? (
              <tr><td colSpan={5} className="muted">No rotation</td></tr>
            ) : (
              selectedRotations.map((u) => (
                <tr key={u.id}>
                  {/* Production links the names to the member-profile pages; styled to match (blue),
                      navigation deferred until those routes exist (Contacts) — same as the Rotations list. */}
                  <td className="rot-name">{u.preceptorName ?? "—"}</td>
                  <td className="rot-name">{u.studentName}</td>
                  <td className="text-center">
                    <input
                      type="checkbox"
                      checked={u.documentsApproved}
                      onChange={() => toggleDocs(u)}
                      disabled={confirmations.isPending}
                      aria-label={`Documents approved for ${u.studentName}`}
                    />
                  </td>
                  <td className="text-center">
                    <input
                      type="checkbox"
                      checked={u.preceptorConfirmed}
                      onChange={() => togglePreceptor(u)}
                      disabled={confirmations.isPending}
                      aria-label={`Preceptor confirmed for ${u.studentName}`}
                    />
                  </td>
                  <td className="text-center">
                    <input type="checkbox" checked={u.needsVisa} readOnly aria-label={`Needs visa for ${u.studentName}`} />
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </section>

      <Tabs labels={["Results", "ToDo's", "Campaign", "Reports", "Revenue"]} active={tab} onChange={setTab} />

      {tab === 1 ? (
        <DashboardTodosPanel />
      ) : tab === 2 ? (
        <DashboardCampaignPanel />
      ) : tab === 3 ? (
        <DashboardReportsPanel />
      ) : tab === 4 ? (
        <DashboardRevenuePanel />
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
                <div className="score-metric-title">TOTAL PROGRAMS</div>
                <div className="score-row">
                  <Circle value={data.programs} />
                  {/* The legacy breakdown shows only these three families; Dental (if any) folds
                      out, so the circle can exceed the sum of the three rows by the Dental count. */}
                  <ul className="score-breakdown">
                    <li>InPerson <Badge value={programFamilyCount(data.programsByType, "InPerson")} /></li>
                    <li>Consultation <Badge value={programFamilyCount(data.programsByType, "Consultation")} /></li>
                    <li>TeleRotation <Badge value={programFamilyCount(data.programsByType, "TeleRotation")} /></li>
                  </ul>
                </div>
              </div>
              <div className="score-metric">
                <div className="score-pill-row"><div className="score-metric-title">TOTAL STUDENTS</div><Pill value={data.students} /></div>
                <div className="score-pill-row"><div className="score-metric-title">TOTAL PRECEPTORS</div><Pill value={data.preceptors} /></div>
              </div>
              <div className="score-metric">
                <div className="score-metric-title">TOTAL ROTATIONS</div>
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
