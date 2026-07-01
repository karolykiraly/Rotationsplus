import { describe, it, expect, beforeEach, vi } from "vitest";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

// The embedded directories have their own tests — here we mock them to unit-test just the hub shell.
vi.mock("../students/StudentsPage", () => ({ StudentsPage: () => <div>STUDENTS_PANEL</div> }));
vi.mock("../preceptors/PreceptorsPage", () => ({ PreceptorsPage: () => <div>PRECEPTORS_PANEL</div> }));

const h = vi.hoisted(() => ({ useMe: vi.fn() }));
vi.mock("../useMe", () => ({ useMe: () => h.useMe() }));

import { ContactsPage } from "./ContactsPage";

describe("ContactsPage (Contacts hub)", () => {
  beforeEach(() => {
    h.useMe.mockReset().mockReturnValue({ user: { isAdmin: true } });
  });

  it("renders the five production tabs and the Students directory by default", () => {
    render(<ContactsPage />);
    for (const label of ["Students", "Preceptors", "Sales", "SDR", "Contacts"]) {
      expect(screen.getByRole("tab", { name: label })).toBeInTheDocument();
    }
    expect(screen.getByText("STUDENTS_PANEL")).toBeInTheDocument();
    expect(screen.queryByText("PRECEPTORS_PANEL")).not.toBeInTheDocument();
  });

  it("switches to the Preceptors directory tab", async () => {
    render(<ContactsPage />);
    await userEvent.click(screen.getByRole("tab", { name: "Preceptors" }));
    expect(screen.getByText("PRECEPTORS_PANEL")).toBeInTheDocument();
    expect(screen.queryByText("STUDENTS_PANEL")).not.toBeInTheDocument();
  });

  it("shows a placeholder for the not-yet-ported tabs (Sales/SDR/Contacts)", async () => {
    render(<ContactsPage />);
    await userEvent.click(screen.getByRole("tab", { name: "Sales" }));
    const placeholder = screen.getByText(/being ported next/i);
    expect(placeholder).toBeInTheDocument();
    // The tab name is echoed in the placeholder body (a <strong> inside it).
    expect(within(placeholder).getByText("Sales")).toBeInTheDocument();
  });

  it("blocks non-admins", () => {
    h.useMe.mockReturnValue({ user: { isAdmin: false } });
    render(<ContactsPage />);
    expect(screen.getByText(/need the Admin role/i)).toBeInTheDocument();
    expect(screen.queryByRole("tab", { name: "Students" })).not.toBeInTheDocument();
  });
});
