---
name: frontend-test
description: Build and run the SplitIt Angular unit tests. Use when running frontend tests, npm test, karma, ChromeHeadless, Angular build, or checking frontend coverage.
---

# Frontend tests (Angular 19)

Run from `split-it-ui/`:

```bash
npm install --legacy-peer-deps
npm test            # single run, ChromeHeadlessNoSandbox
npm run test:ci     # single run with coverage (same as CI)
npm run build -- --configuration production
```

Rules:
- Always install with `--legacy-peer-deps`.
- Karma runs on `ChromeHeadlessNoSandbox` (`karma.conf.js`); no watch in CI.
- A new component/service should ship with its `.spec.ts`. CI enforces coverage — a missing spec breaks the build.
- User-facing strings go through the EN/ES dictionaries (`src/assets/i18n/en.json`, `es.json`), never hardcoded (see `i18n`).
