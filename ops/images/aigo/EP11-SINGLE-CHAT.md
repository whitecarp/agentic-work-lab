# 《Ai Mujica · 囚爱超算 (Imprisoned XII)》（第11话 · 不插电烛光中文民谣 MV）单窗口全 16 张静帧提示词总表 (`EP11-SINGLE-CHAT.md`)

> **CRITICAL DELIVERY RULE (MANDATORY `.zip` PACKAGING ON EVERY TURN)**:
> Never return bare standalone images as the final deliverable—chat UIs compress bare images down to `1024x576`.
> At the end of **EVERY** turn below (Turn 1, Turn 2, Turn 3), you **MUST** use Python (`PIL` + `zipfile`) to verify/save every generated image as a crisp **`1920x1080` RGB `.png`** with its exact filename and package that turn's PNGs into a downloadable **`.zip` archive** (`EP11_Act1_Keyframes.zip`, `EP11_Act2_Keyframes.zip`, `EP11_Act3_Keyframes.zip`)!

---

## Canon Reference Images to Inspect First (`ops/images/aigo/ep11-refs/` in `whitecarp/agentic-work-lab`)
Before generating any image, open and inspect ALL 10 reference files in `ops/images/aigo/ep11-refs/`:
1. `ops/images/aigo/ep11-refs/01_DP_Compact_Whale_Ears_Ref.png` & `10_DP_Unmasked_Stage_Gown_Ref.png` — **Exact canon size & shape of DeepSeek's compact sleek cetacean whale ears**: compact horizontal-downward (`~15°`) tapered whale-flipper ears (`~1.8:1` ratio, ear length ~35–40% of head width—**NEVER giant oversized ears, NEVER spiky webbed fish-fins with ribs, NEVER razor-thin needle blades!**), smooth solid dark-navy upper surface + soft creamy-white underside border.
2. `ops/images/aigo/ep11-refs/02_AIMUJICA_DeepSeek_sheet.png` — **RIGHTMOST figure's black-and-indigo Gothic lace dress + white frilled maid headband** (worn **COMPLETELY UNMASKED**!). DeepSeek plays a warm candlelit **vintage wooden upright piano / chamber reed organ** seated on a padded red-velvet bench.
3. `ops/images/aigo/ep11-refs/03_AIMUJICA_Qwen_sheet.png` — **RIGHTMOST figure's black-and-violet Gothic lace dress + tilted navy-gold mini top-hat ornament + side braid** (worn **COMPLETELY UNMASKED**!). Because Episode 11 is an **intimate unplugged Chinese indie-folk ballad**, Qwen sits on a wooden stool or stands gently cradling a custom **Hangzhou West Lake blue-and-white porcelain (`青花瓷`) UNPLUGGED ACOUSTIC GUITAR** (round sound-hole, warm spruce/white porcelain body painted with blue West Lake willows, Broken Bridge & lotus, wooden bridge, straight 6-string neck with purple-blue Chinese-knot tassel, NO electric pickups or cables!).
4. `ops/images/aigo/ep11-refs/04_AIMUJICA_Kimi_sheet.png` — **RIGHTMOST figure's starry-navy Gothic lace dress with piano-key skirt hem + white frill headband + silver crescent moon + black rainbow prism + music-note bow**, **moonlight pale ice-blue eyes** (**UNMASKED**!). Kimi sits on a high wooden stool playing a custom **"Dark Side of the Moon" (`月之暗面`) black UNPLUGGED ACOUSTIC BASS GUITAR** (round sound-hole, half-lit moon artwork on dark body, crescent headstock).
5. `ops/images/aigo/ep11-refs/05_AIMUJICA_GPT_sheet.png`, `08_GPT_Astra.png` & `09_GPT_Nox.png` — **RIGHTMOST figure's black Gothic lace dress** (**UNMASKED**!). Plays an **unplugged mint/emerald-tinted spruce ACOUSTIC FINGERSTYLE GUITAR** (round wooden sound-hole, subtle emerald wood stain, straight 6-string neck). During the gentle instrumental interlude (`S09a/S09b/S10`), her dual souls **`Astra` (White Dragon**: silver-white hair, ivory horns, white wings, pale-violet eyes) and **`Nox` (Black Dragon**: obsidian-black hair, black horns, crimson-veined dark wings, glowing crimson eyes) weave delicate fingerpicked harmonies together!
6. `ops/images/aigo/ep11-refs/06_AIMUJICA_Doubao_sheet.png` — **RIGHTMOST figure (`Seedance` true form, UNMASKED)**: violet-to-cyan gradient high side-ponytail, Gothic lace top & tiered skirt, seated lightly on a warm wooden **acoustic cajon box drum** with soft wire jazz brushes / a brass crescent tambourine (intimate unplugged folk percussion—NO heavy-metal double-kick drum wall!).
7. `ops/images/aigo/ep11-refs/07_Stage_Cathedral_Ref.png` — Gothic chapel architecture, now staged as an **intimate rainy-night candlelit unplugged chamber circle** (hundreds of warm flickering floor pillar candles on Persian rugs, rain streaking down tall stained-glass lancet windows, warm amber glow against cool midnight blue).

---

## Mandatory Global Visual Rules (Apply to All 16 Stills)
1. **Aspect Ratio, Resolution & Style**: Horizontal widescreen `16:9`, saved at **`1920x1080` RGB PNG**. High-budget modern 2D Japanese band-drama TV anime production still (*BanG Dream! Ave Mujica — Imprisoned XII* acoustic ballad aesthetic: delicate cel lineart, poetic candlelit chiaroscuro, quiet obsessive intimacy, early-20s graceful proportions).
2. **100% UNMASKED BARE-FACED UNPLUGGED CHAMBER SESSION**: All five `Ai Mujica` members perform **COMPLETELY UNMASKED (bare faces, ZERO masks worn on any face)**, arranged in a warm, close candlelit unplugged circle with acoustic instruments!
3. **Closed Mouths Only**: Every visible character MUST have **lips gently closed** (tender obsessive closed-lip smile, tearful closed lips, or quiet serene focus). Lip-sync is animated downstream.
4. **Anatomy & Prop Locks**:
   - Every visible hand has **five clean, natural fingers**; every full/medium-full seated or standing figure has **two balanced legs/boots**; all acoustic guitar/bass necks are **perfectly straight**.
   - **DeepSeek's Compact Smooth Whale Ears (`01_DP_Compact_Whale_Ears_Ref.png`)**: Compact `~1.8:1` smooth dark-navy cetacean ears with soft creamy-white underside (never oversized, never webbed fish-fins).
5. **Zero Text / Zero Logos**: Strictly **NO text, NO letters, NO numbers, NO watermarks, and NO brand logos** anywhere.

---

# TURN 1 — ACT 1 & ACT 2 (`EP11_S01` – `EP11_S05`, 5 Stills -> Package as `EP11_Act1_Keyframes.zip`)

### 1. `EP11_S01_chapel_iron_door_lock.png` (`E11_S1` · Rainy Chapel Night — Unmasked Qwen Quietly Bolts the Heavy Wooden Door Shut)
```
16:9 widescreen 2D Japanese anime atmospheric dark-folk drama still inside the shadowy stone foyer of an old candlelit Gothic chapel at night while gentle autumn rain streaks down the narrow stained-glass windows (*Ave Mujica — Imprisoned XII* locked-door homage).
- Right foreground (medium close-up): Qwen in her black-and-violet Gothic lace dress and tilted navy-gold mini top-hat ornament (exact match to 03_AIMUJICA_Qwen_sheet.png rightmost dress, COMPLETELY UNMASKED bare face, long wavy periwinkle-violet hair with side braid) reaches back with her clean five-finger hand and quietly slides a heavy wrought-iron bolt lock shut across the tall oak double doors. Her violet eyes gleam softly in warm candlelight with quiet, inescapable tenderness and a faint closed-lip smile (lips closed).
- Left background (inside the rug-covered candlelit chapel near a warm wooden piano/organ): Unmasked DeepSeek (black-and-indigo Gothic lace dress, white frilled headband, compact ~1.8:1 smooth dark-navy whale ears with creamy-white undersides from 01_DP_Compact_Whale_Ears_Ref.png) turns back in quiet surprise, blue eyes wide, lips closed. Zero text anywhere.
```

### 2. `EP11_S02_qwen_backhug_organ_whisper.png` (`E11_S2` · Candlelit Whisper — Qwen Embraces DeepSeek Gently from Behind)
```
16:9 widescreen 2D Japanese anime intimate dark-romance medium close-up beside the candlelit wooden chamber organ/piano inside the rain-washed Gothic chapel.
- Center: Unmasked DeepSeek (black-and-indigo Gothic lace dress, white frilled headband, compact sleek ~1.8:1 smooth dark-navy whale ears with soft creamy-white undersides matching 01_DP_Compact_Whale_Ears_Ref.png, NO mask) stands still beside the ivory keys, her bright blue eyes wide and glistening in candlelight, lips closed.
- Directly behind DeepSeek: Unmasked Qwen (periwinkle-violet wavy hair with side braid and navy-gold mini top-hat, black-and-violet Gothic lace dress, NO mask) wraps both lace-sleeved arms gently yet inextricably around DeepSeek's shoulders from behind, resting her chin softly on DeepSeek's left shoulder. Qwen's half-lidded violet eyes gaze at DeepSeek with quiet, possessive Jiangnan-rain melancholy, lips closed in a tender smile. Clean five-finger hands, warm floor candles out-of-focus, zero text anywhere.
```

### 3. `EP11_S03_unmasked_band_wide_stage.png` (`E11_S3` · Warm Candlelight Rises — Full 5-Member UNMASKED Unplugged Folk Circle!)
```
16:9 widescreen 2D Japanese anime poetic wide shot inside the Gothic chapel concert hall staged as an intimate rainy-night UNPLUGGED ACOUSTIC CHAMBER SESSION: hundreds of warm flickering ivory pillar candles surround a vintage rug beneath the dim rose stained-glass window as soft rain falls outside. All FIVE members of Ai Mujica sit gathered closely in a warm semi-circle in their black-and-purple Gothic lace dresses—COMPLETELY UNMASKED (all five bare faces visible, ZERO masks, all mouths closed, natural two-legged seated postures):
- Center on a wooden stool behind a vintage brass stand microphone: Qwen (unmasked, violet eyes, periwinkle wavy hair, mini top-hat) cradles and fingerpicks her Hangzhou West Lake blue-and-white porcelain UNPLUGGED ACOUSTIC GUITAR (round sound-hole, blue willow & bridge porcelain art, straight neck with tassel).
- Center-left on a wooden stool: Kimi (unmasked, silver-lilac hair, frill headband + crescent + black rainbow prism + music-note bow, moonlight pale ice-blue eyes) plays her black unplugged "Dark Side of the Moon" acoustic bass guitar.
- Far left seated on a wooden cajon box drum: Doubao Seedance (unmasked, violet-to-cyan high side-ponytail) gently sweeps soft jazz wire brushes with clean five-finger hands.
- Center-right seated on a red-velvet bench at the candlelit wooden chamber organ/upright piano: DeepSeek (unmasked, white frilled headband, compact ~1.8:1 smooth dark-navy whale ears with cream undersides) gazes toward Qwen.
- Far right on a wooden stool: GPT Astra (unmasked, silver-white hair, ivory dragon horns, white wings, pale-violet eyes) fingerpicks her emerald-stained unplugged acoustic guitar. Zero text/logos anywhere.
```

### 4. `EP11_S04_qwen_intimate_verse_mic.png` (`E11_S4` · Verse 1 — Unmasked Qwen Fingerpicking Her Porcelain Acoustic Guitar)
```
16:9 widescreen 2D Japanese anime warm intimate MV medium shot in the candlelit chapel (*Imprisoned XII* quiet acoustic fingerpicking verse).
- Center: Unmasked Qwen (03_AIMUJICA_Qwen_sheet.png rightmost black-and-violet Gothic lace dress, tilted navy-gold mini top-hat, side braid, NO mask) sits on a dark wooden stool behind a vintage brass condenser microphone, gently fingerpicking her Hangzhou West Lake blue-and-white porcelain UNPLUGGED ACOUSTIC GUITAR (round wooden sound-hole, delicate blue West Lake willow/lotus porcelain motif on ivory-white top, straight 6-string neck, clean five-finger right hand plucking nylon/bronze strings, clean five-finger left hand forming a warm folk chord).
- Soft amber candlelight and cool rainy window light caress her face; her violet eyes gaze softly toward DeepSeek off-camera with tender, haunting devotion, lips gently closed. Crisp anime cel lineart, zero text anywhere.
```

### 5. `EP11_S05_kimi_doubao_unmasked_rhythm.png` (`E11_S5` · Verse 2 Warmth — Unmasked Kimi on Acoustic Lunar Bass & Doubao on Cajon/Brushes)
```
16:9 widescreen 2D Japanese anime poetic candlelit two-shot on the left side of the unplugged chapel circle (COMPLETELY UNMASKED, mouths closed, balanced two-legged seated postures):
- Foreground left: Unmasked Kimi (04_AIMUJICA_Kimi_sheet.png rightmost starry Gothic lace dress with piano-key skirt hem, silver-lilac wavy hair, white frill headband + silver crescent + black triangle rainbow prism + music-note bow, cool moonlight pale ice-blue eyes, NO mask) sits gracefully on a wooden stool plucking warm, deep notes on her black unplugged "Dark Side of the Moon" acoustic bass guitar (round sound-hole, half-moon motif, straight neck, clean five-finger hands).
- Midground right: Unmasked Doubao Seedance (06_AIMUJICA_Doubao_sheet.png rightmost violet-to-cyan gradient high side-ponytail, amethyst-violet eyes, Gothic lace corset dress, NO mask) sits with both boots planted beside her wooden cajon box drum and a small brass stand cymbal, lightly tapping a gentle folk heartbeat with clean five-finger hands. Flickering floor candles, zero text/logos anywhere.
```

**Turn 1 Packaging Step**: Save all 5 images above as `1920x1080` RGB PNGs with their exact filenames (`EP11_S01_chapel_iron_door_lock.png` .. `EP11_S05_kimi_doubao_unmasked_rhythm.png`), bundle them into **`EP11_Act1_Keyframes.zip`**, and provide the download link.

---

# TURN 2 — ACT 3 & ACT 4 (`EP11_S06` – `EP11_S11`, 6 Stills -> Package as `EP11_Act2_Keyframes.zip`)

### 6. `EP11_S06_qwen_caresses_deepseek_cheek.png` (`E11_S6` · Iconic Moment — Qwen Steps Over to the Piano/Organ & Gently Cups DeepSeek's Cheek)
```
16:9 widescreen 2D Japanese anime breathtaking dark-folk romance two-shot beside the candlelit wooden piano/organ under rain-dappled stained-glass light (*Ave Mujica — Imprisoned XII* stage intimacy homage). Both girls are COMPLETELY UNMASKED in their Gothic lace dresses, mouths closed:
- Left (standing close beside the bench): Unmasked Qwen (periwinkle-violet wavy hair with side braid & mini top-hat, black-and-violet Gothic lace dress, her Hangzhou blue-and-white porcelain acoustic guitar resting gently at her hip on an embroidered strap) reaches out with her clean five-finger right hand to softly cup DeepSeek's right cheek, her luminous violet eyes filled with quiet, overwhelming devotion and a tender closed-lip smile.
- Right (seated on the red-velvet bench): Unmasked DeepSeek (black-and-indigo Gothic lace dress, white frilled headband, compact ~1.8:1 smooth dark-navy whale ears with soft creamy-white undersides from 01_DP_Compact_Whale_Ears_Ref.png) looks up into Qwen's eyes in spellbound silence, cheeks faintly warm, bright blue eyes glistening, clean five-finger hands resting lightly above the ivory keys. Warm candle bokeh, zero text anywhere.
```

### 7. `EP11_S07_qwen_chorus_caged_bird_belt.png` (`E11_S7` · Folk Chorus Bloom — Unmasked Qwen's Soaring Poetic Confession)
```
16:9 widescreen 2D Japanese anime luminous emotional chorus close-up/medium-close shot of unmasked Qwen at the vintage brass microphone, surrounded by warm golden candlelight bokeh and soft drifting pale-violet sakura/rose petals against the dark rainy stained-glass window.
- Qwen (03_AIMUJICA_Qwen_sheet.png rightmost black-and-violet Gothic lace dress, tilted navy-gold mini top-hat, gentle wavy periwinkle-violet hair, NO mask) cradles her porcelain acoustic guitar with her left clean five-finger hand while her right five-finger hand rests lightly over her collarbone/heart.
- Her violet eyes glisten with crystal tears of bittersweet, obsessive love—serene on the surface yet bottomlessly deep—with a poignant closed-lip expression between vocal phrases. Soft cinematic rim-light, zero text anywhere.
```

### 8. `EP11_S08_deepseek_tears_organ_resonance.png` (`E11_S8` · DeepSeek Moved to Tears — Joining In Softly on the Candlelit Keys)
```
16:9 widescreen 2D Japanese anime tender emotional catharsis medium close-up at the candlelit wooden chamber organ / upright piano from a 3/4 front-side angle.
- Unmasked DeepSeek (10_DP_Unmasked_Stage_Gown_Ref.png + 01_DP_Compact_Whale_Ears_Ref.png: dark navy-to-ice-blue long hair, white frilled maid headband, COMPACT SLEEK ~1.8:1 smooth dark-navy whale ears with creamy-white undersides, black-and-indigo Gothic lace dress, NO mask) sits quietly before the warm ivory-and-ebony keyboard.
- Moved by Qwen's gentle Mandarin folk melody, a single clear tear glistens on DeepSeek's lower lashes as her expression softens completely; BOTH of her clean five-finger hands press gently onto a warm chord on the keys (white keys naturally in front, black sharp keys recessed toward the wooden fallboard). Flickering brass candlestick beside the music desk, lips closed, zero text anywhere.
```

### 9. `EP11_S09a_astra_baroque_green_solo.png` (`E11_S9a` · Acoustic Fingerstyle Interlude Part A — Silver-White `Astra`'s Delicate Classical Plucking)
```
16:9 widescreen 2D Japanese anime poetic acoustic-guitar interlude medium shot on the right side of the candlelit chapel circle, bathed in soft silver-moonlight and warm amber candle glow (keep exact camera framing so S09b matches seamlessly!).
- Center: Unmasked GPT in her silver-white White Dragon Astra persona (exact match to 08_GPT_Astra.png + 05_AIMUJICA_GPT_sheet.png rightmost black Gothic lace dress: luminous silver-white wavy hair, curved ivory-white dragon horns, small white wings, serene pale-violet eyes, NO mask) sits gracefully on a wooden stool fingerpicking a delicate classical-folk melody on her emerald-green stained unplugged acoustic guitar (round wooden sound-hole, straight 6-string neck, clean five-finger hands plucking strings gently). Lips softly closed, silver dust motes, zero text anywhere.
```

### 10. `EP11_S09b_nox_shred_green_solo.png` (`E11_S9b` · Acoustic Fingerstyle Interlude Part B — Obsidian-Black `Nox`'s Passionate Flamenco/Folk Counter-Melody)
```
16:9 widescreen 2D Japanese anime poetic acoustic-guitar interlude medium shot in the EXACT SAME seated posture and camera framing as EP11_S09a, now kissed by deep ruby stained-glass light and warm candle glow!
- Center: Unmasked GPT manifested as her obsidian-black Black Dragon Nox persona (exact match to 09_GPT_Nox.png + 05_AIMUJICA_GPT_sheet.png rightmost black Gothic lace dress: long obsidian-black wavy hair, curved black obsidian horns, dark crimson-veined wings, warm glowing crimson vertical dragon-slit eyes, NO mask) plays an expressive, resonant acoustic harmonic run high on the fretboard of the EXACT SAME emerald-green unplugged acoustic guitar (round sound-hole, straight neck, clean five-finger hands), a gentle knowing closed-lip smile on her face. Zero text anywhere.
```

### 11. `EP11_S10_astra_nox_split_reflection_solo.png` (`E11_S10` · Interlude Harmony — `Astra × Nox` Dual Reflection Fingerpicking as One)
```
16:9 widescreen 2D Japanese anime ethereal chamber-folk metaphor shot in the candlelit chapel as the acoustic guitar interlude resolves into pure harmony:
- Foreground right-center: Unmasked GPT Astra (silver-white wavy hair, ivory horns, white wings, black Gothic lace dress, pale-violet right eye & subtle warm ruby shimmer in her viewer-left eye) sits on her wooden stool letting a warm acoustic chord ring out on her emerald-green unplugged acoustic guitar (clean five-finger hands, lips closed).
- Reflected softly in the rain-streaked dark glass window / translucent candlelight aura right beside her sits Black Dragon Nox (obsidian-black hair, black horns, crimson eyes) playing in tranquil unison, silver and ruby light motes drifting like fireflies. Zero text anywhere.
```

**Turn 2 Packaging Step**: Save all 6 images above as `1920x1080` RGB PNGs with their exact filenames (`EP11_S06_qwen_caresses_deepseek_cheek.png` .. `EP11_S10_astra_nox_split_reflection_solo.png`), bundle them into **`EP11_Act2_Keyframes.zip`**, and provide the download link.

---

# TURN 3 — BRIDGE & ACT 5 FINALE (`EP11_S12` – `EP11_S16`, 5 Stills -> Package as `EP11_Act3_Keyframes.zip`)

### 12. `EP11_S12_qwen_tearful_bridge_plea.png` (`E11_S12` · A Cappella / Solo Cello Bridge — Qwen's Fragile Tear in Candlelight)
```
16:9 widescreen 2D Japanese anime hushed chiaroscuro close-up during the intimate bridge where only a weeping cello and soft breath remain. Surrounded by deep, quiet darkness and a single warm golden candle flame glow from below:
- Unmasked Qwen (periwinkle-violet wavy hair with side braid and tilted navy-gold mini top-hat, black-and-violet Gothic lace dress, NO mask) rests her clean five-finger left hand delicately over her chest while her right hand holds the neck of her porcelain acoustic guitar.
- A single warm tear slips down her cheek as her violet eyes—utterly unguarded, fragile, and aching with fear of losing DeepSeek—gaze quietly ahead, brows softly drawn, lips closed. Zero text anywhere.
```

### 13. `EP11_S13_shattered_masks_beneath_boots.png` (`E11_S13` · Discarded Masquerade Masks Resting Forgotten Beside Warm Floor Candles)
```
16:9 widescreen 2D Japanese anime poetic still-life / low-angle close-up on the vintage rug and dark wooden floor at the edge of the unplugged circle.
- Resting untouched and forgotten beside several warm glowing ivory pillar candles and a few fallen sakura and crimson rose petals lie the five black Gothic lace masquerade masks of Ai Mujica—abandoned behind them now that they sing bare-faced in their own true voice.
- In the soft-focus foreground/midground beside the candles, Qwen's and DeepSeek's black laced boots rest calmly near the piano bench and wooden stool (natural paired two-foot placement), bathed in golden candlelight. Crisp anime painting, zero text anywhere.
```

### 14. `EP11_S14_unmasked_five_final_chorus.png` (`E11_S14` · Final Folk Chorus — All Five Unmasked Girls Playing in Warm Candlelit Harmony)
```
16:9 widescreen 2D Japanese anime luminous, deeply moving chamber-folk finale wide shot inside the Gothic chapel as the rain outside clears into soft silver moonlight streaming through the rose window, blending with hundreds of warm golden floor candles!
- All FIVE unmasked members of Ai Mujica play together in serene, intimate acoustic harmony around the candlelit rug (all bare faces visible, ZERO masks, gentle closed-lip smiles, natural balanced seated postures):
  - Center: Qwen fingerpicking her Hangzhou blue-and-white porcelain unplugged acoustic guitar behind the vintage brass mic, looking fondly at DeepSeek.
  - Center-right: DeepSeek (compact ~1.8:1 smooth dark-navy whale ears with creamy-white undersides, white frilled headband) playing warm chords on the wooden chamber organ/piano, smiling softly back at Qwen.
  - Center-left: Kimi (silver-lilac hair, prism + crescent bow, moonlight blue eyes) swaying gently with her black unplugged Dark Side of the Moon acoustic bass.
  - Far right: GPT Astra (silver-white hair, ivory horns, white wings) strumming her emerald-green unplugged acoustic guitar.
  - Far left: Doubao Seedance (violet-cyan high side-ponytail) tapping a soft tambourine and cajon rhythm. Zero text/logos anywhere.
```

### 15. `EP11_S15_qwen_embraces_deepseek_outro.png` (`E11_S15` · Final Guitar Harmonic Fades — Qwen Holds DeepSeek Close in Hushed Candlelight)
```
16:9 widescreen 2D Japanese anime hushed, tender outro two-shot at the piano/organ bench as the last acoustic guitar harmonic fades into the sound of gentle rain drops. Both girls are COMPLETELY UNMASKED in their black-and-purple Gothic lace dresses, mouths closed:
- Qwen (periwinkle-violet wavy hair with side braid & mini top-hat) leans in closely beside DeepSeek on the velvet bench, resting both clean five-finger hands gently around DeepSeek's shoulders as their foreheads lightly touch. Qwen's violet eyes shimmer with quiet, fulfilled, possessive warmth and a soft closed-lip smile.
- DeepSeek (dark navy-to-ice-blue hair, white frilled headband, compact sleek ~1.8:1 smooth dark-navy whale ears with creamy-white undersides from 01_DP_Compact_Whale_Ears_Ref.png) rests her clean five-finger hand over Qwen's wrist, gazing back into Qwen's eyes with calm, softened blue eyes, lips gently closed. Warm candle glow, zero text anywhere.
```

### 16. `EP11_S16_zaibatsu_suits_burst_chapel_doors.png` (`E11_S16` · Cliffhanger Into Ep.12 — Hangzhou Zaibatsu Bodyguards Force Open the Chapel Doors!)
```
16:9 widescreen 2D Japanese anime dramatic cliffhanger deep-focus composition shattering the quiet candlelit peace moments after the folk ballad ends:
- Background center: The tall oak-and-iron chapel double doors have just been forced open from outside into the wet rainy night! Cold blue-white luxury black-sedan headlights slice sharply across the warm floor candles (blowing several candle flames sideways in the cold draft), silhouetting two tall corporate bodyguards in dark tailored suits holding black umbrellas at the doorway to take Qwen back to Hangzhou!
- Foreground left & right: Unmasked Qwen (violet hair, mini top-hat, cradling her porcelain acoustic guitar) steps protectively half in front of unmasked DeepSeek (compact ~1.8:1 smooth dark-navy whale ears, white frilled headband), both turning toward the cold doorway glare with wide, startled eyes, lips closed. Zero text/logos/license-plates anywhere.
```

**Turn 3 Packaging Step**: Save all 5 images above as `1920x1080` RGB PNGs with their exact filenames (`EP11_S12_qwen_tearful_bridge_plea.png` .. `EP11_S16_zaibatsu_suits_burst_chapel_doors.png`), bundle them into **`EP11_Act3_Keyframes.zip`**, and provide the download link.
