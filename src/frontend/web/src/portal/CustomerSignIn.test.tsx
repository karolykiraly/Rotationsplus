import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";

const loginRedirect = vi.fn();
vi.mock("@azure/msal-react", () => ({ useMsal: () => ({ instance: { loginRedirect } }) }));

import { CustomerSignIn } from "./CustomerSignIn";

describe("CustomerSignIn", () => {
  beforeEach(() => loginRedirect.mockReset().mockResolvedValue(undefined));

  it("renders the welcome hero with the brand and the sign-in CTA", () => {
    render(<CustomerSignIn />);
    expect(screen.getByRole("heading", { name: /Find your clinical rotation/ })).toBeInTheDocument();
    expect(screen.getByAltText("Rotations Plus")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Sign in / Sign up" })).toBeInTheDocument();
  });

  it("triggers the CIAM redirect on click", () => {
    render(<CustomerSignIn />);
    fireEvent.click(screen.getByRole("button", { name: "Sign in / Sign up" }));
    expect(loginRedirect).toHaveBeenCalledTimes(1);
  });
});
