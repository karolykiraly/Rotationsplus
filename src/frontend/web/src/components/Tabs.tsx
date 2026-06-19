/** Program-type style tab strip (clone of the legacy `Tab` component): a row of labels with a
 *  brand underline under the active one. Controlled via `active` + `onChange`. */
export function Tabs({
  labels,
  active,
  onChange
}: {
  labels: string[];
  active: number;
  onChange: (index: number) => void;
}) {
  return (
    <div className="tabs" role="tablist">
      {labels.map((label, i) => (
        <div
          key={label}
          role="tab"
          aria-selected={i === active}
          tabIndex={0}
          className={`tab${i === active ? " active" : ""}`}
          onClick={() => onChange(i)}
          onKeyDown={(e) => {
            if (e.key === "Enter" || e.key === " ") {
              e.preventDefault();
              onChange(i);
            }
          }}
        >
          {label}
        </div>
      ))}
    </div>
  );
}
