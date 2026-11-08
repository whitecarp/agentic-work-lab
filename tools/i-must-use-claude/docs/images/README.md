# README visuals

These assets illustrate the public project. They contain no private conversations, account data, credentials, or configuration backups.

| Asset | Type and provenance |
| --- | --- |
| `hero.png` | v0.4.0 banner, composed in HTML and rendered with Chromium: the right-hand part of the earlier generated hero artwork (see the prompt below) as a framed sticker, live lettering for the title and tagline, and the four apps' own icons (`apps/*/icon.ico`, the helper mascot and the clock whale-girl). The previous three-tool banner remains in Git history. |
| `hero-zh.png` | Chinese edition of the same banner for the Chinese README and the project page's social preview: identical artwork, icons and layout, with Chinese lettering set in Noto Sans SC (Google Fonts) and rendered with Chromium. |
| `translator-preview.svg` | Editable, code-authored workflow illustration with invented example text; not an app screenshot or a live translation result. |
| `clock-preview.svg` | Editable, code-authored workflow illustration with example times. |
| `helper-preview.svg` | Editable, code-authored workflow illustration of version 0.3.0. It assumes no measured score or successful repair. |
| `helper-preview-zh.svg`, `clock-preview-zh.svg` | Chinese editions of the two illustrations for the Chinese README. Same layout; button and option names match the apps' Chinese interface. |
| `translator-compose-zh.png`, `translator-compose-en.png`, `translator-offline-zh.png`, `translator-offline-en.png`, `translator-offline-dialog-zh.png`, `translator-offline-dialog-en.png`, `translator-glossary-zh.png`, `translator-glossary-en.png` | Translator 0.9.6 rendered offscreen at 96 DPI on a GitHub-hosted Windows runner (build run 41556180523, `translator-regression-and-previews`, `native-100`), copied unchanged. Compose, offline-retry and retry-dialog images are the app's own fixed-sample RenderPreview output; the glossary editor shows an excerpt of the built-in terms. Nothing was translated online or sent. |
| `clock-ui.png` | Existing compact clock screenshot; displayed dates and times are examples. |
| `site-hero.webp` | Right-hand crop of the earlier generated hero artwork (the illustration without the title lettering), resized for the project page. |
| `site-helper-mascot.webp` | Downscaled copy of the Environment Helper's mascot (`apps/whale-claude-helper/assets/helper-mascot.png`) for the project page. |
| `site-pack-whale.webp` | Copy of the Pack Helper's whale (`apps/whale-pack-helper/assets/whale.png`) with the flat white backdrop made transparent, for the project page. |
| `site-clock-girl.webp` | Downscaled copy of the clock's existing whale-girl artwork (`apps/two-zone-clock/assets/whalegirl.png`) for the project page's clock demo. |
| `helper-maintenance-en.png`, `helper-maintenance-zh.png` | 0.4.0 maintenance controls rendered offscreen with static demo data; no app window shown, real cache scanned, network probe sent, or settings changed. |

The existing screenshots were copied without alterations. Their inclusion does not represent new app execution, a new translation test, or a system-settings test.

## Hero illustration prompt

This prompt produced the original banner whose illustration is reused in the current `hero.png` and in `site-hero.webp`.

Built-in tool; opaque background; the existing clock whale-girl image was supplied as a reference.

> Use case: ads-marketing. Create a polished horizontal GitHub README hero banner for a community Windows utility suite titled exactly 'I MUST USE CLAUDE'. Reference image is character identity and illustration style reference only: keep the cute blue-haired whale-girl's recognizable blue hair, tiny blue hair bow, white frilled maid headband and smiling friendly face. This is a new banner, do not reproduce the clock icon composition. Landscape ratio about 2.7:1, high resolution. Editorial composition: left half is crisp large dark navy typographic title, broken into 'I MUST USE' then 'CLAUDE', with small line 'THREE LITTLE TOOLS. ONE FRIENDLIER WORKSPACE.' beneath. Right half a charming chibi whale-girl at a little laptop, a cute plump sky-blue whale mascot beside her, floating speech bubbles and a small sun-and-moon clock and a small settings-check symbol subtly integrated. Palette warm cream background, powder blue, deep ink navy, restrained coral-peach highlights, soft watercolor paper texture, clean outlines, gentle shadows, tiny sparkles, premium friendly kawaii stationery aesthetic. Keep title very legible even at README width. Do not include fake app screenshots, numerical scores, guarantees, private text, official brand logos, watermarks, or big empty panels. Image bleeds naturally to edges, no thick frame.
