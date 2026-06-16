import { describe, it, expect, beforeEach, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

const h = vi.hoisted(() => ({ getMe: vi.fn() }));
vi.mock("../api", () => ({ getMe: () => h.getMe() }));

import { HomePage } from "./HomePage";

function newClient() {
  // retryOnMount:false so a cache-seeded error state isn't immediately refetched by the mounted observer.
  return new QueryClient({ defaultOptions: { queries: { retry: false, retryOnMount: false } } });
}

function renderHome(qc = newClient()) {
  return render(
    <QueryClientProvider client={qc}>
      <HomePage />
    </QueryClientProvider>
  );
}

describe("HomePage", () => {
  beforeEach(() => h.getMe.mockReset());

  it("renders the authenticated identity with role badges", async () => {
    h.getMe.mockResolvedValue({
      objectId: "oid-1",
      name: "Ada Admin",
      username: "ada@x",
      roles: ["Admin", "Sales"],
      isStaff: true,
      profileId: "p1",
      lastSignInAtUtc: "2026-06-16T12:00:00Z"
    });
    renderHome();

    expect(await screen.findByText("Ada Admin")).toBeInTheDocument();
    expect(screen.getByText("oid-1")).toBeInTheDocument();
    expect(screen.getByText("Admin")).toBeInTheDocument();
    expect(screen.getByText("Sales")).toBeInTheDocument();
  });

  it("shows an error when the profile fails to load", async () => {
    // Seed the cache into an error state (prefetchQuery swallows the rejection) so the test exercises
    // the error UI without a live floating rejection.
    const qc = newClient();
    await qc.prefetchQuery({ queryKey: ["me"], queryFn: () => Promise.reject(new Error("boom")) });
    renderHome(qc);
    expect(await screen.findByRole("alert")).toHaveTextContent("boom");
  });
});
