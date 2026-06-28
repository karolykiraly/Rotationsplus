import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { Footer } from "./Footer";

describe("Footer", () => {
  it("renders the marketing nav links, legal links and contact", () => {
    render(
      <MemoryRouter>
        <Footer />
      </MemoryRouter>
    );

    expect(screen.getByRole("link", { name: "For Preceptors" })).toHaveAttribute("href", "/for-preceptors");
    // Production's footer lists Blog (live) and leaves Resources commented out — match that.
    expect(screen.getByRole("link", { name: "Blog" })).toHaveAttribute("href", "/blog");
    expect(screen.queryByRole("link", { name: "Resources" })).not.toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Privacy Policy" })).toHaveAttribute("href", "/privacy-policy");
    expect(screen.getByRole("link", { name: "Terms of Service" })).toHaveAttribute("href", "/terms");
    // Production contact details (.org email, the live mailing address).
    expect(screen.getByText(/info@rotationsplus\.org/)).toBeInTheDocument();
    expect(screen.getByText(/777 South Figueroa Street Ste 4600/)).toBeInTheDocument();
    expect(screen.getByText(/RotationsPlus LLC/)).toBeInTheDocument();
  });

  it("renders the four social media links", () => {
    render(
      <MemoryRouter>
        <Footer />
      </MemoryRouter>
    );

    expect(screen.getByRole("link", { name: "Facebook" })).toHaveAttribute(
      "href",
      "https://www.facebook.com/RotationsPlus-103042048784406/"
    );
    expect(screen.getByRole("link", { name: "Instagram" })).toHaveAttribute("href", "https://www.instagram.com/rotationsplus/");
    expect(screen.getByRole("link", { name: "YouTube" })).toHaveAttribute("href", "https://youtube.com/channel/UC8NQ51NzVpMe_8xvTDWXvUQ");
    expect(screen.getByRole("link", { name: "Reddit" })).toHaveAttribute("href", "https://www.reddit.com/user/rotationsplus");
  });
});
