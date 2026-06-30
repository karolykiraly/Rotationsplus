/** A dependency-free dual-thumb range slider (two overlaid native range inputs with a coloured fill
 *  between the thumbs). Replaces the legacy react-range slider in the FilterProgram modal without
 *  pulling in a new dependency, matching its look (grey track, brand-pink fill, two round thumbs).
 *  Native inputs keep it keyboard-accessible and test-friendly (fireEvent.change on either thumb). */
interface Props {
  min: number;
  max: number;
  step?: number;
  value: [number, number];
  onChange: (value: [number, number]) => void;
  minLabel?: string;
  maxLabel?: string;
}

export function RangeSlider({ min, max, step = 1, value, onChange, minLabel = "Minimum", maxLabel = "Maximum" }: Props) {
  const [lo, hi] = value;
  const span = max - min || 1;
  const pct = (v: number) => ((Math.min(Math.max(v, min), max) - min) / span) * 100;

  return (
    <div className="range-slider">
      <div className="range-track" />
      <div className="range-fill" style={{ left: `${pct(lo)}%`, right: `${100 - pct(hi)}%` }} />
      <input
        className="range-thumb range-thumb-lo"
        type="range"
        min={min}
        max={max}
        step={step}
        value={lo}
        aria-label={minLabel}
        // The two range inputs overlap; the later (high) one paints on top by DOM order. When the low
        // thumb sits in the upper half (where the two thumbs converge near max), raise it so it stays
        // mouse-grabbable instead of being trapped under the high thumb.
        style={{ zIndex: lo >= (min + max) / 2 ? 4 : undefined }}
        // Keep the low thumb from crossing the high one.
        onChange={(e) => onChange([Math.min(Number(e.target.value), hi), hi])}
      />
      <input
        className="range-thumb range-thumb-hi"
        type="range"
        min={min}
        max={max}
        step={step}
        value={hi}
        aria-label={maxLabel}
        onChange={(e) => onChange([lo, Math.max(Number(e.target.value), lo)])}
      />
    </div>
  );
}
