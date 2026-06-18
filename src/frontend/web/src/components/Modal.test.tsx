import { describe, it, expect, vi } from "vitest";
import { useState } from "react";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Modal } from "./Modal";

function renderModal() {
  const onClose = vi.fn();
  render(
    <Modal title="Edit thing" onClose={onClose}>
      <div className="modal-body">body content</div>
    </Modal>
  );
  return onClose;
}

describe("Modal", () => {
  it("renders as a labelled dialog", () => {
    renderModal();
    expect(screen.getByRole("dialog", { name: "Edit thing" })).toBeInTheDocument();
  });

  it("closes on Escape", async () => {
    const onClose = renderModal();
    await userEvent.keyboard("{Escape}");
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it("closes when the overlay is clicked but not the dialog body", async () => {
    const onClose = renderModal();

    // Clicking inside the dialog must NOT close it.
    await userEvent.click(screen.getByText("body content"));
    expect(onClose).not.toHaveBeenCalled();

    // Clicking the overlay (outside the dialog) closes it.
    await userEvent.click(screen.getByRole("dialog").parentElement!);
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it("moves focus to the first focusable element on open", () => {
    render(
      <Modal title="t" onClose={vi.fn()}>
        <button>First</button>
        <button>Second</button>
      </Modal>
    );
    expect(screen.getByRole("button", { name: "First" })).toHaveFocus();
  });

  it("traps Tab within the dialog", async () => {
    render(
      <Modal title="t" onClose={vi.fn()}>
        <button>First</button>
        <button>Last</button>
      </Modal>
    );
    const first = screen.getByRole("button", { name: "First" });
    const last = screen.getByRole("button", { name: "Last" });

    last.focus();
    await userEvent.tab(); // from the last element wraps to the first
    expect(first).toHaveFocus();

    await userEvent.tab({ shift: true }); // shift+Tab from the first wraps to the last
    expect(last).toHaveFocus();
  });

  it("does not re-steal focus when the parent re-renders with a new onClose identity", async () => {
    // Callers pass inline-arrow onClose handlers (new identity each render). A parent re-render while
    // the modal is open must NOT yank focus back to the first field nor break focus restore.
    function Harness() {
      const [tick, setTick] = useState(0);
      const [open, setOpen] = useState(false);
      return (
        <>
          <button onClick={() => setOpen(true)}>Open</button>
          <button onClick={() => setTick((t) => t + 1)}>Bump {tick}</button>
          {open && (
            <Modal title="t" onClose={() => setOpen(false)}>
              <input aria-label="first" />
              <input aria-label="second" />
            </Modal>
          )}
        </>
      );
    }
    render(<Harness />);
    const trigger = screen.getByRole("button", { name: "Open" });
    await userEvent.click(trigger);
    expect(screen.getByLabelText("first")).toHaveFocus(); // effect moved focus into the dialog

    // Force a parent re-render (new onClose closure) while the modal is open. Clicking the bump button
    // moves focus to it; the modal's effect must NOT re-run and steal focus back to the first field.
    const bump = screen.getByRole("button", { name: /Bump/ });
    await userEvent.click(bump);
    expect(screen.getByLabelText("first")).not.toHaveFocus(); // not re-stolen
    expect(bump).toHaveFocus();

    // Focus restore still targets the original trigger, not a now-unmounted dialog field.
    await userEvent.keyboard("{Escape}");
    expect(trigger).toHaveFocus();
  });

  it("restores focus to the trigger when it closes", async () => {
    function Harness() {
      const [open, setOpen] = useState(false);
      return (
        <>
          <button onClick={() => setOpen(true)}>Open</button>
          {open && (
            <Modal title="t" onClose={() => setOpen(false)}>
              <button>Inside</button>
            </Modal>
          )}
        </>
      );
    }
    render(<Harness />);
    const trigger = screen.getByRole("button", { name: "Open" });
    await userEvent.click(trigger);
    expect(screen.getByRole("button", { name: "Inside" })).toHaveFocus(); // focus moved in

    await userEvent.keyboard("{Escape}");
    expect(trigger).toHaveFocus(); // restored to the trigger
  });
});
