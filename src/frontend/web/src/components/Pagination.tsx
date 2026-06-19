/** First / Previous / numbered window / Next / Last pager — clone of the legacy PaginationComponent.
 *  1-based `page`. Renders nothing when there is a single page. */
export function Pagination({
  page,
  pageSize,
  totalItems,
  onChange,
  maxButtons = 5
}: {
  page: number;
  pageSize: number;
  totalItems: number;
  onChange: (page: number) => void;
  maxButtons?: number;
}) {
  const pages = Math.max(1, Math.ceil(totalItems / pageSize));
  if (pages <= 1) return null;

  // Window of up to `maxButtons` page numbers centered on the active page.
  const half = Math.floor(maxButtons / 2);
  let start = Math.max(1, page - half);
  const end = Math.min(pages, start + maxButtons - 1);
  start = Math.max(1, end - maxButtons + 1);
  const numbers = Array.from({ length: end - start + 1 }, (_, i) => start + i);

  return (
    <div className="pager" role="navigation" aria-label="Pagination">
      <button onClick={() => onChange(1)} disabled={page === 1}>First</button>
      <button onClick={() => onChange(page - 1)} disabled={page === 1}>Previous</button>
      {numbers.map((n) => (
        <button
          key={n}
          className={n === page ? "active" : ""}
          aria-current={n === page ? "page" : undefined}
          onClick={() => onChange(n)}
        >
          {n}
        </button>
      ))}
      <button onClick={() => onChange(page + 1)} disabled={page === pages}>Next</button>
      <button onClick={() => onChange(pages)} disabled={page === pages}>Last</button>
    </div>
  );
}
