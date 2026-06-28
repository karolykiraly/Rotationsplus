import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Routes, Route } from "react-router-dom";
import { PublicLayout } from "./PublicLayout";

/** PublicLayout renders with NO MSAL mock — that it works at all proves the public tree is
 *  provider-free (no useMsal call anywhere in the public branch). */
function renderLayout(path = "/") {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route element={<PublicLayout />}>
          <Route index element={<div>HOME CHILD</div>} />
        </Route>
      </Routes>
    </MemoryRouter>
  );
}

describe("PublicLayout", () => {
  it("renders the nav, CTAs, the routed child and the footer", () => {
    renderLayout();
    expect(screen.getByText("HOME CHILD")).toBeInTheDocument();
    // Blog appears in both the header nav and the footer nav — both point at /blog.
    const blogLinks = screen.getAllByRole("link", { name: "Blog" });
    expect(blogLinks.length).toBeGreaterThan(0);
    blogLinks.forEach((l) => expect(l).toHaveAttribute("href", "/blog"));
    // CTAs: Login → staff entry, Sign Up → customer (CIAM) portal.
    expect(screen.getByRole("link", { name: "Login" })).toHaveAttribute("href", "/rotationsplusadmin");
    expect(screen.getByRole("link", { name: "Sign Up" })).toHaveAttribute("href", "/portal");
  });

  it("toggles the mobile menu", async () => {
    renderLayout();
    const burger = screen.getByRole("button", { name: /toggle menu/i });
    expect(burger).toHaveAttribute("aria-expanded", "false");
    await userEvent.click(burger);
    expect(burger).toHaveAttribute("aria-expanded", "true");
    await userEvent.click(burger);
    expect(burger).toHaveAttribute("aria-expanded", "false");
  });
});
