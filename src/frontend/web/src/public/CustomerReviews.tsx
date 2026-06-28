import { useEffect, useState } from "react";

/** Static reproduction of the live "What Our Customers Say" Google-Reviews block. The live site embeds
 *  an external Elfsight Google-reviews widget (a rotating carousel of review cards); we render the same
 *  section as a dependency-free rotating carousel from static content, so the (private) DEV page makes
 *  no external third-party call — owner decision 2026-06-28 ("reproduce statically"). Swap to the live
 *  Elfsight widget at the public www cutover. */
const REVIEWS: { name: string; initial: string; text: string }[] = [
  {
    name: "Roopesh Reddy",
    initial: "R",
    text: "Had a wonderful experience working with Mr. Omer. He was always responsive, professional, and genuinely cared about finding the right rotation for me."
  },
  {
    name: "Alexander Vega Real",
    initial: "A",
    text: "Great mentor and teacher. RotationsPlus gave me the opportunity to do a rotation with a top hospitalist in my specialty. Highly recommend them."
  },
  {
    name: "Manjesh Dalal",
    initial: "M",
    text: "I completed two clinical rotations through RotationsPlus and my experience was excellent. The team was transparent and supportive throughout."
  },
  {
    name: "Kinda Ghaffari",
    initial: "K",
    text: "I recently completed a fantastic, high-yield rotation and cannot recommend this experience enough. The process was smooth from start to finish."
  }
];

const ROTATE_MS = 6000;

export function CustomerReviews() {
  const [index, setIndex] = useState(0);
  const [paused, setPaused] = useState(false);

  const go = (i: number) => setIndex((i + REVIEWS.length) % REVIEWS.length);

  // Auto-advance like the live Elfsight carousel; pauses while hovered.
  useEffect(() => {
    if (paused) return;
    const timer = setInterval(() => setIndex((i) => (i + 1) % REVIEWS.length), ROTATE_MS);
    return () => clearInterval(timer);
  }, [paused]);

  const review = REVIEWS[index];

  return (
    <section className="reviews">
      <h2 className="reviews-title">What Our Customers Say</h2>
      <div className="reviews-head">
        <span className="reviews-google">Google Reviews</span>
        <span className="reviews-score">4.9</span>
        <span className="reviews-stars" aria-label="4.9 out of 5 stars">★★★★★</span>
        <span className="reviews-count">(461)</span>
        <a
          className="btn btn-primary reviews-cta"
          href="https://www.google.com/search?q=RotationsPlus+reviews"
          target="_blank"
          rel="noopener noreferrer"
        >
          Review us on Google
        </a>
      </div>

      <div
        className="reviews-carousel"
        onMouseEnter={() => setPaused(true)}
        onMouseLeave={() => setPaused(false)}
      >
        <button
          type="button"
          className="reviews-arrow reviews-arrow-prev"
          aria-label="Previous review"
          onClick={() => go(index - 1)}
        >
          ‹
        </button>

        {/* Stable element (no changing key) so the aria-live region persists and announces updates. */}
        <article className="review-card" aria-live="polite">
          <div className="review-top">
            <span className="review-avatar" aria-hidden="true">{review.initial}</span>
            <span className="review-name">{review.name}</span>
          </div>
          <div className="review-stars" aria-label="5 out of 5 stars">★★★★★</div>
          <p className="review-text">{review.text}</p>
        </article>

        <button
          type="button"
          className="reviews-arrow reviews-arrow-next"
          aria-label="Next review"
          onClick={() => go(index + 1)}
        >
          ›
        </button>
      </div>

      <div className="reviews-dots" role="group" aria-label="Choose a customer review">
        {REVIEWS.map((r, i) => (
          <button
            key={r.name}
            type="button"
            aria-current={i === index ? "true" : undefined}
            aria-label={`Show review from ${r.name}`}
            className={`reviews-dot${i === index ? " active" : ""}`}
            onClick={() => go(i)}
          />
        ))}
      </div>
    </section>
  );
}
