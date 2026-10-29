# Animation timing study

[繁體中文](animation-timing.md) · **English**

Study date: **2026-10-26** · Compiled: **2026-10-28**

## Findings

Output frame rate, the duration of an exposure, and displacement between poses are different quantities. Changing a render from 24 to 30 fps does not create missing contact, release, or withdrawal actions.

| 24 fps timebase | Frames per exposure | Exposures per second |
| --- | ---: | ---: |
| On ones | 1 | 24 |
| On twos | 2 | 12 |
| On threes | 3 | 8 |

Exposure count is not the number of unique new drawings; drawings may be reused.

CELSYS demonstrates how changing in-between spacing and exposure timing affects motion. [Primary tutorial](https://howto.clip-studio.com/library/page/view/retasstudio_anime2_00_005).

Animation Mentor's slow-in/slow-out lesson provides additional context for reading motion. [Tutorial](https://www.animationmentor.com/blog/slow-in-and-slow-out-the-12-basic-principles-of-animation/).

## Local diagnostic result

The inspected version was 66.7 seconds at 30 fps, with 2,001 frames. Its timeline contained 52 segments, including 44 holds; the median hold was 0.85 seconds and the longest was 7 seconds. These numbers describe that version's timing, not a universal quality score.

For placing a bowl on a table, inspect: approach → contact → brief load transfer → release → withdrawal. Check the hand, bowl, table contact, and occlusion at each stage. A dissolve changes the image transition; it does not establish a physical handoff.

## Verification limits

The result is a diagnostic method, timeline analysis, and review criteria. At the original study cutoff, improved motion had not yet been accepted. Consult the catalogue for each later video or experimental cut; completing a method does not establish that every motion defect is fixed.

[Back to the lab](../README_EN.md)
