# AI Memory Keeper Web App (v0.2.1)

[繁體中文](README.md) · **English** · [CLI & Hook Suite](../README_EN.md) · [Back to the lab](../../../README_EN.md)

**中文简介：** AI记忆守护者是一个静态网页。主人用自己的 Gemini API 密钥，把 AI 宠物的聊天记录整理成一份记忆档案（主人是谁、性格和说话方式、昵称、梗、约定、时间线、喜欢和讨厌）和一段唤醒语，可以导出 Markdown 和 JSON。界面语言跟随浏览器：中文浏览器显示中文，其他显示英文，可以手动切换。每条记忆是一颗星星，整理完成后连成双子座。角色设计参考 @ZipZipPipe的 Gemini 拟人形象（非 Google 官方角色）。聊天记录和密钥不经过我们的服务器。运行、部署和测试情况见下文（英文）。

A static web page that reads an AI pet's chat logs with the owner's own Gemini API key and writes a memory sheet (owner, personality and way of talking, nicknames, in-jokes, promises, timeline, likes and dislikes) plus a short wake-up note for a fresh session. Export as Markdown or JSON. The product is for users worldwide: the UI follows the browser's language (Chinese for Chinese browsers, English for everyone else), with a toggle.

Names, chosen by the user on 11-04: **AI Memory Keeper** in English and **AI记忆守护者** in Chinese. "Gemini Saver" and 大肥鱼记忆保管箱 are retired, and Google's brand rules don't allow "Gemini" inside a product name anyway (see the end of this file). Each language's name is one constant, `appName` in `public/i18n.js`; the export credit line is `md.made` there, and `index.html` has the same names in `<title>`, `<h1>` and the share tags.

## Files

| Path | What it is |
| --- | --- |
| `public/index.html` | The page, with a CSP that only allows requests to `generativelanguage.googleapis.com` |
| `public/app.js` | Page wiring: inputs, run, results, downloads |
| `public/core.js` | Logic with no DOM: token estimate, chunking, JSON import, prompts, Gemini call, merge, export |
| `public/i18n.js` | All Chinese and English strings, including the privacy note |
| `public/demo.js` | `?demo=1`: a made-up chat and a local stand-in for Gemini, no network, no key |
| `public/sky.js` | The memory sky: each memory item is a round star; the finished sheet joins them into the Twins constellation |
| `public/assets/art/*.png` | The user's Codex art, kept as masters (about 2 MB each, left out of the deploy) |
| `public/assets/art/web/` | Web copies the page uses: WebP images plus 64 and 32 px PNG favicons |
| `firebase.json`, `.firebaserc` | Firebase Hosting config for project `gemini-saver-4146a` (same CSP as a header) |
| `dev-server.mjs` | Local server for `public/` that sends the headers from `firebase.json` |
| `test/core.test.mjs` | Node tests for the logic and for export from a stubbed Gemini response |

No build step and no dependencies. Needs Node 18+ only for the dev server and tests.

## Run it locally

```bash
node dev-server.mjs
```

Then open http://127.0.0.1:5178/. The language follows the browser; add `?lang=zh` or `?lang=en` to force one. Add `?demo=1` to try the whole flow with a sample chat and no key. In this Claude workspace the launch config `memory-keeper` runs the same server.

Tests:

```bash
node --test test/
```

## How it works

1. Pasted text and files (.txt / .md / .json / .log) are combined. JSON exports are turned into `[time] role: text` lines when they look like chats or memory lists; anything else is sent as JSON.
2. The text is split into parts of at most the chosen size (default 200,000 estimated tokens, counted high: 1 per CJK character, 1 per 3 other characters). Small logs go in one request.
3. Each part goes to `POST https://generativelanguage.googleapis.com/v1beta/interactions` with `store: false`, a JSON schema in `response_format`, and `thinking_level` low (parts) or medium (final). Then the part notes are merged, in rounds if they don't fit in one request, and the last merge writes the wake-up note. The prompts tell Gemini to record only what the logs say, leave out phone numbers and other identifiers, and never guess anyone's gender: unless the logs state it, people are named or called TA (Chinese) or they (English). The gender rule was added 11-04, after the user's first real run assumed the owner was male.
4. Busy answers (500/503) and per-minute limits (429) are retried 3 times, after about 5, 15 and 30 seconds (or Google's `retryDelay` when it gives one), with 「Gemini API 正忙，xx 秒后重试」 on screen. If that still fails, a plain message suggests trying later or switching to the other free model. Parts already read are kept in page memory, so clicking again only reads what's missing. A daily-quota 429 stops at once. This was added in v0.2.1 (11-04), after the user's live run hit "gemini-3.8-flash is currently experiencing high demand" (503).
5. If Google refuses because of the user's region, the page says so and links Google's official list of supported regions. Google's docs don't publish the exact wording of that refusal, so `core.js` matches the usual phrasings (`REGION`); anything else shows Google's own message.

## Look, art and credit

- **Night sky:** deep indigo, with a blue-violet-pink accent taken from the guardian's hair. Each memory item is a round star; while Gemini reads, stars appear part by part, and the finished sheet joins them into the Twins (双子座), drawn from real star positions in `sky.js`. Hovering a star shows its memory.
- **Staying clear of Google's look:** Google's trademark guidelines bar copying its trade dress ("color combinations… product icons") or anything "confusingly similar". So the stars are round, never the 4-point sparkle of Google's Gemini mark; there's no Google font; and the gradient stays an accent.
- **Art slots,** each falling back to a CSS gradient if its file is missing: `hero` (header; title over the open sky on wide screens, below it on phones), `avatar` (next to the title and the favicon), `empty` (step 1 until something is added), `loading` (while reading), `done` (next to the finished constellation).
- **Rebuilding the web copies** after the masters change: the Codex runtime's Python has Pillow with WebP. The script used on 11-04 sized them to hero 1280 wide, empty and done 960, loading 512 with alpha, avatar 512, plus 64 and 32 px PNGs. `firebase.json` ignores `assets/art/*.png`, so only the web copies deploy. On 11-05, `overlay.webp` (480×600, with alpha) was added the same way for the auto-mode illustration, made from the CLI's overlay art `cli/assets/gemini-overlay.png`.
- **Credit,** in the footer and here, the user's wording: 「角色设计参考 @ZipZipPipe的 Gemini 拟人形象（非 Google 官方角色）」, with @ZipZipPipe linking to https://space.bilibili.com/4168597. The page doesn't call the character by name, because the guidelines also bar Google trademarks in names and monikers.

Why no `Api-Revision` header: Google's docs show it, but Google's servers reject it in the browser's CORS preflight (checked 11-04: preflight with it → 403; without it → 200). The docs say the header has been ignored since 2026-06-08, when the old `outputs` schema was removed, so the page reads the current `steps` schema.

## Deploy to Firebase Hosting (live since 11-04)

Live at https://ai-memory-keeper.web.app (primary site, created 11-06; legacy https://gemini-saver-4146a.web.app also updated). Each deploy had the user's approval ("Yes, deploy"):
- **11-04:** v0.2.0, then v0.2.1 (retries on 503/429).
- **11-05 09:07 Beijing:** the auto-mode section.
  - The page's `.auto-mode` section with the `auto*` strings in `i18n.js`, taken from `cli/WEBSITE_COPY.md`. Antigravity wrote the copy.
  - Claude fixed two lines: Codex is marked as untested, and the free-tier line quotes Google's rule.
  - The app version stays 0.2.1, because `core.js` didn't change.
  - Checked live afterwards: the files match the local ones byte for byte, both languages show with no console errors, and the masters return 404.
- **Not deployed (11-05, about 09:40 Beijing): Claude's redesign of the auto-mode section.** Under `ops/PROTOCOL.md` v5 (about 10:00), design is Claude's again, so this is the design. The wording Claude wrote has gone to Antigravity for approval (task `copy-memory-keeper-site-001`, brief `ops/briefs/2026-11-05-memory-keeper-site-copy.md`). The deploy waits for the user's word. A snapshot is in `design-proposals/claude-20261105/`.
  - **Built from the page's own parts:**
    - The steps are a short constellation: round numbered stars joined by a thin line, as in the memory sky.
    - The two features are tiles marked with round stars, replacing the 4-point ✦ bullets.
    - The privacy line uses the amber box from the top of the page.
  - **An illustration**, captioned as one, replaces the reused "done" picture: an agent window running `/compact`, with the guardian's overlay cut-out (`assets/art/web/overlay.webp`) and her line in a bubble.
  - **The title** no longer repeats the badge.
  - **Naming:** the 09:07 version named the character (Gemini娘 / Gemini-chan) on the page, against the naming rule above. This version calls her 守护者 / "the guardian", and her bubble speaks in the first person, like the rest of the page.
  - **Strings:** `autoExtras` items are now `{ title, body }`.
- **Also not deployed (11-05, about 09:48 Beijing): Claude's proposal for step 1's empty state,** made after the user said it looked "too empty". Its one new line (`emptySky`) is in the same copy brief. Both redesigns sit in `public/`, so any deploy would publish them.
  - `#empty-art` is now a two-column welcome: the art on the left; on the right, her line (`emptyLine`) in a speech bubble.
  - Under the bubble is the Twins drawn dim: the 16 stars and 15 lines from `sky.js`'s layout, as static SVG so its gradient ids can't clash with the result sky. It has a one-line caption (`emptySky`).
  - The paste box and the file bar are one panel (`.input-panel`). The box starts shorter (7 rows) and shows faint stars only while empty (`:placeholder-shown`).
  - On phones, the art and bubble stack and the dim sky is left out. The welcome still hides once there is input.

Firebase uploads 14 files; the PNG masters are ignored.

In this Claude workspace, deploy from the Terminal panel, because the sandboxed shell can't reach Google's deploy APIs (`serviceusage.googleapis.com` failed there). On this PC, PowerShell's execution policy blocks `firebase.ps1`, so type `firebase.cmd` instead of `firebase`. Start the tab in this `app/` folder, because a new tab opens in the repo root.

Needs the user's go-ahead, and the user approves the login in their own browser.

```bash
npm install -g firebase-tools
```

```bash
firebase login
```

```bash
firebase deploy --only hosting --project gemini-saver-4146a
```

Run the last two from this `app/` folder. The site then lives at https://ai-memory-keeper.web.app (`"site": "ai-memory-keeper"` in `firebase.json`).

## Verified, and not yet

Verified on 11-04 in the built-in browser against the local server:
- Chinese and English UI and the language switch, at desktop and phone width (375 px, no sideways scroll), light and dark.
- Pasting, file import by drop (.txt with BOM and CRLF, .json export, a .png rejected), removing a file, live size and part count.
- Demo run: 2 parts read, merged once, the later part updates a promise to kept; wake-up note and every section render; copy button.
- Markdown and JSON exports (captured from the page; content, file names and types checked).
- CSP: a request to any other host is blocked in the page.
- A request from the page to Google with a fake key: CORS passes and Google's own error ("API key not valid") is shown.
- v0.2.0, also 11-04: all five art files load in place; the header text sits over the sky (desktop) or below the image (375 px phone, no sideways scroll); stars appear while reading (10 after the first demo part) and the finished demo draws 15 stars and 14 lines with hover text; language follows the browser (zh-CN here) and `?lang=en` switches; the credit line and its link show in both languages; no console errors.
- 21 Node tests, the 17 below plus language choice, region refusal, memory stars and the constellation, and per-part progress: chunk limits and order, giant lines, JSON import shapes, request shape (key header, `store: false`, no `Api-Revision`), retry on 429 with `retryDelay`, stops on daily quota / 400 / cut-off / bad JSON, one-request and multi-part pipelines, merge rounds, export in both languages from a stubbed Interactions response.

Not verified yet:
- A real region refusal. The wording match in `REGION` is a guess, since Google doesn't document it.
- **A multi-part run with a real key, and the default `gemini-3.8-flash`.** The user's own-key test on 11-04 worked end to end: a made-up chat, `gemini-3.6-flash`, one request, and an accurate sheet. The read-in-parts-and-merge path has only run against stubs, and the gender rule hasn't had a real run yet.
- Free-tier tokens-per-minute for each model. Google shows these per project in AI Studio, not in the docs, so the 200k default part size is a guess. If the first real run hits a 429, use a smaller size.
- A real save to disk of the downloads (the test read the files from the page instead).
- DeepSeek Harness (dsh) memory export format. Not looked up; dsh exports are handled by the generic JSON and plain-text import.
- The live page with a real key. Checked live on 11-04 without a key: both languages, all five images, the credit line, the CSP and other headers, masters returning 404, and no console errors.
- Real phones and Safari / Firefox.

## Google's rules that matter here (official pages, read 11-04)

From the [Gemini API Additional Terms](https://aistudio.google.com/docs/terms) (effective 2026-03-23):
- **Free tier:** Google uses submitted content and responses "to provide, improve, and develop Google products and services and machine learning technologies"; human reviewers may read, annotate and process input and output, after Google disconnects it from the account, key and project; "Do not submit sensitive, confidential, or personal information to the Unpaid Services."
- **Paid (billing on):** prompts and responses aren't used to improve products; they are logged for a limited time only to detect Prohibited Use Policy violations.
- **EEA, Switzerland, UK:** only Paid Services when making API clients available to users there; the paid data terms apply to free quota too.
- **Age:** users must be 18+, and the API may not be used in an app "directed towards or is likely to be accessed by individuals under the age of 18."
- **Purpose:** "for developers building with Google AI models for professional or business purposes, not for consumer use."
- **Regions:** only in the [available regions](https://aistudio.google.com/docs/available-regions); mainland China and Hong Kong are not listed.

The privacy note in `i18n.js` follows the first three. The age, region and EEA rules are shown next to the key field, with an 18+ checkbox before running.

Brand rules ([Google trademark guidelines](https://partnermarketinghub.withgoogle.com/brands/google/trademarks-and-terms/trademark-guidelines-for-proper-usage/), [product co-branding](https://partnermarketinghub.withgoogle.com/brands/google/use-cases/product-co-branding/), both reached from about.google's Brand Resource Center):
- Not allowed: putting Google brand features into "your own product names, service names, trademarks, logos, or company names"; registering Google trademarks "as second- or third-level domain names"; using them in social media account names; implying a relationship with, sponsorship by or endorsement by Google.
- Allowed: saying in plain text that the product is built with a Google API, in specific wording that doesn't overstate the relationship ("works with", "integrates with" are their examples), plus a legal line in the footer when using their trademarks. The [Google APIs Terms](https://developers.google.com/terms) (section 6) license Google's brand features only "for the purpose of promoting or advertising that you use the APIs", under these guidelines, and bar anything that "suggests partnership with, sponsorship by, or endorsement by Google".
- How to word it: the guidelines say to use a trademark "only as an adjective – never as a noun or verb, and never in the plural or possessive form", followed by the generic term. So write "uses the Gemini API", "built with the Gemini API" or "works with the Gemini API". Avoid "Powered by Gemini", "uses Gemini" or "Gemini's" (the mark used as a noun or possessive), and words like official, partner or certified. Also don't make "Gemini" the most prominent thing on a page, cover or thumbnail. The page follows this: every mention is "Gemini API", plus the legal line "Gemini is a trademark of Google LLC" and a not-endorsed line in the footer.
- The name passes: "AI Memory Keeper" / AI记忆守护者 have no Google trademark in them.
- The address: on 11-06, the Hosting site was renamed to `https://ai-memory-keeper.web.app` (`firebase hosting:sites:create ai-memory-keeper` on project `gemini-saver-4146a`, with `"site": "ai-memory-keeper"` in `firebase.json`), so the URL matches the product name `ai-memory-keeper` and avoids any Google trademark in the third-level domain.
