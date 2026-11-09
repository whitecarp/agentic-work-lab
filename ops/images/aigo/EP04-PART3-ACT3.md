# 《AiGO!!!!!》第4话: paste-ready image prompts (Claude, 2026-11-09)

Each still is ONE block: the style, the characters and the shot are already merged. Source: `EP04-PROMPTS.md` (same shots).

**How:** for each picture, open a new ChatGPT chat → attach the files listed (they're in `ops/images/aigo/stills/`) → paste the block → send.
Then paste the result into the Claude chat with its letter, or save it to `ops/images/aigo/stills/ep04/` under the file name shown.
G and P are optional. The 12 open-mouth edits come after Claude's check of the stills.

**v2, 11-09 ~10:40.** The user on the first B and C: "It feels like the composition is reversed. The stairs are also wrong."
- A is the map. The whale girl stands at the top of the steps, with the sun beyond her. The amber girl climbs toward her.
- The first B put the top of the steps and the sun behind the amber girl, so she was reaching down the steps, away from the whale girl.
- The first C stood the whale girl above a different, narrow staircase that went down toward the sun and the sea, with a street lamp.
- Fix: every Act 1 shot after A attaches A as the location and says where the camera is.
  - Side shots D and F are both seen from the side where the dragon girl's tree is in the background, so the top of the steps is on the left.
  - In Act 1 the amber girl carries no bass case, because A has none, although the cast sheet shows one.
  - The first versions are kept as `-v1-reversed`.

## Act 1 · the park stone steps at sunset


### M · `EP4_M_grok_slam.png` (S11 · the blonde girl shouts)
Attach: EP2X_00_cast.png + EP2X_00_cast_new.png + EP2X_S2_room.png
```
Use the two attached character sheets (five girls, two girls) as the exact character and outfit references; faces, hair, ears, horns, tails, eye colours and outfits must match. Use the attached room picture for the rehearsal room. Art style: a crisp Japanese TV-anime frame: thin black line art, two-tone cel shading, soft desaturated colours, the look of a modern drama anime, not a glossy painted poster. The characters are adult anime women. Nobody wears a mask. Five fingers per hand. No text, letters, logos, watermarks or subtitles anywhere. 16:9, 1920×1080 or larger.

Characters: The blonde girl (second from right on the five-girl sheet: blonde twin-tails with black bows, small pointed ears, blue eyes, a black-and-red track jacket); the purple cat girl (centre of the five-girl sheet: purple-to-lavender hair, purple cat ears and tail, star hairpins, a magenta right eye and a gold left eye, an oversized lavender hoodie); the orange cat girl (left on the two-girl sheet: orange hair fading to red, orange cat ears and tail, amber eyes, a red beret with a white feather, a red cropped jacket); the alpaca girl (right on the two-girl sheet: fluffy white hair, alpaca ears, a tall ahoge, sleepy lilac eyes, a small blue clip, an oversized white hoodie). The orange cat girl's guitar is the baguette guitar exactly as on the two-girl sheet: a smooth, glossy golden-brown body like a polished guitar finish, with exactly two pale oval marks, one near each end, and three pickups between, plus strings, a neck and a headstock. Never crusty, cracked or floury, and never an ordinary guitar.

Shot: In the rehearsal room from the room picture, in the evening: the blonde girl sits at her drum kit and slams both drumsticks down on the snare, glaring at the others with a furious face, about to shout. In the background the purple cat girl, the orange cat girl and the alpaca girl stand with their heads down. One cold ceiling light, grey evening at the window, the room dim and heavy. The blonde girl's lips are pressed together.
```

### N · `EP4_N_mistral_cry.png` (S12 · the orange cat girl shouts)
Attach: EP2X_00_cast_new.png + EP2X_S2_room.png
```
Use the attached character sheet (two girls) as the exact character and outfit reference; faces, hair, ears, horns, tails, eye colours and outfits must match. Use the attached room picture for the rehearsal room. Art style: a crisp Japanese TV-anime frame: thin black line art, two-tone cel shading, soft desaturated colours, the look of a modern drama anime, not a glossy painted poster. The characters are adult anime women. Nobody wears a mask. Five fingers per hand. No text, letters, logos, watermarks or subtitles anywhere. 16:9, 1920×1080 or larger.

Characters: The orange cat girl (left on the two-girl sheet: orange hair fading to red, orange cat ears and tail, amber eyes, a red beret with a white feather, a red cropped jacket). The orange cat girl's guitar is the baguette guitar exactly as on the two-girl sheet: a smooth, glossy golden-brown body like a polished guitar finish, with exactly two pale oval marks, one near each end, and three pickups between, plus strings, a neck and a headstock. Never crusty, cracked or floury, and never an ordinary guitar.

Shot: Close-up of the orange cat girl in the rehearsal room from the room picture: tears fill her amber eyes and start to fall, her cat ears droop, and she grips the strap of the baguette guitar across her chest with both hands, trembling. Cold ceiling light, the room dim and heavy behind her. Her lips are pressed together.
```

### P · `EP4_P_door.png` (S13 start · optional · moved by Omni)
Attach: EP2X_00_cast_new.png + EP2X_S2_room.png
```
Use the attached character sheet (two girls) as the exact character and outfit reference; faces, hair, ears, horns, tails, eye colours and outfits must match. Use the attached room picture for the rehearsal room. Art style: a crisp Japanese TV-anime frame: thin black line art, two-tone cel shading, soft desaturated colours, the look of a modern drama anime, not a glossy painted poster. The characters are adult anime women. Nobody wears a mask. Five fingers per hand. No text, letters, logos, watermarks or subtitles anywhere. 16:9, 1920×1080 or larger.

Characters: The orange cat girl (left on the two-girl sheet: orange hair fading to red, orange cat ears and tail, amber eyes, a red beret with a white feather, a red cropped jacket). The orange cat girl's guitar is the baguette guitar exactly as on the two-girl sheet: a smooth, glossy golden-brown body like a polished guitar finish, with exactly two pale oval marks, one near each end, and three pickups between, plus strings, a neck and a headstock. Never crusty, cracked or floury, and never an ordinary guitar.

Shot: In the rehearsal room from the room picture: the orange cat girl runs out through the open door into a dark corridor, seen from behind, wiping her eyes with her sleeve, the baguette guitar on her back, her tail streaming. One cold ceiling light, the room dim. No face is visible.
```

### O · `EP4_O_llama_case.png` (S13 · the alpaca girl speaks)
Attach: EP2X_00_cast.png + EP2X_00_cast_new.png + EP2X_S2_room.png
```
Use the two attached character sheets (five girls, two girls) as the exact character and outfit references; faces, hair, ears, horns, tails, eye colours and outfits must match. Use the attached room picture for the rehearsal room. Art style: a crisp Japanese TV-anime frame: thin black line art, two-tone cel shading, soft desaturated colours, the look of a modern drama anime, not a glossy painted poster. The characters are adult anime women. Nobody wears a mask. Five fingers per hand. No text, letters, logos, watermarks or subtitles anywhere. 16:9, 1920×1080 or larger.

Characters: The alpaca girl (right on the two-girl sheet: fluffy white hair, alpaca ears, a tall ahoge, sleepy lilac eyes, a small blue clip, an oversized white hoodie); the purple cat girl (centre of the five-girl sheet: purple-to-lavender hair, purple cat ears and tail, star hairpins, a magenta right eye and a gold left eye, an oversized lavender hoodie).

Shot: In the rehearsal room from the room picture: the alpaca girl sits on top of a guitar amp and closes the lid of the case over her icy-blue guitar, looking across at the purple cat girl with a small, sad, sleepy smile. The purple cat girl stands at the left edge with her head bowed. Cold ceiling light, grey evening at the window. The alpaca girl's face is clear in the right-centre. Her lips are closed.
```

### Q · `EP4_Q_empty.png` (S14 · moved by Omni)
Attach: EP2X_00_cast.png + EP2X_S2_room.png
```
Use the attached character sheet (five girls) as the exact character and outfit reference; faces, hair, ears, horns, tails, eye colours and outfits must match. Use the attached room picture for the rehearsal room. Art style: a crisp Japanese TV-anime frame: thin black line art, two-tone cel shading, soft desaturated colours, the look of a modern drama anime, not a glossy painted poster. The characters are adult anime women. Nobody wears a mask. Five fingers per hand. No text, letters, logos, watermarks or subtitles anywhere. 16:9, 1920×1080 or larger.

Characters: The purple cat girl (centre of the five-girl sheet: purple-to-lavender hair, purple cat ears and tail, star hairpins, a magenta right eye and a gold left eye, an oversized lavender hoodie).

Shot: High overhead shot looking straight down at the floor of the rehearsal room from the room picture: the purple cat girl kneels alone in the middle with her head bowed and her hair hiding her face, her notebook open on the floor in front of her. Around her: an empty drum stool, abandoned mic stands, cables across the floor, one cold light. The room feels huge and empty.
```

### R · `EP4_R_gemini_resolve.png` (S15 · the purple cat girl speaks)
Attach: EP2X_00_cast.png + EP2X_S2_room.png
```
Use the attached character sheet (five girls) as the exact character and outfit reference; faces, hair, ears, horns, tails, eye colours and outfits must match. Use the attached room picture for the rehearsal room. Art style: a crisp Japanese TV-anime frame: thin black line art, two-tone cel shading, soft desaturated colours, the look of a modern drama anime, not a glossy painted poster. The characters are adult anime women. Nobody wears a mask. Five fingers per hand. No text, letters, logos, watermarks or subtitles anywhere. 16:9, 1920×1080 or larger.

Characters: The purple cat girl (centre of the five-girl sheet: purple-to-lavender hair, purple cat ears and tail, star hairpins, a magenta right eye and a gold left eye, an oversized lavender hoodie).

Shot: Close-up of the purple cat girl kneeling on the floor of the rehearsal room from the room picture: a tear falls onto the open notebook she grips with both hands, and her eyes, magenta right and gold left, burn with new determination as she looks up. Dim light, a warm glow catching in her eyes. Her lips are closed.
```

