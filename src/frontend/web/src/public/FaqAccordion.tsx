import { useState } from "react";

export interface FaqItem {
  q: string;
  a: string;
}

/** A simple expand/collapse FAQ list (numbered), matching the legacy site's accordion. Reused by the
 *  For-Preceptors / Consulting pages and the standalone FAQ page (LP-4). */
export function FaqAccordion({ items }: { items: FaqItem[] }) {
  const [open, setOpen] = useState<number | null>(null);

  return (
    <div className="faq-list">
      {items.map((item, i) => {
        const isOpen = open === i;
        return (
          <div className="faq-item" key={item.q}>
            <button
              className="faq-q"
              aria-expanded={isOpen}
              onClick={() => setOpen(isOpen ? null : i)}
            >
              <span className="faq-num">{String(i + 1).padStart(2, "0")}</span>
              <span className="faq-text">{item.q}</span>
              <span className="faq-sign" aria-hidden="true">{isOpen ? "–" : "+"}</span>
            </button>
            {isOpen && <div className="faq-a">{item.a}</div>}
          </div>
        );
      })}
    </div>
  );
}
