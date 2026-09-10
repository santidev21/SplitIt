---
name: run-e2e
description: Run the SplitIt Playwright e2e suite. Use when running or debugging end-to-end tests.
---

# Playwright e2e

From `split-it-ui/`:

```bash
npx playwright install chromium
npx playwright test
```

The backend must be reachable for the full-stack suite (see `playwright.fullstack.config.ts`).
