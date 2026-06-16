/// <reference types="vitest/config" />
import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// Vite 8 on Node 22 (see Plan_Architecture.md §3.7). Vitest config colocated.
export default defineConfig({
  plugins: [react()],
  server: { port: 5173 },
  build: { outDir: "dist" },
  test: {
    globals: true,
    environment: "jsdom",
    setupFiles: ["./src/setupTests.ts"],
    coverage: {
      provider: "v8",
      reporter: ["text", "lcov"],
      include: ["src/**/*.{ts,tsx}"],
      exclude: [
        "src/**/*.test.{ts,tsx}",
        "src/main.tsx",
        // Pure composition/wiring (no logic) — exercised by booting the app, not unit-tested.
        "src/router.tsx",
        "src/queryClient.ts",
        "src/components/StaffMsalShell.tsx",
        "src/portal/CustomerMsalShell.tsx",
        "src/vite-env.d.ts",
        "src/setupTests.ts"
      ],
      thresholds: { lines: 70, functions: 70, branches: 70, statements: 70 }
    }
  }
});
