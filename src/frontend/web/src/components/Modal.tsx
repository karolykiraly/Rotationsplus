import { useEffect, useRef, type ReactNode } from "react";

interface ModalProps {
  title: string;
  onClose: () => void;
  children: ReactNode;
  /** Wider dialog for multi-field forms. */
  wide?: boolean;
}

const FOCUSABLE =
  'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';

/** Minimal accessible modal: overlay click + Escape close, dialog role. Focus is moved into the dialog
 *  on open, trapped within it while open (Tab/Shift+Tab cycle), and restored to the trigger on close.
 *  Footer/body are composed by the caller so each form owns its own buttons. */
export function Modal({ title, onClose, children, wide = false }: ModalProps) {
  const dialogRef = useRef<HTMLDivElement>(null);
  // Keep the latest onClose in a ref so the focus effect runs exactly once per open. Callers pass
  // unstable handlers (inline arrows), so depending on onClose directly would re-run the effect on
  // every parent re-render — re-stealing focus mid-edit and corrupting the captured trigger.
  const onCloseRef = useRef(onClose);
  onCloseRef.current = onClose;

  useEffect(() => {
    const previouslyFocused = document.activeElement as HTMLElement | null;
    const dialog = dialogRef.current;
    const focusables = () => (dialog ? Array.from(dialog.querySelectorAll<HTMLElement>(FOCUSABLE)) : []);

    // Move focus into the dialog (first field, or the dialog itself if it has none).
    (focusables()[0] ?? dialog)?.focus();

    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        onCloseRef.current();
        return;
      }
      if (e.key !== "Tab") return;

      const items = focusables();
      if (items.length === 0) {
        e.preventDefault();
        return;
      }
      const first = items[0];
      const last = items[items.length - 1];
      // Wrap at the edges. Treat "focus currently outside the trap" (index -1, e.g. on the dialog
      // container) as being at the boundary so Tab/Shift+Tab can't escape the dialog.
      const idx = items.indexOf(document.activeElement as HTMLElement);
      if (e.shiftKey && idx <= 0) {
        e.preventDefault();
        last.focus();
      } else if (!e.shiftKey && (idx === -1 || idx === items.length - 1)) {
        e.preventDefault();
        first.focus();
      }
    };

    window.addEventListener("keydown", onKey);
    return () => {
      window.removeEventListener("keydown", onKey);
      // Restore focus to whatever opened the dialog, if it's still in the document.
      if (previouslyFocused?.isConnected) previouslyFocused.focus();
    };
  }, []);

  return (
    <div className="overlay" onClick={onClose}>
      <div
        ref={dialogRef}
        className={`modal${wide ? " wide" : ""}`}
        role="dialog"
        aria-modal="true"
        aria-label={title}
        tabIndex={-1}
        onClick={(e) => e.stopPropagation()}
      >
        <h3>{title}</h3>
        {children}
      </div>
    </div>
  );
}
