import { useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { useMutation, useQuery } from "@tanstack/react-query";
import { bookRotation, getCustomerProgram, getProgramQuote } from "./customerApi";
import { programTypeLabel } from "../programs/programTypes";
import type { ProgramDetail } from "../api";

const MAX_WEEKS = 520;

const money = (n: number) =>
  n.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });

/** Today as a local YYYY-MM-DD (the date-input min), computed without a UTC shift. */
function todayIso(): string {
  const d = new Date();
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}

/** The student-facing booking panel: pick a start date + duration, see the server-computed price, and
 *  book. The booking is created Pending; the deposit is then paid from the rotations tracker. */
function BookingPanel({ program }: { program: ProgramDetail }) {
  const navigate = useNavigate();
  const [weeks, setWeeks] = useState<number>(program.minWeeksPerRotation);
  const [startDate, setStartDate] = useState("");

  const weeksValid = Number.isInteger(weeks) && weeks >= program.minWeeksPerRotation && weeks <= MAX_WEEKS;

  // Server-authoritative price for the chosen duration (never recomputed client-side).
  const quote = useQuery({
    queryKey: ["portal-quote", program.id, weeks],
    queryFn: () => getProgramQuote(program.id, weeks),
    enabled: weeksValid
  });

  const book = useMutation({
    mutationFn: () => bookRotation(program.id, startDate, weeks),
    onSuccess: () => navigate("/portal/rotations")
  });

  const canBook = weeksValid && startDate !== "" && !book.isPending;

  return (
    <div className="card" style={{ padding: 24, marginTop: 16 }}>
      <h3 style={{ marginTop: 0 }}>Book this rotation</h3>

      <div className="booking-fields">
        <label className="field">
          <span>Start date</span>
          <input
            type="date"
            aria-label="Start date"
            min={todayIso()}
            value={startDate}
            onChange={(e) => setStartDate(e.target.value)}
          />
        </label>
        <label className="field">
          <span>Weeks</span>
          <input
            type="number"
            aria-label="Weeks"
            min={program.minWeeksPerRotation}
            max={MAX_WEEKS}
            value={Number.isNaN(weeks) ? "" : weeks}
            onChange={(e) => setWeeks(e.target.value === "" ? NaN : e.target.valueAsNumber)}
          />
        </label>
      </div>

      {!weeksValid && (
        <p className="hint">This program requires {program.minWeeksPerRotation}–{MAX_WEEKS} weeks.</p>
      )}

      {quote.data && (
        <dl className="pay-breakdown" style={{ marginTop: 12 }}>
          <div>
            <dt>Total program cost</dt>
            <dd>${money(quote.data.totalAmount)}</dd>
          </div>
          <div>
            <dt>{quote.data.isOpen ? "Due now (paid in full)" : "Deposit due now"}</dt>
            <dd className="pay-amount">${money(quote.data.depositAmount)}</dd>
          </div>
          {!quote.data.isOpen && (
            <div>
              <dt>Outstanding after deposit</dt>
              <dd>${money(quote.data.outstandingAmount)}</dd>
            </div>
          )}
        </dl>
      )}

      {book.isError && <div className="banner error" role="alert" style={{ marginTop: 12 }}>{(book.error as Error).message}</div>}

      <div style={{ marginTop: 16 }}>
        <button type="button" className="btn btn-primary" disabled={!canBook} onClick={() => book.mutate()}>
          {book.isPending ? "Booking…" : "Book this rotation"}
        </button>
      </div>
    </div>
  );
}

/** Student-facing program detail + booking. No honorarium (the API returns it null for customers). */
export function ProgramDetailPage() {
  const { id } = useParams();
  const program = useQuery({
    queryKey: ["portal-program", id],
    queryFn: () => getCustomerProgram(id!),
    enabled: !!id
  });
  const p = program.data;

  return (
    <>
      <Link to="/portal" className="btn-link">← Back to browse</Link>

      {program.isLoading && <div className="card state" style={{ marginTop: 16 }}>Loading…</div>}
      {program.isError && (
        <div className="card state" style={{ marginTop: 16 }}>Couldn’t load this program: {(program.error as Error).message}</div>
      )}

      {p && (
        <>
          <div className="card" style={{ padding: 24, marginTop: 16 }}>
            <h2 style={{ margin: "0 0 8px" }}>{p.specialtyName}</h2>
            <span className="badge">{programTypeLabel(p.programType)}</span>
            <dl className="dl">
              <dt>Duration</dt>
              <dd>{p.minWeeksPerRotation}+ weeks</dd>
              <dt>Capacity</dt>
              <dd>up to {p.maxStudentsPerRotation} students per rotation</dd>
              <dt>Price</dt>
              <dd>${money(p.retailAmountPerWeek)} / week</dd>
              {p.preceptorName && (<><dt>Preceptor</dt><dd>{p.preceptorName}</dd></>)}
              {p.description && (<><dt>About</dt><dd>{p.description}</dd></>)}
            </dl>
          </div>

          <BookingPanel program={p} />
        </>
      )}
    </>
  );
}
