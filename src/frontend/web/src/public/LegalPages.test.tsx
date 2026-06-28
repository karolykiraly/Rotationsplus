import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { TermsPage } from "./TermsPage";
import { PrivacyPolicyPage } from "./PrivacyPolicyPage";

describe("TermsPage", () => {
  it("renders the title, a TOC and section content", () => {
    render(<TermsPage />);
    expect(screen.getByRole("heading", { name: "RotationsPlus Terms of Service" })).toBeInTheDocument();
    expect(screen.getByText("Updated February 16th 2026")).toBeInTheDocument();
    // Section headings are numbered.
    expect(screen.getByRole("heading", { name: /1\. RP Services/ })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: /2\. Client Obligations/ })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: /11\. Contact/ })).toBeInTheDocument();
    // TOC anchor link to a section.
    expect(screen.getByRole("link", { name: "Fees" })).toHaveAttribute("href", "#fees");
    expect(screen.getByText(/info@rotationsplus\.org/)).toBeInTheDocument();
  });
});

describe("PrivacyPolicyPage", () => {
  it("renders the intro, the 14-item contents list and section 1", () => {
    render(<PrivacyPolicyPage />);
    expect(screen.getByRole("heading", { name: "Privacy Policy" })).toBeInTheDocument();
    expect(screen.getByText("Last updated June 10, 2021")).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: /What Information Do We Collect/ })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "https://stripe.com/privacy" })).toBeInTheDocument();
  });
});
