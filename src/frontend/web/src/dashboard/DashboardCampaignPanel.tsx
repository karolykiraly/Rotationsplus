import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  createCampaign,
  getCampaigns,
  sendCampaign,
  type CampaignStatus,
  type CampaignSummary,
  type EmailAudience
} from "../api";

const AUDIENCES: { value: EmailAudience; label: string }[] = [
  { value: "AllStudents", label: "All students" },
  { value: "StudentsWithBooking", label: "Students who booked" },
  { value: "StudentsWithoutBooking", label: "Students who haven't booked" },
  { value: "AllPreceptors", label: "All preceptors" }
];

const AUDIENCE_LABEL: Record<EmailAudience, string> = Object.fromEntries(
  AUDIENCES.map((a) => [a.value, a.label])
) as Record<EmailAudience, string>;

const STATUS_CLASS: Record<CampaignStatus, string> = {
  Draft: "badge",
  Queued: "badge badge-warn",
  Sending: "badge badge-warn",
  Sent: "badge badge-ok",
  Failed: "badge badge-danger"
};

/** Format an ISO instant as a short local date-time. */
function formatWhen(iso?: string | null): string {
  if (!iso) return "—";
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? "—" : d.toLocaleString(undefined, { dateStyle: "medium", timeStyle: "short" });
}

/** The admin dashboard "Campaign" tab (GET/POST /api/campaigns): compose an email campaign for an
 *  audience and send it (the Worker fans out via the email sender — a fake sender until cutover). The
 *  list shows each campaign's status + sent/failed tally. */
export function DashboardCampaignPanel() {
  const queryClient = useQueryClient();
  const campaigns = useQuery<CampaignSummary[]>({ queryKey: ["campaigns"], queryFn: getCampaigns });

  const [subject, setSubject] = useState("");
  const [body, setBody] = useState("");
  const [audience, setAudience] = useState<EmailAudience>("AllStudents");

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ["campaigns"] });

  const create = useMutation({
    mutationFn: () => createCampaign(subject.trim(), body.trim(), audience),
    onSuccess: () => { setSubject(""); setBody(""); void invalidate(); }
  });

  const send = useMutation({
    mutationFn: (id: string) => sendCampaign(id),
    onSuccess: () => void invalidate()
  });

  const rows = campaigns.data ?? [];
  const canCreate = subject.trim().length > 0 && body.trim().length > 0 && !create.isPending;

  return (
    <section className="dash-card">
      <h2 className="dash-title">Campaign</h2>

      <div className="campaign-compose">
        <div className="field">
          <label htmlFor="c-subject">Subject</label>
          <input id="c-subject" value={subject} maxLength={200} onChange={(e) => setSubject(e.target.value)}
            placeholder="e.g. Spring rotations are open" />
        </div>
        <div className="field">
          <label htmlFor="c-audience">Audience</label>
          <select id="c-audience" value={audience} onChange={(e) => setAudience(e.target.value as EmailAudience)}>
            {AUDIENCES.map((a) => <option key={a.value} value={a.value}>{a.label}</option>)}
          </select>
        </div>
        <div className="field span-2">
          <label htmlFor="c-body">Message</label>
          <textarea id="c-body" value={body} maxLength={10000} rows={5} onChange={(e) => setBody(e.target.value)}
            placeholder="Write your message…" />
        </div>
        <div className="campaign-compose-actions">
          {create.isError && <span className="err">{(create.error as Error).message}</span>}
          <button type="button" className="btn btn-primary" onClick={() => create.mutate()} disabled={!canCreate}>
            {create.isPending ? "Saving…" : "Save draft"}
          </button>
        </div>
      </div>

      {campaigns.isLoading && <div className="state">Loading campaigns…</div>}
      {campaigns.isError && (
        <div className="banner error" role="alert">Couldn’t load campaigns: {(campaigns.error as Error).message}</div>
      )}
      {!campaigns.isLoading && !campaigns.isError && rows.length === 0 && (
        <div className="state">No campaigns yet — compose one above.</div>
      )}

      {rows.length > 0 && (
        <table className="program-table campaign-table">
          <thead>
            <tr><th>Subject</th><th>Audience</th><th>Status</th><th>Sent / Failed</th><th>Sent at</th><th></th></tr>
          </thead>
          <tbody>
            {rows.map((c) => (
              <tr key={c.id}>
                <td className="heading-xxxs">{c.subject}</td>
                <td>{AUDIENCE_LABEL[c.audience]}</td>
                <td><span className={STATUS_CLASS[c.status]}>{c.status}</span></td>
                <td>{c.status === "Draft" ? "—" : `${c.sentCount} / ${c.failedCount}`}</td>
                <td className="doc-due">{formatWhen(c.sentAtUtc)}</td>
                <td className="last-td">
                  {c.status === "Draft" && (
                    <button type="button" className="btn btn-primary button-sm"
                      onClick={() => send.mutate(c.id)} disabled={send.isPending}>
                      {send.isPending && send.variables === c.id ? "Sending…" : "Send"}
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
      {send.isError && <div className="banner error" role="alert">{(send.error as Error).message}</div>}
    </section>
  );
}
