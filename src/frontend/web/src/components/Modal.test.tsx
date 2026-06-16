import { describe, it, expect, vi } from "vitest";
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
});
