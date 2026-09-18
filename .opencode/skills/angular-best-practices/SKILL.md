---
name: angular-best-practices
description: Angular conventions and quality rules for SplitIt. Use when writing or reviewing components, services, guards, interceptors, pipes, templates, RxJS streams, routing, forms, i18n or state in split-it-ui/.
---

# Angular best practices (SplitIt)

Applies to `split-it-ui/`. Angular 19 today, upgrading to 20 LTS (target). Standalone components, Angular Material + SCSS + Bootstrap, ngx-translate, Karma/Jasmine.

## Components
- Standalone components and functional guards/interceptors; import what the template uses.
- One component per folder: `.ts` + `.html` + `.scss` (+ `.spec.ts`).
- `@Input()` must be typed. Never `any` for domain data — use the interfaces in `src/app/models/*.model.ts`.
- Match the DI style of the file you touch (`inject()` in functional contexts; constructor DI is the existing norm in classes).
- Prefer `OnPush` change detection for new presentational components.

## Templates
- Prefer built-in control flow (`@if`, `@for`, `@switch`) for new code; always add `track` to `@for`. Legacy `*ngIf`/`*ngFor` is acceptable in existing files.
- No business logic in templates — move it to typed getters/methods.
- Never bind untrusted HTML (`[innerHTML]`, `bypassSecurityTrust*`) without sanitizing.
- Show inline validation errors on forms.
- **Never hardcode user-facing strings** — use the ngx-translate dictionaries (see `i18n` skill).

## Services & HTTP
- Components never call `HttpClient` directly — go through a service under `src/app/modules/*/services` or `src/app/shared/services`.
- Every service method returns a typed `Observable<T>` using a model interface.
- Build URLs from `environment.apiUrl`, never hardcode hosts.
- Cross-cutting behavior goes in the interceptors: `auth.interceptor.ts` (attach token, clear session on 401) and `error.interceptor.ts` (surface failures). Keep their order correct in `app.config.ts`.

## Errors & UX
- Surface failures with the existing notification/SweetAlert2 service; never leave a request unhandled.
- Guard async UI with an `isLoading` flag and render a loading state.
- No `console.log`/`console.error` as the only handling.

## RxJS
- `subscribe()` inside components is the current pattern, but **always** unsubscribe (`takeUntilDestroyed(this.destroyRef)` preferred, or `ngOnDestroy` + `Subject`).
- Prefer the `async` pipe for streams you don't need to mutate.
- Use `catchError`/`finalize` instead of nesting error logic; never `subscribe()` inside another `subscribe()` — use `switchMap`/`concatMap`.

## Routing
- Lazy-load pages. Functional guards only.
- Protect authenticated areas with `auth.guard.ts`; admin area with `admin.guard.ts`.
- Keep defensive fallbacks in the route tree.

## Forms
- Reactive forms (`FormBuilder` + `Validators`) with inline error messages for anything with more than a field or two.
- Validate on the client for UX, but never assume it replaces server-side validation.

## i18n
- `split-it-ui/src/assets/i18n/en.json` and `es.json` must stay in sync — same keys, same nesting, same `{{placeholders}}`. See the `i18n` skill.

## Tests
- Karma + Jasmine, colocated `.spec.ts` next to the unit. Run `npm run test:ui` from the repo root (`test:ci` uses `ChromeHeadlessNoSandbox`).
- Test success **and** error paths for anything hitting a service.

## Version discipline
- Do **not** change `@angular/*` or TypeScript versions as part of a feature change — the upgrade is a dedicated phase.
- SSR scaffolding (`app.config.server.ts`, `app.routes.server.ts`, `@angular/ssr`) exists but the build does not use it; don't wire it up casually.
- Bootstrap + Angular Material coexist — don't add a third UI system.

## Review checklist
- [ ] Standalone, typed inputs, no `any` for domain data.
- [ ] HTTP only via typed services; errors handled centrally.
- [ ] Subscriptions unsubscribed; no nested subscribes.
- [ ] Lazy routes + guards preserved; new protected routes guarded.
- [ ] Inline validation errors; all copy in the i18n dictionaries (EN + ES).
- [ ] Colocated spec covering the new/changed behavior; `npm run test:ui` green.
