import { useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { useMutation, useQuery } from "@tanstack/react-query";
import { bookRotation, getCustomerProgram, getProgramQuote } from "./customerApi";
import { ProgramImage } from "./ProgramImage";
import { programCode, programTypeLabel } from "../programs/programTypes";
import type { ProgramDetail } from "../api";

const MAX_WEEKS = 520;

const money = (n: number) =>
  n.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });

const isHourly = (p: ProgramDetail) => p.programType === "ConsultationSub";

/** Today as a local YYYY-MM-DD (the date-input min), computed without a UTC shift. */
function todayIso(): string {
  const d = new Date();
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}

/** One label/value row in the details grid (value falls back to an em-dash placeholder). */
function Detail({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="pd-row">
      <div className="pd-label">{label}</div>
      <div className="pd-value">{value ?? "—"}</div>
    </div>
  );
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
    <div className="pd-book">
      <h3 className="pd-book-title">Book this rotation</h3>

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
        <dl className="pay-breakdown">
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

      {book.isError && <div className="banner error" role="alert">{(book.error as Error).message}</div>}

      <button type="button" className="btn btn-primary pd-book-cta" disabled={!canBook} onClick={() => book.mutate()}>
        {book.isPending ? "Booking…" : "Book this rotation"}
      </button>
    </div>
  );
}

/** Student-facing program detail + booking, cloned to the live "Program Details" screen: a hero band,
 *  a detail card with the tag/title/price header + the "10% Approval Deposit Required" line, a
 *  two-column body (image + preceptor + booking on the left; tag chips + details grid + description +
 *  reviews on the right), and a contact footer. No honorarium (the API returns it null for customers).
 *  Fields the catalog API doesn't expose yet (program code, image, location, seats, hospitals, tags,
 *  reviews, unlock) render as faithful placeholders per the frontend-layout-only rule. */
export function ProgramDetailPage() {
  const { id } = useParams();
  const program = useQuery({
    queryKey: ["portal-program", id],
    queryFn: () => getCustomerProgram(id!),
    enabled: !!id
  });
  const p = program.data;

  return (
    <div className="pdetail">
      <section className="pdetail-hero">
        <h1>Program Details</h1>
      </section>

      <div className="pdetail-body">
        {program.isLoading && <div className="card state">Loading…</div>}
        {program.isError && (
          <div className="card state">Couldn’t load this program: {(program.error as Error).message}</div>
        )}

        {p && (
          <div className="pdetail-card">
            <Link to="/portal" className="btn-link pd-back">← Back to browse</Link>

            <div className="pd-head">
              <div className="pd-tagsrow">
                <div className="rcard-tags">
                  <span className="tag-pill">Program {programCode(p.programType, p.programNumber)}</span>
                  <span className="tag-pill">{programTypeLabel(p.programType)}</span>
                  {!isHourly(p) && <span className="tag-pill">{p.maxStudentsPerRotation} seats available</span>}
                </div>
                <span className="pd-mindur">
                  {isHourly(p) ? `${p.minWeeksPerRotation} Hourly` : `For ${p.minWeeksPerRotation} weeks minimum`}
                </span>
              </div>

              <div className="pd-titlerow">
                <h2 className="pd-title">{p.specialtyName}</h2>
                {/* Header price = the whole minimum stay (per-week × min weeks), as on the live site. */}
                <div className="pd-price">
                  ${money(isHourly(p) ? p.retailAmountPerWeek : p.retailAmountPerWeek * p.minWeeksPerRotation)}
                </div>
              </div>

              <div className="pd-subrow">
                <div className="rcard-loc">
                  <svg className="rcard-pin" width="12" height="18" viewBox="0 0 12 18" fill="none" aria-hidden>
                    <path
                      d="M6 0C2.7 0 0 2.7 0 6c0 4.5 6 12 6 12s6-7.5 6-12c0-3.3-2.7-6-6-6zm0 8.2A2.2 2.2 0 1 1 6 3.8a2.2 2.2 0 0 1 0 4.4z"
                      fill="#5AA6FF"
                    />
                  </svg>
                  <span>{[p.city, p.state].filter(Boolean).join(", ") || "—"}</span>
                </div>
                <span className="pd-deposit">10% Approval Deposit Required</span>
              </div>
            </div>

            <div className="pd-cols">
              <div className="pd-left">
                <div className="pd-img">
                  <ProgramImage url={p.imageUrl} className="pd-photo" alt={`${p.specialtyName} program`} />
                </div>
                <div className="pd-preceptor">
                  <h3 className="pd-section">Preceptor</h3>
                  <div className="pd-label">Name</div>
                  <div className="pd-preceptor-name">{p.preceptorName ?? "—"}</div>
                </div>
                <BookingPanel program={p} />
              </div>

              <div className="pd-right">
                <div className="pd-chips">
                  {p.isOpen && <span className="tag-chip">Instant Approval</span>}
                  {p.tags.map((t) => <span key={t} className="tag-chip">{t}</span>)}
                </div>
                <div className="pd-grid">
                  <Detail label="Specialty" value={p.specialtyName} />
                  <Detail label="Affiliated Hospitals" value="—" />
                  <Detail label="Sub-specialty" value="—" />
                  <Detail label="Inpatient" value="—" />
                  <Detail label="LOR Letterhead" value="—" />
                  <Detail label="Max Students per Rotation" value={p.maxStudentsPerRotation} />
                  <Detail label="Designation & Name of Program" value={p.specialtyName} />
                  <Detail label="Min number of weeks" value={p.minWeeksPerRotation} />
                  <Detail label="Privileges" value="—" />
                  <Detail label="Required Documents" value="—" />
                  <Detail label="Research" value="—" />
                  <Detail label="Topic" value="—" />
                </div>

                <div className="pd-desc">
                  <div className="pd-label">Description</div>
                  <div className="pd-desc-body">{p.description || "No description"}</div>
                </div>

                <div className="pd-reviews">
                  <h3 className="pd-section">Reviews(0)</h3>
                  <p className="muted">This is a new program and is pending reviews</p>
                </div>
              </div>
            </div>

            <div className="pd-divider" />
            <div className="pd-contact">
              <div>
                <div className="pd-contact-head">Call Us</div>
                <div>+1 (657) 214-7174</div>
              </div>
              <div>
                <div className="pd-contact-head">E-mail Us</div>
                <div><a href="mailto:join@rotationsplus.org">join@rotationsplus.org</a></div>
              </div>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
