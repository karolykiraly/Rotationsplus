import { describe, it, expect, beforeEach, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter, Routes, Route } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

const h = vi.hoisted(() => ({ getMe: vi.fn() }));
vi.mock("../api", () => ({ getMe: () => h.getMe() }));

import { PostLoginRedirect } from "./PostLoginRedirect";

function renderRedirect() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <MemoryRouter initialEntries={["/admin"]}>
        <Routes>
          <Route path="/admin" element={<PostLoginRedirect />} />
          <Route path="/admin/dashboard" element={<div>DASH</div>} />
          <Route path="/admin/programs" element={<div>PROGRAMS</div>} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>
  );
}

describe("PostLoginRedirect", () => {
  beforeEach(() => h.getMe.mockReset());

  it("shows a loading state, then routes the signed-in user by role", async () => {
    h.getMe.mockResolvedValue({ objectId: "o", roles: ["Sales"] });
    renderRedirect();
    expect(screen.getByText(/Loading your console/)).toBeInTheDocument();
    expect(await screen.findByText("PROGRAMS")).toBeInTheDocument();
  });

  it("routes an admin to the dashboard", async () => {
    h.getMe.mockResolvedValue({ objectId: "o", roles: ["Admin"] });
    renderRedirect();
    expect(await screen.findByText("DASH")).toBeInTheDocument();
  });
});
