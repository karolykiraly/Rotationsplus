import { describe, it, expect, beforeEach, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

const h = vi.hoisted(() => ({
  loginRedirect: vi.fn().mockResolvedValue(undefined),
  logoutRedirect: vi.fn().mockResolvedValue(undefined),
  getMe: vi.fn(),
  state: { accounts: [] as unknown[] }
}));

vi.mock("@azure/msal-react", () => ({
  useMsal: () => ({
    instance: { loginRedirect: h.loginRedirect, logoutRedirect: h.logoutRedirect },
    accounts: h.state.accounts
  })
}));

vi.mock("./api", () => ({ getMe: () => h.getMe() }));

import App from "./App";

describe("App", () => {
  beforeEach(() => {
    h.loginRedirect.mockClear();
    h.logoutRedirect.mockClear();
    h.getMe.mockReset();
    h.state.accounts = [];
  });

  it("shows the brand header", () => {
    render(<App />);
    expect(screen.getByText("Rotations Plus — Staff")).toBeInTheDocument();
  });

  it("triggers loginRedirect when not signed in", async () => {
    render(<App />);
    await userEvent.click(screen.getByRole("button", { name: "Sign in" }));
    expect(h.loginRedirect).toHaveBeenCalled();
  });

  it("calls the API and renders the identity when signed in", async () => {
    h.state.accounts = [{ homeAccountId: "a" }];
    h.getMe.mockResolvedValue({
      objectId: "oid-1",
      name: "Jane",
      username: "jane@x",
      roles: ["Admin"],
      isStaff: true
    });
    render(<App />);
    await userEvent.click(screen.getByRole("button", { name: "Call /api/me" }));
    expect(await screen.findByText("Authenticated identity")).toBeInTheDocument();
    expect(screen.getByText("oid-1")).toBeInTheDocument();
  });

  it("shows an error when the API call fails", async () => {
    h.state.accounts = [{ homeAccountId: "a" }];
    h.getMe.mockRejectedValue(new Error("boom"));
    render(<App />);
    await userEvent.click(screen.getByRole("button", { name: "Call /api/me" }));
    expect(await screen.findByRole("alert")).toHaveTextContent("boom");
  });

  it("signs out", async () => {
    h.state.accounts = [{ homeAccountId: "a" }];
    render(<App />);
    await userEvent.click(screen.getByRole("button", { name: "Sign out" }));
    expect(h.logoutRedirect).toHaveBeenCalled();
  });
});
