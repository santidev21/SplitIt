---
name: i18n
description: Keep the English/Spanish UI dictionaries in sync. Use when adding UI text, translating, en.json, es.json, ngx-translate, hardcoded strings, or fixing untranslated labels.
---

# EN/ES i18n

Dictionaries: `split-it-ui/src/assets/i18n/en.json` and `es.json` (ngx-translate).

Rules:
- Never hardcode a user-facing string in a template or component — add a key to both dictionaries.
- Every key in `en.json` must exist in `es.json` and vice versa. Same nesting, same interpolation placeholders (`{{name}}`).
- Watch for dynamic fragments: `"Paid by {{name}}"` style strings need the name localized too, not just the wrapper.
- After editing, verify with `npm run build -- --configuration production` from `split-it-ui/`.
