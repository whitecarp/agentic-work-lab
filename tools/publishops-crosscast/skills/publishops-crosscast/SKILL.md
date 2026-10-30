---
name: publishops-crosscast
description: Operate PublishOps CrossCast through the vpub CLI for multi-platform video publishing, metadata, comments, collections, metrics and resumable operations. Use for CrossCast requests involving Bilibili, Douyin, YouTube, Instagram or X; not general browsing or unrelated account actions.
---

# PublishOps CrossCast

Use the installed `vpub` CLI. Read `vpub --help`, `vpub capabilities`, and the relevant subcommand's `--help` before constructing unfamiliar commands. If the command is missing, use the installation instructions in the user's CrossCast checkout; do not invent installer URLs or claim a successful installation without running the CLI.

## Login and execution

- Reuse CrossCast's own saved account sessions. Bilibili delegates to bili-cli; YouTube uses OAuth and the official API; Douyin, Instagram and X use dedicated browser profiles controlled by the CLI.
- First login, expired sessions and platform identity challenges require the user to complete the official login. Do not request passwords, PINs, cookies or authorization codes in chat. Do not import the host browser's credentials.
- X publishing requires both a subscription and actual Media Studio access. TikTok is not supported. Check reported capabilities and account-specific limits before an operation.
- Execute only the requested actions, preserving existing visibility, schedules and unrelated content. Existing user authorization persists; do not add a redundant review step. Ask only when a required target or scope is missing.

## Content rules

- Follow the video's source language, separately from the documentation language. Bilibili/Douyin: Chinese content gets Simplified Chinese; other languages get the original language plus Simplified Chinese. YouTube/Instagram/X: English content gets English; other languages get the original language plus English.
- Replies use only the commenter's language. Reports use the user's language, normally Chinese or English. Generate metadata from actual source materials; do not invent events, claims, responses or attribution.
- Use the production cover for the correct episode. Douyin and Instagram share the approved portrait cover; respect safe areas. Preserve Instagram's original video aspect ratio; do not insert a cover frame or force a portrait canvas. Explain any remaining mobile cover adjustment.
- Gmail notifications are optional, off by default, and require an explicit user request and a connected Gmail tool. Do not substitute Outlook, SMTP or SMS.

## Receipts and verification

- Use exact platform/work/comment/collection IDs from reads. A matching title alone is insufficient. Distinguish private saved collections from public work series.
- Use stable request IDs when provided by the command. After interruption, reuse the original request or its read-only verify/resume command. Never blindly repeat a publish, comment, message, delete or collection mutation.
- Treat `UNVERIFIED`, partial reads, pending platform review and test fixtures honestly. A successful click or zero exit code alone is not proof of a persisted result. Read back through the CLI and retain the receipt.
- Do not clear saved lists, delete videos, contact unrelated accounts or enable recurring operations merely to test a feature. Controlled live tests must fit the user's authorized scope and restore their temporary changes.
- Keep credentials, private messages, raw client logs and account-specific diagnostics out of public GitHub commits. Report meaningful outcomes and remaining limits with verified links.

The Codex client chooses its progress wording. The skill's display name is PublishOps CrossCast; do not promise an exact progress sentence or a custom client UI.
