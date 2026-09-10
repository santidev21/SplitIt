---
description: Run the SplitIt Playwright e2e suite.
---

Follow the `run-e2e` skill. From `split-it-ui/`:

```bash
npx playwright install chromium
npx playwright test
```

Full-stack suite (needs the backend reachable, see `playwright.fullstack.config.ts`):
```bash
npm run e2e:fullstack
```

Docker HTTPS suite:
```bash
npm run e2e:https
```

Extra input: $ARGUMENTS (e.g. a spec path to run a single file).

Do not fix failures unless asked — report which spec and which step failed.
