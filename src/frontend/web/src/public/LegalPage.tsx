/** A clause within a legal section: an optional bold label (e.g. "a." or "Rotation Fee.") + text,
 *  with an optional nested list of sub-points. */
export interface LegalClause {
  label?: string;
  text: string;
  sub?: string[];
}

/** A block is either a plain paragraph (`text`) or a list of labelled `clauses`. */
export interface LegalBlock {
  text?: string;
  clauses?: LegalClause[];
}

export interface LegalSection {
  id: string;
  title: string;
  blocks: LegalBlock[];
}

export interface LegalContent {
  title: string;
  updated: string;
  intro: LegalBlock[];
  sections: LegalSection[];
}

function Blocks({ blocks }: { blocks: LegalBlock[] }) {
  return (
    <>
      {blocks.map((b, i) =>
        b.clauses ? (
          <dl className="legal-clauses" key={i}>
            {b.clauses.map((c, j) => (
              <div className="legal-clause" key={j}>
                {c.label && <dt>{c.label}</dt>}
                <dd>
                  {c.text}
                  {c.sub && (
                    <ol className="legal-sub" type="i">
                      {c.sub.map((s, k) => (
                        <li key={k}>{s}</li>
                      ))}
                    </ol>
                  )}
                </dd>
              </div>
            ))}
          </dl>
        ) : (
          <p key={i}>{b.text}</p>
        )
      )}
    </>
  );
}

/** Renders a long-form legal page (Terms, Privacy) as a single readable column with a table-of-contents
 *  of anchor links. Faithful reproduction of the live site's legal copy. */
export function LegalPage({ content }: { content: LegalContent }) {
  return (
    <div className="legal">
      <section className="public-page-head">
        <h1>{content.title}</h1>
        <p className="legal-updated">{content.updated}</p>
      </section>

      <section className="section legal-body">
        <div className="legal-intro">
          <Blocks blocks={content.intro} />
        </div>

        {content.sections.length > 1 && (
          <nav className="legal-toc" aria-label="Contents">
            <ol>
              {content.sections.map((s) => (
                <li key={s.id}>
                  <a href={`#${s.id}`}>{s.title}</a>
                </li>
              ))}
            </ol>
          </nav>
        )}

        {content.sections.map((s, i) => (
          <section className="legal-section" id={s.id} key={s.id}>
            <h2>
              {i + 1}. {s.title}
            </h2>
            <Blocks blocks={s.blocks} />
          </section>
        ))}
      </section>
    </div>
  );
}
