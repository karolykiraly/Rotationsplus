import { Link, useParams } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { getCustomerProgram } from "./customerApi";
import { programTypeLabel } from "../programs/programTypes";

const money = (n: number) =>
  n.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });

/** Student-facing program detail. No honorarium (the API returns it null for customers). */
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
      )}
    </>
  );
}
