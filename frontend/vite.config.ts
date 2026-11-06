import path from "node:path";
import tailwindcss from "@tailwindcss/vite";
import react from "@vitejs/plugin-react";
import { configDefaults, defineConfig } from "vitest/config";

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      "@": path.resolve(import.meta.dirname, "./src"),
    },
  },
  server: {
    proxy: {
      // The browser scenario points it at a backend of its own: playwright.config.ts.
      "/api": process.env.CALENDAR_API_URL ?? "http://localhost:5284",
    },
  },
  test: {
    environment: "jsdom",
    // Playwright runs these: mise run test:e2e.
    exclude: [...configDefaults.exclude, "e2e/**"],
  },
});
