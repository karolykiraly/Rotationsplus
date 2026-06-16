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
  getCustomerMe: vi.fn()
}));

vi.mock("@azure/msal-react", () => ({
  AuthenticatedTemplate: ({ children }: { children: ReactNode }) => (h.authed ? <>{children}</> : null),
  UnauthenticatedTemplate: ({ children }: { children: ReactNode }) => (h.authed ? null : <>{children}</>),
  useMsal: () => ({ instance: { loginRedirect: h.loginRedirect, logoutRedirect: h.logoutRedirect } })
}));
vi.mock("./customerApi", () => ({ getCustomerMe: () => h.getCustomerMe() }));

import { PortalLayout } from "./PortalLayout";

function renderLayout() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <MemoryRouter>
        <PortalLayout />
      </MemoryRouter>
    </QueryClientProvider>
  );
}

describe("PortalLayout", () => {
  beforeEach(() => {
    h.loginRedirect.mockClear();
    h.logoutRedirect.mockClear();
    h.getCustomerMe.mockReset().mockResolvedValue({ objectId: "o", name: "Stu Dent", roles: ["Student"], isStudent: true, isPreceptor: false });
    h.authed = true;
  });

  it("shows the customer sign-in when unauthenticated", async () => {
    h.authed = false;
    renderLayout();
    await userEvent.click(screen.getByRole("button", { name: "Sign in / Sign up" }));
    expect(h.loginRedirect).toHaveBeenCalled();
  });

  it("renders the signed-in customer name and signs out", async () => {
    renderLayout();
    expect(await screen.findByText("Stu Dent")).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Sign out" }));
    expect(h.logoutRedirect).toHaveBeenCalled();
  });
});
