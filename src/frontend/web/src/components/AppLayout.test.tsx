import { describe, it, expect, beforeEach, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactNode } from "react";

const h = vi.hoisted(() => ({
  authed: true,
  loginRedirect: vi.fn().mockResolvedValue(undefined),
  logoutRedirect: vi.fn().mockResolvedValue(undefined),
  getMe: vi.fn()
}));

vi.mock("@azure/msal-react", () => ({
  AuthenticatedTemplate: ({ children }: { children: ReactNode }) => (h.authed ? <>{children}</> : null),
  UnauthenticatedTemplate: ({ children }: { children: ReactNode }) => (h.authed ? null : <>{children}</>),
  useMsal: () => ({ instance: { loginRedirect: h.loginRedirect, logoutRedirect: h.logoutRedirect } })
}));

vi.mock("../api", () => ({ getMe: () => h.getMe() }));

import { AppLayout } from "./AppLayout";

const ADMIN = { objectId: "o", name: "Ada Admin", username: "ada@x", roles: ["Admin"], isStaff: true, profileId: "p" };

function renderLayout() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <MemoryRouter>
        <AppLayout />
      </MemoryRouter>
    </QueryClientProvider>
  );
}

describe("AppLayout", () => {
  beforeEach(() => {
    h.loginRedirect.mockClear();
    h.logoutRedirect.mockClear();
    h.getMe.mockReset().mockResolvedValue(ADMIN);
    h.authed = true;
  });

  it("shows the sign-in screen when unauthenticated", async () => {
    h.authed = false;
    renderLayout();
    await userEvent.click(screen.getByRole("button", { name: "Sign in" }));
    expect(h.loginRedirect).toHaveBeenCalled();
  });

  it("renders the cloned admin sidebar and signs out via the user menu", async () => {
    renderLayout();
    const userBtn = await screen.findByRole("button", { name: /Ada Admin/ });
    // Legacy-cloned nav items (wired + not-yet-built).
    expect(screen.getByText("Dashboard")).toBeInTheDocument();
    expect(screen.getByText("Programs")).toBeInTheDocument();
    expect(screen.getByText("Rotations")).toBeInTheDocument();
    expect(screen.getByText("Analytics")).toBeInTheDocument();
    expect(screen.getByText("Honorarium")).toBeInTheDocument();

    // Sign out lives behind the header user dropdown.
    expect(screen.queryByRole("button", { name: "Sign out" })).not.toBeInTheDocument();
    await userEvent.click(userBtn);
    await userEvent.click(screen.getByRole("menuitem", { name: "Sign out" }));
    expect(h.logoutRedirect).toHaveBeenCalled();
  });

  it("hides the admin nav for non-admin staff", async () => {
    h.getMe.mockResolvedValue({ ...ADMIN, name: "Cody Coordinator", roles: ["Coordinator"] });
    renderLayout();
    expect(await screen.findByRole("button", { name: /Cody Coordinator/ })).toBeInTheDocument();
    expect(screen.queryByText("Dashboard")).not.toBeInTheDocument();
    expect(screen.queryByText("Programs")).not.toBeInTheDocument();
    expect(screen.queryByText("Rotations")).not.toBeInTheDocument();
  });
});
