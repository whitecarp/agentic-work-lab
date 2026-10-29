# Editable video delivery

[繁體中文](editable-video-workflow.md) · **English**

Compiled: **2026-10-28**

## Workflow

1. **Lock narration first.** Keep selected voice takes and time the picture to actual audio durations.
2. **Use one timing reference.** Distinguish global and scene-local time when reusing clips.
3. **Document baked audio.** Avoid applying gain or effects twice when reopening a project.
4. **Pin tools and dependencies.** Check, preview, and render with the documented HyperFrames version and local dependencies.
5. **Review the actual output.** Record decode checks, sampled frames, audio checks, human review, and SHA-256.

## Two delivery examples

| Property | Whale Girl 02 | Whale Girl 03 |
| --- | ---: | ---: |
| Duration | 116.7 s | 188.8 s |
| Picture | 1920 × 1080, 30 fps | 1920 × 1080, 30 fps |
| Frames | 3,501 | 5,664 |
| Baked audio components | 38 | 47 |
| Selected unmixed voice takes | 13 | 28 |

Revision 3 of episode 02 corrected the AI-declaration highlight in scene 5. Episode 03's delivered file is byte-identical to the full sound preview accepted by the human reviewer, confirmed by a hash comparison after copying.

Episode 03 passed full decoding and targeted frame checks. Lint/runtime/layout error counts were zero, while warnings and informational messages remained. The agent did not watch and listen to the entire video at normal speed, so machine checks are not presented as word-by-word human review.

## Reusable delivery information

Use the [delivery manifest](../templates/delivery-manifest.json) to record filenames, hashes, formats, dependency versions, review methods, unresolved issues, and publication status. Use `null` or `not_run` for unfinished checks rather than pre-filling success.

[Back to the lab](../README_EN.md)
