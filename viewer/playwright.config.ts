import { defineConfig, devices } from '@playwright/test';

const PORT = 4317;

// `e2e:full-stack` runs the full-stack project alone, against the published `dydo map` binary rather
// than vite preview. Workers re-read this config without the runner's argv, so the choice travels
// through the environment they inherit.
if (process.argv.includes('--project=full-stack')) process.env['DYDO_E2E_FULL_STACK'] = '1';
const fullStack = process.env['DYDO_E2E_FULL_STACK'] === '1';
const chrome = { ...devices['Desktop Chrome'], viewport: { width: 1600, height: 1000 } };

export default defineConfig({
  testDir: 'e2e',
  fullyParallel: true,
  forbidOnly: Boolean(process.env['CI']),
  reporter: [['list']],
  // ELK lays out the 161-issue fixture in 3-5 s on an idle machine and far slower under parallel
  // workers; the default 5 s wait flaked. Budget each wait, and each test, for the loaded case.
  timeout: 60_000,
  expect: { timeout: 30_000 },
  use: {
    baseURL: `http://localhost:${String(PORT)}`,
    trace: 'retain-on-failure',
  },
  projects: fullStack
    ? [{ name: 'full-stack', testMatch: 'full-stack.spec.ts', use: chrome }]
    : [{ name: 'chromium', testIgnore: 'full-stack.spec.ts', use: chrome }],
  // The e2e run proves the built bundle, the same bytes `dydo map` embeds.
  ...(fullStack
    ? {}
    : {
        webServer: {
          command: `pnpm run build && pnpm exec vite preview --port ${String(PORT)} --strictPort`,
          url: `http://localhost:${String(PORT)}`,
          reuseExistingServer: !process.env['CI'],
          timeout: 120_000,
        },
      }),
});
