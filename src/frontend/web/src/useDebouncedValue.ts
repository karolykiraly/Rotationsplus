import { useEffect, useState } from "react";

/** Returns a copy of `value` that only updates after it has stopped changing for `delayMs`. Used to debounce
 *  a search input so server-paginated lists fire one request per pause, not one per keystroke. */
export function useDebouncedValue<T>(value: T, delayMs = 300): T {
  const [debounced, setDebounced] = useState(value);

  useEffect(() => {
    const handle = setTimeout(() => setDebounced(value), delayMs);
    return () => clearTimeout(handle);
  }, [value, delayMs]);

  return debounced;
}
