import { useEffect, useState } from "react";

/** A program's hospital image with a graceful fallback to the surrounding gray placeholder box.
 *
 *  Renders nothing (so the parent placeholder shows through) when there's no URL or the image fails
 *  to load. The failed state is keyed to `url` and reset whenever it changes — this matters because
 *  the API mints a fresh short-lived SAS URL on every fetch, so a refetch hands us a new (valid) URL
 *  for the same program; a stale "failed/hidden" flag must not suppress it. (An earlier imperative
 *  `onError → style.display='none'` left the reused <img> node hidden across such src swaps.) */
export function ProgramImage({
  url,
  className,
  alt,
}: {
  url?: string | null;
  className: string;
  alt: string;
}) {
  const [failed, setFailed] = useState(false);

  // A new URL gets a fresh chance to load (resets a prior failure for this slot).
  useEffect(() => setFailed(false), [url]);

  if (!url || failed) {
    return null;
  }

  return <img className={className} src={url} alt={alt} loading="lazy" onError={() => setFailed(true)} />;
}
