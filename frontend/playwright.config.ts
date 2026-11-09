import os from "node:os";
import path from "node:path";
import { defineConfig, devices } from "@playwright/test";

// Ports and a database of its own: the servers of `mise run dev` may be running meanwhile.
const backendUrl = "http://127.0.0.1:5384";
const frontendPort = 5374;
// The backend command removes what the previous run left, so every run starts empty.
const database = path.join(os.tmpdir(), "calendar-e2e.db");
// A deployed app or a running container: the scenario goes there and starts no servers.
const externalUrl = process.env.E2E_BASE_URL;

export default defineConfig({
  testDir: "e2e",
  forbidOnly: !!process.env.CI,
  workers: 1,
  reporter: process.env.CI ? "github" : "list",
  use: {
    baseURL: externalUrl ?? `http://127.0.0.1:${frontendPort}`,
    trace: "retain-on-failure",
  },
  projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"] } }],
  webServer: externalUrl
    ? undefined
    : [
        {
          name: "backend",
          command: `rm -f "${database}" "${database}-wal" "${database}-shm" && dotnet run --project ../backend/CalendarApi --urls ${backendUrl}`,
          env: { ConnectionStrings__Calendar: `Data Source=${database}` },
          url: `${backendUrl}/api/health`,
          // A cold build with restore takes about a minute.
          timeout: 120_000,
        },
        {
          name: "frontend",
          command: `npm run dev -- --strictPort --host 127.0.0.1 --port ${frontendPort}`,
          env: { CALENDAR_API_URL: backendUrl },
          url: `http://127.0.0.1:${frontendPort}`,
        },
      ],
});
