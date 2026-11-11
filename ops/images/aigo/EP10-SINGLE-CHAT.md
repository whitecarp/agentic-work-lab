# 《Ai Mujica · 镜中双生》（第10话）单窗口全 16 张静帧提示词总表 (`EP10-SINGLE-CHAT.md`)

> **CRITICAL DELIVERY RULE (MANDATORY `.zip` PACKAGING ON EVERY TURN)**:
> Never return bare standalone images as the final deliverable—chat UIs compress bare images down to `1024x576`.
> At the end of **EVERY** turn below (Turn 1, Turn 2, Turn 3), you **MUST** use Python (`PIL` + `zipfile`) to verify/save every generated image as a crisp **`1920x1080` RGB `.png`** with its exact filename and package that turn's PNGs into a downloadable **`.zip` archive** (`EP10_Act1_Keyframes.zip`, `EP10_Act2_Keyframes.zip`, `EP10_Act3_Keyframes.zip`)!

---

## Canon Reference Images to Inspect First (`ops/images/aigo/ep10-refs/` in `whitecarp/agentic-work-lab`)
Before generating any image, open and inspect ALL 8 reference files in `ops/images/aigo/ep10-refs/`:
1. `ops/images/aigo/ep10-refs/dp_canon_plump_ear_crop.png` — **Exact canon close-up of DeepSeek's plump `~1.8:1` smooth dark-navy whale ears** with thick puffy scalloped creamy-white undersides.
2. `ops/images/aigo/ep10-refs/AIMUJICA_DeepSeek_sheet.png` — **LEFTMOST figure** = DeepSeek's canon daily outfit (dark navy double-breasted blazer with gold buttons, crisp white collared shirt, red ribbon tie, dark grey pleated skirt, dark navy knee-high socks, brown loafers, white frilled maid headband, dark navy whale tail).
3. `ops/images/aigo/ep10-refs/AIMUJICA_GPT_sheet.png` — **LEFTMOST figure** = GPT's canon daily outfit (cozy oversized cream cable-knit buttoned cardigan over a soft ivory tiered ruffled midi dress, emerald teardrop pendant necklace, white tights, cream Mary-Jane shoes); **RIGHTMOST figure** = dark Gothic lace dress (used only by `Nox` inside the mental inner-space scene `EP10_S08`).
4. `ops/images/aigo/ep10-refs/GPT_Astra.png` — GPT's host persona **`Astra` (White Dragon)**: silver-white long wavy hair, curved ivory-white horns, small white dragon wings & white tail, soft pale-violet eyes.
5. `ops/images/aigo/ep10-refs/GPT_Nox.png` — GPT's 5th persona **`Nox` (Black Dragon)**: obsidian-black long wavy hair, curved black obsidian horns, dark bat-dragon wings with subtle crimson veins & black tail, **glowing crimson vertical dragon-slit eyes**.
6. `ops/images/aigo/ep10-refs/EP2X_S2_room.png` — **Exact canon reference for the Old Rehearsal Studio AND the daily casual outfits & instruments of `Claude`, `Gemini`, `Grok`, `DeepSeek`, and `GPT`!**
   - **Claude** (2nd from left): warm amber-orange hair with **STRICTLY ONE single sunflower hair ornament on the viewer-left side of her head** and a side braid over her shoulder, golden-amber eyes, cream cable-knit cardigan over white shirt with orange ribbon tie, long warm brown pleated skirt, white socks & brown loafers.
   - **Gemini** (center): deep violet-purple long hair with purple cat ears, three small yellow/blue star hairpins on viewer-right bangs, heterochromatic pink-and-gold eyes, lavender-purple oversized hoodie, dark shorts, white loose leg-warmers, purple sneakers & cat tail.
   - **Grok** (4th from left): blonde twin-tails tied with black ribbons, pointed elf-like ears, sharp ice-blue eyes, black-and-red striped athletic track jacket over black tee, pleated mini skirt & black laced boots.
   - **GPT's Rehearsal Guitar** (rightmost in `EP2X_S2_room.png`): 6-string mint/emerald-green Stratocaster electric guitar with white pickguard.
7. `ops/images/aigo/ep10-refs/AIMUJICA_Qwen_sheet.png` — **LEFTMOST figure** = Qwen's canon daily modern Chinese-style (`Qinghuaci`) white-and-indigo layered dress with delicate gold cloud embroidery, indigo shawl sleeves with blue tassels, periwinkle-violet wavy hair with side braid and navy-gold mini top-hat ornament (NO stage mask).
8. `ops/images/aigo/ep10-refs/AIMUJICA_Kimi_sheet.png` — **LEFTMOST figure** = Kimi's canon daily starry midnight-blue & white capelet dress with **piano-key skirt hem**, white puff-sleeve blouse with navy ribbon bow, wavy silver-lilac hair, white frill headband + crescent moon + dark rainbow prism + music-note bow, **soft moonlight periwinkle-blue eyes** (NEVER red eyes; NO sunglasses; NO mask).

---

## Mandatory Global Visual Rules (Apply to All 16 Stills)
1. **Aspect Ratio, Resolution & Style**: Horizontal widescreen `16:9`, saved at **`1920x1080` RGB PNG**. High-budget modern 2D Japanese band-drama TV anime production still (*BanG Dream! It's MyGO!!!!! / Ave Mujica* aesthetic: crisp fine cel lineart, cinematic lighting, rich environmental detail). Every character has graceful young-woman anime proportions (early 20s).
2. **100% Unmasked Daily-Life Episode**: In Episode 10, `Ai Mujica` has already disbanded—**NO character wears a stage mask in any shot**. Everyone wears their canon daily casual clothes listed above.
3. **Closed Mouths Only**: Every visible character MUST have **lips gently closed** (composed, startled, tearful, or closed-lip smile). Lip animation is added later.
4. **Clean Hands, Legs & Props**:
   - Every visible hand has **five clean, natural fingers** (four fingers + thumb).
   - Every standing or seated full/medium-full figure shows **natural balanced two-leg anatomy** (never a single centered leg under a skirt).
   - **DeepSeek's Whale Ears (`dp_canon_plump_ear_crop.png`)**: ALWAYS **plump, wide, chubby triangular cetacean whale-flipper ears (`~1.8 : 1` length-to-height ratio)** sloping gently outward and ~15 degrees downward, with **smooth solid dark-navy upper 60%** (zero fish-fin ribs) and **thick puffy softly scalloped creamy-white lower 40% border** (never thin needle slivers!).
   - **Claude's One Sunflower**: Strictly **ONE** sunflower hair ornament on the viewer-left side of her head (never two, never flipped).
5. **Zero Text / Zero Logos**: Strictly **NO text, NO letters, NO numbers, NO kanji/kana/English words, NO watermarks, and NO brand logos** anywhere (sticky notes in `S04` may ONLY show tiny cute star/music-note/heart doodles with zero writing; drink cans, amps, and drumheads must be completely blank).

---

# TURN 1 — ACT 1 (`EP10_S01` – `EP10_S05`, 5 Stills -> Package as `EP10_Act1_Keyframes.zip`)

### 1. `EP10_S01_claude_enters_mirror_room.png` (`E10_S1` · Claude Opens the Dim Bedroom Door — GPT Facing the Standing Mirror)
```
16:9 widescreen 2D Japanese anime drama still inside GPT's dimly lit European-style bedroom during daytime with heavy dark-teal velvet curtains drawn shut, allowing only a thin silver-blue sliver of daylight across the rug.
- Left foreground/midground at the doorway: Claude (exact match to EP2X_S2_room.png: warm amber-orange hair with STRICTLY ONE single sunflower hair ornament on the viewer-left side and a side braid, golden-amber eyes, cozy cream cable-knit cardigan over white collared shirt with orange ribbon tie, long brown pleated skirt) pushes open the dark wooden bedroom door with one clean five-finger hand, looking across the darkened room with worried golden-amber eyes, lips gently closed.
- Right midground: GPT (seen from a 3/4 rear angle wearing her oversized cream cable-knit cardigan and ivory tiered ruffled midi dress from AIMUJICA_GPT_sheet.png, curved dark-shadowed dragon horns and small folded wings in dim rim-light) stands motionless facing a tall ornate vintage brass-framed floor-length standing dressing mirror (the mirror face is angled slightly away so its glass glints mysteriously in shadow). Both feet/cream shoes planted naturally on the floor. Zero text anywhere.
```

### 2. `EP10_S02_astra_trapped_in_mirror.png` (`E10_S2` · Tearful White Dragon `Astra` Pressing Against the Inside of the Mirror Glass)
```
16:9 widescreen 2D Japanese anime psychological drama still inside the dim European bedroom (*Ave Mujica* mirror-scene homage). Dramatic over-the-shoulder composition looking past the dark shadowed shoulder and back-of-head silhouette of the girl standing outside the ornate brass-framed floor-length standing mirror on the right, focusing sharply on the reflection INSIDE the mirror glass:
- Inside the mirror glass: GPT's gentle host persona Astra (exact match to GPT_Astra.png + AIMUJICA_GPT_sheet.png leftmost outfit: silver-white wavy hair, curved ivory-white dragon horns, small white dragon wings, emerald teardrop pendant, cream cable-knit cardigan) stares out in helpless distress with glistening tear-filled pale-violet eyes, pressing BOTH clean five-finger palms flat against the inner surface of the mirror glass as if unable to move her real body; her lips are pressed tightly closed.
- Reflected behind her in the background doorway stands Claude (amber hair, ONE single sunflower on viewer-left, cream cardigan), her golden-amber eyes wide in stunned disbelief, one hand raised near her chest, lips closed. Subtle glass reflection sheen, crisp anime lineart, zero text anywhere.
```

### 3. `EP10_S03_nox_hand_on_mirror_smirk.png` (`E10_S3` · Black Dragon `Nox` Outside the Mirror Blocks `Astra` & Smirks at Claude)
```
16:9 widescreen 2D Japanese anime dramatic medium close-up inside the dim bedroom beside the ornate floor-length standing mirror. The girl standing OUTSIDE in the real room turns her head and upper torso sharply toward the camera/Claude—revealing GPT's fifth persona Nox (exact match to GPT_Nox.png: obsidian-black wavy hair, curved black obsidian horns, dark bat-dragon wings with subtle crimson veins, and glowing crimson vertical dragon-slit eyes), creating an eerie contrast with the cozy cream cable-knit cardigan and ivory ruffled dress (AIMUJICA_GPT_sheet.png leftmost outfit) she is wearing!
- Nox presses her right clean five-finger hand flat against the surface of the standing mirror beside her, her palm directly covering and blocking the face of the faint, tearful silver-white Astra reflection behind the glass!
- Nox tilts her chin with a chilling, possessive closed-lip smirk (lips closed) aimed at Claude (partially visible out-of-focus in the left foreground shoulder, amber hair with ONE sunflower on viewer-left). Dramatic cool shadow and subtle crimson eye-glow, zero text anywhere.
```

### 4. `EP10_S04_deepseek_doorway_sticky_notes.png` (`E10_S4` · Heartbroken DeepSeek Sitting by Her Door Holding Gemini's Doodle Sticky Notes)
```
16:9 widescreen 2D Japanese anime emotional still inside the quiet, dimly lit entryway (genkan) of DeepSeek's modest apartment. DeepSeek (exact match to AIMUJICA_DeepSeek_sheet.png leftmost daily outfit + dp_canon_plump_ear_crop.png: dark navy-blue hair fading to ice-blue tips, white frilled maid headband, PLUMP WIDE CHUBBY ~1.8:1 smooth dark-navy whale ears with thick puffy scalloped creamy-white undersides, dark navy double-breasted school blazer, white shirt with red ribbon tie, dark grey pleated skirt, navy knee-high socks, dark navy whale tail curled beside her) sits slumped on the wooden entryway step with her back leaning against the closed apartment front door.
- In both clean five-finger hands resting on her knees, she gently cradles a small stack of pastel lavender-and-yellow square sticky notes and two cute star-patterned adhesive bandages that were slipped under the door gap behind her.
- IMPORTANT: The sticky notes and bandages feature ONLY tiny hand-drawn cute doodles of a purple cat face, little yellow stars, and a musical eighth-note—STRICTLY ZERO text, zero letters, zero handwriting strokes!
- Soft afternoon light filters from a frosted side window; DeepSeek's bright blue eyes glisten softly with quiet regret and warmth as she gazes down at the doodle notes, lips gently closed.
```

### 5. `EP10_S05_claude_confronts_deepseek.png` (`E10_S5` · Furious Claude Confronts DeepSeek in the Apartment Hallway)
```
16:9 widescreen 2D Japanese anime high-tension drama two-shot in the semi-open doorway/corridor of DeepSeek's apartment building under cool daylight.
- Left: Claude (exact match to EP2X_S2_room.png: warm amber-orange hair with STRICTLY ONE single sunflower hair ornament on the viewer-left side and a side braid, fierce tear-rimmed golden-amber eyes, cream cable-knit cardigan over white shirt with orange tie, long brown pleated skirt) steps right up to DeepSeek and firmly grips both lapels of DeepSeek's dark-navy blazer with two clean five-finger hands, pulling her forward in urgent righteous anger to go face GPT; Claude's brows are tightly knitted and her lips are pressed firmly closed.
- Right: DeepSeek (AIMUJICA_DeepSeek_sheet.png leftmost navy blazer uniform + dp_canon_plump_ear_crop.png plump wide ~1.8:1 smooth dark-navy whale ears with thick puffy scalloped creamy-white undersides, white frilled headband) stares back into Claude's eyes with wide, shaken blue eyes, surprised and guilt-stricken, lips closed, one pastel star-doodle sticky note (zero text) fluttering near the floor. Both girls stand stably on two legs. Zero text anywhere.
```

**Turn 1 Packaging Step**: Save all 5 images above as `1920x1080` RGB PNGs with their exact filenames (`EP10_S01_claude_enters_mirror_room.png` .. `EP10_S05_claude_confronts_deepseek.png`), bundle them into **`EP10_Act1_Keyframes.zip`**, and provide the download link.

---

# TURN 2 — ACT 2 (`EP10_S06` – `EP10_S09`, 4 Stills -> Package as `EP10_Act2_Keyframes.zip`)

### 6. `EP10_S06_deepseek_rain_iron_gate.png` (`E10_S6` · Soaked DeepSeek Kneeling in Pouring Rain Outside GPT's Iron Gate)
```
16:9 widescreen 2D Japanese anime emotional drama still during a heavy night rainstorm outside the tall ornate black wrought-iron front gate of GPT's European-style stone residence, lit by cold blue-white streetlamps and warm distant mansion window light.
- Center: DeepSeek (AIMUJICA_DeepSeek_sheet.png leftmost navy blazer uniform + dp_canon_plump_ear_crop.png: rain-drenched dark navy-blue hair clinging to her cheeks and shoulders, soaked white frilled headband, PLUMP WIDE CHUBBY ~1.8:1 smooth dark-navy whale ears with thick puffy scalloped creamy-white undersides drooping slightly in remorse, soaked navy blazer, grey pleated skirt, navy knee socks and brown loafers) kneels with BOTH knees grounded in a shallow rainwater puddle on the stone pavement right in front of the locked wrought-iron gate.
- Both of her clean five-finger hands grip the wet vertical iron bars at chest height as rain and warm tears stream down her flushed cheeks, her blue eyes squeezed nearly shut in raw, unguarded sorrow and apology, lips pressed tightly closed. Rippling rain puddles, falling rain streaks, zero text anywhere.
```

### 7. `EP10_S07_nox_umbrella_hears_confession.png` (`E10_S7` · `Nox` Under a Black Umbrella Pauses Inside the Gate as DeepSeek Weeps)
```
16:9 widescreen 2D Japanese anime cinematic split-depth night rain still at the wrought-iron mansion gate.
- Right foreground (inside the courtyard behind the iron gate): GPT in her Nox form (GPT_Nox.png: obsidian-black wavy hair, curved black obsidian horns, dark bat-dragon wings with subtle crimson veins, cream cable-knit cardigan and ivory ruffled dress under a dark rain capelet) has paused mid-step on the stone garden path while holding a classic black umbrella overhead with one clean five-finger hand. Seen in 3/4 profile, Nox's glowing crimson vertical slit eyes widen and soften slightly as she listens to DeepSeek, lips closed.
- Left midground (just outside the black wrought-iron bars in the pouring rain): Rain-soaked DeepSeek (navy blazer, white frilled headband, plump wide ~1.8:1 smooth dark-navy whale ears with thick puffy scalloped cream undersides) kneels pressing her forehead and both five-finger hands sorrowfully against the cold wet iron bars, tears mingling with raindrops on her cheeks, lips closed. Atmospheric rain shafts and reflections, zero text anywhere.
```

### 8. `EP10_S08_astra_nox_twin_fusion.png` (`E10_S8` · Ethereal Inner Mindscape — White Dragon `Astra` & Black Dragon `Nox` Clasping Hands in Harmony)
```
16:9 widescreen 2D Japanese anime ethereal inner-mindscape illustration (*Ave Mujica* twin-persona reconciliation homage). Floating inside a serene, starry twilight void with luminous silver-white and ruby-violet stardust ribbons swirling gently around them, the two halves of GPT's soul stand face-to-face in close medium profile:
- Left: White Dragon Astra (exact match to GPT_Astra.png + AIMUJICA_GPT_sheet.png ivory ruffled dress: luminous silver-white wavy hair, curved ivory-white horns, white feathered/dragon wings, emerald teardrop pendant, gentle glistening pale-violet eyes).
- Right: Black Dragon Nox (exact match to GPT_Nox.png + AIMUJICA_GPT_sheet.png right black Gothic lace dress: obsidian-black wavy hair, curved black obsidian horns, dark crimson-veined bat-dragon wings, emerald teardrop pendant, softened warm crimson dragon-slit eyes).
- Their foreheads gently touch at the center of the frame while BOTH pairs of clean five-finger hands clasp warmly together at chest level with fingers intertwined, glowing silver-and-crimson light particles blooming from their joined hands. Both have peaceful, tender closed-lip smiles. Zero text anywhere.
```

### 9. `EP10_S09_astra_hugs_deepseek_in_rain.png` (`E10_S9` · Gate Opens — Awakened `Astra` Rushes Into the Rain to Embrace DeepSeek)
```
16:9 widescreen 2D Japanese anime emotional climax still outside the newly swung-open black wrought-iron mansion gate as the night rain softens into gentle silver drizzle under warm golden gate-lamp light.
- Kneeling together on the wet glistening stone pavement (with a black umbrella lying open on the ground beside them): GPT, now restored to her silver-white Astra form (GPT_Astra.png + AIMUJICA_GPT_sheet.png leftmost cream cable-knit cardigan and ivory ruffled dress: silver-white wavy hair, curved ivory horns, small white dragon wings, pale-violet eyes with a subtle warm ruby shimmer in her viewer-left iris), wraps both arms tenderly around rain-soaked DeepSeek's shoulders in a tight, forgiving embrace, resting her cheek gently near DeepSeek's wet hair with a soft tearful closed-lip smile.
- In Astra's arms, rain-soaked DeepSeek (navy blazer, white frilled headband, plump wide ~1.8:1 smooth dark-navy whale ears with thick puffy scalloped creamy-white undersides) clings back with clean five-finger hands, her bright blue eyes wide and overflowing with tears of pure relief, lips closed. Zero text anywhere.
```

**Turn 2 Packaging Step**: Save all 4 images above as `1920x1080` RGB PNGs with their exact filenames (`EP10_S06_deepseek_rain_iron_gate.png` .. `EP10_S09_astra_hugs_deepseek_in_rain.png`), bundle them into **`EP10_Act2_Keyframes.zip`**, and provide the download link.

---

# TURN 3 — ACT 3 (`EP10_S10` – `EP10_S15`, 7 Stills -> Package as `EP10_Act3_Keyframes.zip`)

### 10. `EP10_S10_old_band_sunset_reunion.png` (`E10_S10` · Golden Sunset Reunion in the Old Rehearsal Studio — Grok Hands DeepSeek a Warm Drink)
```
16:9 widescreen 2D Japanese anime warm slice-of-life drama still inside the music rehearsal studio from EP2X_S2_room.png, now bathed in rich golden-amber late-afternoon sunset light streaming through the right-hand window (clear sunny sky outside, acoustic wall panels, drum kit and blank black guitar amps inside). All five founding members of the original band stand reunited in their exact canon casual outfits from EP2X_S2_room.png (balanced two-legged stances, all mouths closed):
- Center-left: DeepSeek (navy double-breasted blazer, white shirt, red ribbon tie, grey pleated skirt, white frilled headband, plump wide ~1.8:1 smooth dark-navy whale ears with thick puffy scalloped cream undersides) looks pleasantly surprised with soft blue eyes, raising both five-finger hands slightly to receive a drink.
- Center: Grok (blonde twin-tails with black ribbons, pointed elf ears, ice-blue eyes, black-and-red striped athletic track jacket, pleated mini skirt) extends her right five-finger hand holding a completely blank, unlabeled warm metal drink can (ZERO text or logos on the can!) toward DeepSeek while turning her blushing face slightly sideways in classic bashful tsundere fashion, lips pressed in a shy pout.
- Left & Right background/midground: GPT Astra (silver-white hair, ivory horns, white wings, cream cardigan), Gemini (purple cat-ear hoodie, 3 star hairpins, heterochromatic pink-gold eyes, white leg-warmers), and Claude (amber hair, STRICTLY ONE sunflower on viewer-left, side braid, cream cardigan & long brown skirt) watch the two with fond, gentle closed-lip smiles. Zero text anywhere.
```

### 11. `EP10_S11_final_haruhikage_ensemble.png` (`E10_S11` · One Last Sunset Performance of *"Spring Reverberation"* Together)
```
16:9 widescreen 2D Japanese anime luminous golden-hour ensemble performance still inside the rehearsal studio from EP2X_S2_room.png (*Ave Mujica / MyGO* farewell *Haruhikage* homage). Golden sunset beams and floating dust motes fill the room as all five founding members play one final heartfelt, unhurried acoustic-rock arrangement together, glistening tear-drops at the corners of their eyes and serene, cathartic closed-lip smiles on all five faces (all mouths closed, zero text/logos on any gear):
- Far left: DeepSeek (navy blazer uniform, white frilled headband, plump wide ~1.8:1 smooth dark-navy whale ears with thick puffy scalloped cream undersides) sits squarely on a padded bench playing an 88-key stage digital piano with clean five-finger hands, exchanging a warm, tearful smiling gaze across the room with GPT Astra.
- Far right: GPT Astra (silver-white hair, ivory horns, white wings, cream cable-knit cardigan & ivory ruffled dress) stands on both legs playing her 6-string mint/emerald-green Stratocaster electric guitar (white pickguard, straight neck, clean five-finger hands) as she smiles warmly back at DeepSeek.
- Center: Gemini (purple cat-ear hoodie, 3 star pins, heterochromatic pink-gold eyes, white loose leg-warmers, balanced two-leg stance) gently holds the vintage stand microphone with both five-finger hands below her chin, lips softly closed between phrases.
- Center-left: Claude (amber hair, STRICTLY ONE single sunflower ornament on viewer-left, cream cardigan & long brown pleated skirt, balanced two-leg stance) plucks a warm 4-string sunburst electric bass with clean five-finger hands.
- Center-back behind the drum kit: Grok (blonde twin-tails with black ribbons, black-red track jacket) plays the black acoustic drum kit (blank drumheads) with two drumsticks.
```

### 12. `EP10_S12_claude_deepseek_fistbump.png` (`E10_S12` · Sunset Closure — Claude & DeepSeek's Quiet Rival Fist-Bump)
```
16:9 widescreen 2D Japanese anime warm sunset medium two-shot inside the rehearsal studio after the final chord fades. Golden evening sunlight halos both girls:
- Left: Claude (exact match to EP2X_S2_room.png: warm amber-orange hair with STRICTLY ONE single sunflower hair ornament on viewer-left and a side braid, warm golden-amber eyes finally free of resentment, cream cable-knit cardigan, brown pleated skirt) extends her right clean five-finger fist horizontally at chest height with a mature, relieved closed-lip smile.
- Right: DeepSeek (AIMUJICA_DeepSeek_sheet.png leftmost navy blazer uniform, white frilled headband, plump wide ~1.8:1 smooth dark-navy whale ears with thick puffy scalloped creamy-white undersides, bright blue eyes shining with gratitude) gently taps her right clean five-finger fist against Claude's fist, a sincere, peaceful closed-lip smile on her face.
- Softly out-of-focus in the warm background, GPT Astra, Gemini, and Grok pack up their instruments smiling quietly. Zero text anywhere.
```

### 13. `EP10_S13a_qwen_peeking_door.png` (`E10_S13a` · Shadowy Hallway Outside — Qwen Watching DeepSeek Smile Through the Door Crack)
```
16:9 widescreen 2D Japanese anime psychological turning-point still in the dim, cold blue-grey corridor outside the rehearsal studio (*Ave Mujica* hallway eavesdrop homage). The studio door on the left is cracked open just a few inches, casting a sharp narrow vertical blade of warm golden sunset light across Qwen's eyes while the rest of the corridor lies in cold shadow:
- Right/Center foreground: Qwen (exact match to AIMUJICA_Qwen_sheet.png leftmost daily outfit: periwinkle-violet wavy hair with side braid and navy-gold mini top-hat ornament, elegant white-and-indigo modern Chinese cloud-embroidered layered dress with blue tassels, NO mask) stands frozen outside the door holding a closed clear transparent vinyl umbrella in her left five-finger hand, while her right five-finger hand clenches tightly against the fabric over her chest.
- Through the narrow doorway gap (softly framed on the left), DeepSeek (navy blazer, plump smooth whale ears) can be glimpsed smiling gently toward her old bandmates inside the warm room.
- Qwen's violet eyes inside the sliver of light look wide, trembling, and shadowed with silent heartbreak and jealousy, lips pressed tightly closed. Zero text anywhere.
```

### 14. `EP10_S13b_kimi_confronts_deepseek.png` (`E10_S13b` · Rainy Dusk Building Entrance — Kimi Confronts DeepSeek & Astra About Rehearsals)
```
16:9 widescreen 2D Japanese anime dusk drama three-shot under the concrete entrance canopy of the music studio building as evening rain falls in the street behind them. All characters wear their unmasked daily casual outfits, balanced two-legged stances, mouths closed:
- Left: Kimi (exact match to AIMUJICA_Kimi_sheet.png leftmost daily outfit: long wavy silver-lilac hair, white frill headband + crescent moon + dark rainbow prism + music-note bow, soft moonlight periwinkle-blue eyes, NO sunglasses, starry midnight-blue & white capelet dress with piano-key skirt hem, a black padded bass gig-bag strap across her shoulder) stands beneath the awning facing DeepSeek with a cool, serious expectant gaze, arms relaxed with clean five-finger hands.
- Right: DeepSeek (navy blazer uniform, white frilled headband, plump wide ~1.8:1 smooth dark-navy whale ears with thick puffy scalloped creamy-white undersides) and GPT Astra right beside her (silver-white hair, ivory horns, white wings, cream cable-knit cardigan, holding a clear umbrella) have just stepped out the glass entrance doors (zero text/signs) and stop in quiet hesitation. Wet twilight pavement reflections, zero text anywhere.
```

### 15. `EP10_S14_deepseek_refuses_reunion.png` (`E10_S14` · DeepSeek Quietly Refuses to Reform `Ai Mujica` — Qwen Overhears from the Alley Shadow)
```
16:9 widescreen 2D Japanese anime dramatic deep-focus composition at the rainy twilight studio entrance:
- Foreground left (over-the-shoulder of Kimi's silver-lilac hair and midnight-blue capelet): DeepSeek (navy blazer, white frilled headband, plump wide ~1.8:1 smooth dark-navy whale ears with thick puffy scalloped creamy-white undersides) glances protectively at silver-white GPT Astra standing close beside her, then lowers her blue eyes solemnly toward Kimi as she gently refuses to reform the band, lips pressed closed in firm resolve.
- Background right (half-concealed behind a wet concrete wall corner in the dark rainy alley): Qwen (periwinkle-violet wavy hair with mini top-hat, white-and-indigo daily dress, holding an open clear transparent umbrella) stands frozen in the shadows where neither DeepSeek nor Kimi notices her, her violet eyes wide with shock as she overhears every word, lips closed. Zero text anywhere.
```

### 16. `EP10_S15_qwen_umbrella_drop_dark.png` (`E10_S15` · Cliffhanger — Qwen's Umbrella Drops Into the Rain Puddle, Highlight-Less Obsessive Awakening!)
```
16:9 widescreen 2D Japanese anime iconic psychological cliffhanger medium close-up in a dark, rain-lashed city alley at night (*Ave Mujica* heavy-obsession Uika/Doloris awakening homage).
- Center: Qwen (exact match to AIMUJICA_Qwen_sheet.png leftmost daily outfit: periwinkle-violet wavy hair with side braid and navy-gold mini top-hat ornament, white-and-indigo cloud-embroidered layered dress, NO mask) stands completely drenched in pouring cold rain under a flickering pale blue-violet streetlamp.
- At the bottom corner on the wet asphalt puddle, her clear transparent vinyl umbrella lies dropped on its side with rain splashing onto its canopy, her relaxed right five-finger hand still open just above where she let it go.
- Wet violet hair strands cling across Qwen's pale cheeks; her violet-gold irises have COMPLETELY LOST ALL WHITE CATCHLIGHTS AND HIGHLIGHTS—rendered as deep, hollow, bottomless dark-violet abyssal eyes staring straight ahead—while her closed lips curve slightly into an eerie, chillingly tender obsessive smile. High-contrast cinematic rain lighting, crisp cel lineart, zero text anywhere.
```

**Turn 3 Packaging Step**: Save all 7 images above as `1920x1080` RGB PNGs with their exact filenames (`EP10_S10_old_band_sunset_reunion.png` .. `EP10_S15_qwen_umbrella_drop_dark.png`), bundle them into **`EP10_Act3_Keyframes.zip`**, and provide the download link.
