import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import {
  getDashboardTodos,
  type DashboardTodos,
  type DocumentTodoItem,
  type PaymentTodoItem,
  type PreceptorTodoItem
} from "../api";

/** Format a YYYY-MM-DD wire date without a timezone shift (parse the parts directly). */
function formatDate(iso: string): string {
  const [y, m, d] = iso.split("-").map(Number);
  if (!y || !m || !d) return iso;
  return new Date(y, m - 1, d).toLocaleDateString(undefined, { year: "numeric", month: "short", day: "numeric" });
}

/** One to-do queue card: a titled list with a count badge, an "all clear" empty state, and a
 *  "+N more" link into the owning admin screen when the full count exceeds the preview. */
function TodoQueue<T>({
  title,
  count,
  items,
  to,
  linkLabel,
  renderItem,
  itemKey
}: {
  title: string;
  count: number;
  items: T[];
  to: string;
  linkLabel: string;
  renderItem: (item: T) => React.ReactNode;
  itemKey: (item: T) => string;
}) {
  const remaining = count - items.length;
  return (
    <section className="todo-queue">
      <div className="todo-queue-head">
        <h3 className="todo-queue-title">{title}</h3>
        <span className={`todo-count${count > 0 ? " has" : ""}`}>{count}</span>
      </div>
      {count === 0 ? (
        <div className="todo-empty">All clear — nothing waiting.</div>
      ) : (
        <ul className="todo-list">
          {items.map((it) => (
            <li key={itemKey(it)} className="todo-item">{renderItem(it)}</li>
          ))}
          {remaining > 0 && (
            <li className="todo-more">
              <Link to={to}>+{remaining} more — {linkLabel}</Link>
            </li>
          )}
        </ul>
      )}
    </section>
  );
}

/** The admin dashboard "ToDo's" tab: the actionable work queues (GET /api/dashboard/todos) — documents
 *  awaiting review, rotations awaiting payment, and preceptors awaiting approval — each linking into the
 *  screen where the admin clears it. */
export function DashboardTodosPanel() {
  const todos = useQuery<DashboardTodos>({ queryKey: ["dashboard-todos"], queryFn: getDashboardTodos });

  if (todos.isLoading) return <section className="dash-card state">Loading your to-do's…</section>;
  if (todos.isError) {
    return <section className="dash-card state">Couldn’t load your to-do's: {(todos.error as Error).message}</section>;
  }
  const data = todos.data;
  if (!data) return null;

  return (
    <section className="dash-card">
      <h2 className="dash-title">ToDo's</h2>
      <div className="todo-grid">
        <TodoQueue<DocumentTodoItem>
          title="Documents to review"
          count={data.documentsToReview.count}
          items={data.documentsToReview.items}
          to="/admin/students"
          linkLabel="review documents"
          itemKey={(d) => d.documentId}
          renderItem={(d) => (
            <>
              <span className="todo-primary">{d.studentName}</span>
              <span className="todo-secondary">{d.documentTypeName} · R{d.rotationNumber}</span>
            </>
          )}
        />
        <TodoQueue<PaymentTodoItem>
          title="Awaiting payment"
          count={data.awaitingPayment.count}
          items={data.awaitingPayment.items}
          to="/admin/rotations"
          linkLabel="view rotations"
          itemKey={(p) => p.rotationId}
          renderItem={(p) => (
            <>
              <span className="todo-primary">{p.studentName}</span>
              <span className="todo-secondary">{p.specialtyName} · R{p.rotationNumber} · starts {formatDate(p.startDate)}</span>
            </>
          )}
        />
        <TodoQueue<PreceptorTodoItem>
          title="Preceptor approvals"
          count={data.preceptorApprovals.count}
          items={data.preceptorApprovals.items}
          to="/admin/permission"
          linkLabel="review approvals"
          itemKey={(p) => p.preceptorId}
          renderItem={(p) => (
            <>
              <span className="todo-primary">{p.fullName}</span>
              <span className="todo-secondary">{p.specialtyName} · {p.email}</span>
            </>
          )}
        />
      </div>
    </section>
  );
}
