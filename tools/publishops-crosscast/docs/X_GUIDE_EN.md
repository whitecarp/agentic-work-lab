# X (Twitter) adapter

[繁體中文](X_GUIDE.md) · **English**

This release provides **in-app-browser handoff**: sign-in URL, one post body, account checks, media snapshots, single submission and receipt validation. A host agent must operate the official site. One X publication has now been read back in the signed-in UI; a standalone API publisher and full-playback acceptance remain incomplete.

**Live test:** Ready media, English text and AI disclosure survived a saved-draft reopen. The user then explicitly authorized publication; the same draft was submitted and the original status read back. It was not deleted. Self-only tests must use draft-only mode, which forbids final submission until explicit authorization changes the mode.

```sh
vpub onboard --platforms x
vpub init --video final.mp4 --out x-publish.json --platforms x
# Review identity and text
vpub plan x-publish.json
vpub publish x-publish.json --confirm CURRENT_PLAN_HASH
```

Set `expected_identity` to the handle visible in the signed-in profile, without `@`. Confirm the account in the same browser before `browser begin`; let the user choose the sign-in method.

- **Text:** `plan.platforms.x.post_text` combines title, description and hashtags. `ai_generated: true` adds an AI disclosure. Use the complete generated `post_text` without silently rewriting or truncating it.
- **Audience:** Account/current UI audience applies; this is not an individual private-video switch and does not change protected-account settings.
- **Limits:** Check the website's weighted character counter, media validation and account limits; do not treat Python string length as X's counter. Inspect duration/size/format first, stop on mismatch, and do not silently trim or assume paid privileges.
- **Submit:** Wait for processing, check exact text/audience, submit once; inspect the original post after an uncertain result.
- **Receipt:** Read back the status URL, numeric ID, original handle, exact `post_text` and source fields. Validation rejects a different author, mismatched ID or altered disclosure/text.

X's web-upload guide currently lists 140 seconds and 512 MB for non-subscribers, with different paid limits; validate the actual account UI as well. [Official source](https://help.x.com/en/using-x/x-videos) (checked 2026-10-29).

Discovery, work listings, analytics, comments and inbox are not unified X commands yet. Do not describe this handoff as a full X CLI.

## Custom thumbnails and subscription

**Custom thumbnails use subscription-gated Media Studio.** Official guidance limits Studio access to verified subscribers. Its library offers Change thumbnail with a video frame or uploaded image matching the video aspect ratio. Ordinary post composition is a different surface; payment alone does not prove that the library is ready.

[Official access](https://help.x.com/en/using-x/media-studio) · [Official thumbnail guidance](https://help.x.com/en/using-x/media-studio-faqs) (checked 2026-10-29).

**Tier:** Media Studio is included with Premium and Premium+, not Basic. [Official tier comparison](https://help.x.com/en/using-x/x-premium).

**Follow-up test, 2026-10-29:**

The library initially failed to initialize. A single direct Upload Media attempt succeeded and exposed media details. Change thumbnail → upload accepted the matching 16:9 production cover; artwork and metadata survived closing/reopening. Newly authorized Studio publication produced a new status with the original cover. The previous status was retained. This is host-UI validation, not a standalone Studio API or dedicated CLI cover-receipt command.

Workflow: verify account/media → upload or select existing media → Change thumbnail → upload same-ratio artwork → read back → Post when authorized → verify the status URL. Library media IDs differ from post IDs. Uploading media is not publication and does not replace an earlier post.

This Studio post retains the AI-generated text disclosure, but the native Made with AI badge was not observed. Text disclosure is not proof of the native label. Full playback and mobile presentation remain separate checks.

Related links: [Draft procedure](DRAFT_TESTING_EN.md)

[Back to the lab](../../../README_EN.md)
