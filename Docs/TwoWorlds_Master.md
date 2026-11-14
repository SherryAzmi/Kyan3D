> **SUPERSEDED.** The source of truth for Two Worlds is now `Docs/TwoWorlds_Design_and_Development.pdf` (one puzzle, one 40 x 40 m layout, flat-colour low-poly style, no textures). This earlier draft is kept for reference only; where they differ, follow the PDF.

# TWO WORLDS: Master Document (2-day game jam prototype)

Status: DRAFT 1 for review. Written from the designer interview and the original brief.
Sections are tagged by discipline so the team can split this later: **[DESIGN]**, **[ART]**, **[VFX]**, **[AUDIO]**, **[UI]**, **[TECH]**.
Items marked **(PROPOSED)** are my suggestions where you said "you decide"; they need your approval.
Core mechanics are shared with *Between Two Worlds* (two gamepads, keyboard fallback, third-person orbit camera, vertical split-screen, interact and power buttons, no fail state).

---

## 1. Summary
| | |
|---|---|
| Title | **Two Worlds** |
| Genre | 3D cooperative puzzle adventure, local split-screen, 2 players |
| Engine | Unity 6 (URP) |
| Deadline | 2 days |
| Length | About **5 minutes** |
| Theme | Something is divided, but the two parts complete each other |
| Message | "Loving yourself is a journey. You don't have to become someone else to deserve it." |
| Fail state | None |
| Scope | 2 characters, 2 worlds, **3 puzzles**, 1 mirror fragment, no inventory, no dialogue, no AI |

## 2. Story [DESIGN]
A person looks into a mirror and sees a faded, exhausted version of themselves. The mirror shatters. The screen goes black and splits in two: the **Present**, a tired and lost person in a pale desert, and the **Future**, a confident, full-of-life person in a vibrant forest. The players do not know they are the same person. The Present needs the mirror fragments to rediscover themself; the Future helps find them, and the Present helps the Future in return. At the end the two discover they were always one person.

## 3. Characters (the same human, two states) [ART]
Unnamed and universal. One human body model, two looks.
| | **The Present** | **The Future** |
|---|---|---|
| Emotion | Depressed, tired, lost | Full of life, confident, hopeful |
| Colour | Washed-out grey and beige clothes, dull skin tone | Warm, saturated colours, soft glow |
| Posture and animation | Slouched, slow walk, head down | Upright, relaxed walk, head up |
| Light | None | Small warm light around them |
| Ability | **Dig**: hold power on a sand mound to sink it; **Touch the river** | **Bloom**: hold power on a root, spring or gate; **Touch the river** |
- Cheapest route (PROPOSED): one free humanoid asset (e.g. Unity Starter Assets or Mixamo), two material sets, two sets of idle and walk animations.
- Walk speed 4 m/s (Present 3.2 m/s, Future 4.5 m/s) (PROPOSED).

## 4. The two worlds [DESIGN/ART]
Different layouts, joined by the Memory River. Each world is one small playable area of about 40 x 40 m.

### World A: The Pale Desert (the Present)
Empty desert, pale beige and grey, low sun, faint haze. Sand dunes, broken objects, buried memories (half-buried chair, picture frames, a cracked doll). Lonely and melancholic. The **Memory River bed** is dry and runs along the east side.

### World B: The Green Valley (the Future)
Lush green forest, glowing plants, magical roots and trees, golden light. A **stream** comes down from a spring on the west side and joins the Memory River. A wide **Gap** (ravine) cuts through the middle.

### The Memory River [DESIGN]
One river that exists in both worlds. Four roles, all included:
1. **Communication:** a player touches the water (interact); glowing ripples appear at the same place in the other world.
2. **Object transfer:** an object dropped in the river appears on the other world's bank.
3. **Flow control:** the Future's **Water Gate** controls how much water flows into the desert half.
4. **Brightens with progress:** the river glows more and more as fragments are collected.

## 5. Puzzles [DESIGN]
Chain: each puzzle unlocks the next. Target times add up to about 5 minutes.

### Puzzle 1: The Dry Bed (about 1:15) *teaches the river, communication, flow control*
| Object | World | Description |
|---|---|---|
| **River Stone** | Desert | Glowing stone beside the dry bed. Touching it makes ripples in the forest. |
| **Water Gate** | Forest | Root-wheel gate in the stream. Hold power to open. |
| **Dry Bed** | Desert | The empty river channel; fills with water when the gate is open. |
| **Wet Sand** | Desert | Sand that darkens along the bed; mounds nearby become diggable. |
1. The Present touches the **River Stone**; ripples appear at the **Water Gate** in the forest (the signal).
2. The Future holds power on the **Water Gate**; the stream flows down the Memory River.
3. The **Dry Bed** in the desert fills with water, plants sprout, and the surrounding sand turns soft (diggable).
Result: Puzzle 2 unlocks. Desert gets a little greener along the river.

### Puzzle 2: The Broken Bridge (about 1:45) *the main cooperative puzzle*
| Object | World | Description |
|---|---|---|
| **Sand Mounds** (x3) | Desert | Mounds that hide the Ancient Path stones and a buried seed. |
| **Ancient Path** | Desert | Half-buried stone path leading to the Chasm edge. |
| **Memory Seed** | Desert | Glowing seed found in the last mound; a carryable object. |
| **Root Socket** | Forest | Stone socket on the Gap's edge; accepts the Memory Seed. |
| **Bridge Roots** | Forest | Two root stumps on opposite sides of the Gap; extend into a bridge. |
| **The Chasm / The Gap** | Both | The same ravine in both worlds, mirrored. |
1. The Present holds power on each **Sand Mound**; it sinks, revealing the **Ancient Path**.
2. The last mound reveals the **Memory Seed**. The Present picks it up and drops it in the **Memory River**.
3. The seed appears on the forest bank. The Future picks it up and places it in the **Root Socket**.
4. The **Bridge Roots** extend across the **Gap** (about 5 seconds); the **Ancient Path** in the desert becomes a solid bridge across the **Chasm**.
Result: the way to the shrine opens. Neither player can do it alone.

### Puzzle 3: The Mirror Fragment (about 1:00) *cooperation under time*
| Object | World | Description |
|---|---|---|
| **Fragment Shrine** | Desert | Stone altar across the Chasm, under a final mound. |
| **Fragment Mound** | Desert | Large sand mound that sinks only while the river is flowing. |
| **Mirror Fragment** | Desert | The first shard of the broken mirror. |
1. The Present crosses the bridge and holds power on the **Fragment Mound**.
2. The mound only sinks while the river is flowing: the Future must hold the **Water Gate** open at the same time. The Present touches the **River Stone** to signal "now".
3. The mound sinks; the Present takes the **Mirror Fragment**.
Collecting the fragment (both screens): glow burst, chime, the mirror HUD icon updates, the desert gets slightly brighter, the river glows brighter, and a short memory plays: *Recognizing pain*. A faded still of the person sitting alone by a window, with the line **"You were tired. You were allowed to be."** (PROPOSED).

## 6. Opening sequence (about 0:45, in-engine) [DESIGN/ART/AUDIO]
| Time | Shot |
|---|---|
| 0:00-0:15 | A small room with one mirror. The Present stands in front of it. The reflection is faded and slightly out of focus. Quiet, low tone. |
| 0:15-0:25 | A thin crack appears in the mirror. The camera pushes in. |
| 0:25-0:35 | The mirror shatters into shards, slow motion. White flash, then black. |
| 0:35-0:45 | The screen splits in two and fades in: desert on the left, forest on the right. Title **TWO WORLDS**. Control starts. |

## 7. Ending sequence (about 0:45) [DESIGN/ART/AUDIO]
| Time | Shot |
|---|---|
| 0:00-0:10 | The mirror fragments appear and drift together in front of both cameras. One shard is glowing; the other pieces are faint outlines (the journey continues). |
| 0:10-0:20 | The pieces join into one mirror. The Present and the Future walk toward the mirror from their own sides. |
| 0:20-0:35 | The reflection shows **one person**, the same face blending from Present to Future. |
| 0:35-0:45 | Text: **"Loving yourself is a journey. You don't have to become someone else to deserve it."** Fade out, **Play again / Quit**. |

## 8. Camera and controls [TECH]
Same as our other project: free orbit third-person camera on the right stick, vertical split-screen (Present left, Future right), left stick move, **A** interact, hold **X** or right trigger for power, **Start** pause. Keyboard fallback for development.

## 9. Environment changes with progress [VFX/ART]
| After | Change |
|---|---|
| Puzzle 1 | Desert bed shows water, small plants; wet sand darkens |
| Puzzle 2 | Bridge and path glow; first colour returns to desert props |
| Puzzle 3 (fragment) | Desert noticeably brighter (post-processing saturation +, fog thinner); river glowing |
Light and light-redirecting is **decorative only**: no light puzzle.

## 10. Audio [AUDIO]
| Layer | Description |
|---|---|
| Desert bed | Soft wind, sparse low piano, distant hum. |
| Forest bed | Birdsong, water, soft strings and harp. |
| Memory River | A gentle hum that grows richer with progress. |
| Music | One short piece that starts as the desert theme and blends into the forest theme at the end. |
SFX: mirror crack, mirror shatter, ripple touch, water gate open, water flowing, sand sinking (hold loop), seed pickup, seed splash in the river, seed appearing, roots growing, fragment collect (chime), memory sting, mirror assembling, UI click.

## 11. UI [UI]
- Light, auto-hiding prompts: button icon plus short verb (e.g. "Touch the stone", "Hold to dig").
- Hold-progress ring above mounds, gates and sockets.
- Mirror icon in a corner showing the fragment collected (1 of 5 slots).
- Start screen: **Play**, **Controls**, **Quit**. Pause: **Resume**, **Restart**, **Quit**.
- Short controls screen before play (both characters' buttons).

## 12. Teaching and hints [DESIGN/UI]
- First 20 seconds: a movement tip per half.
- Carved or glowing icons near each interactable.
- After 60 seconds without progress, the next object pulses softly in both worlds.

## 13. Timing and balance [DESIGN]
| Part | Target |
|---|---|
| Opening | 0:45 |
| Puzzle 1 | 1:15 |
| Puzzle 2 | 1:45 |
| Puzzle 3 | 1:00 |
| Ending | 0:45 |
| **Total** | **about 5:30** |
Dig time per mound 3 s. Root growth 5 s. Water Gate opens in 2 s. Fragment mound needs about 3 s of overlapping hold.

## 14. Asset list [ART]
Humanoid character (free asset) with two material sets and two animation sets; desert dunes and props (half-buried chair, picture frames, broken pots, dead tree); forest trees, glowing plants, roots, rocks; River Stone, Water Gate, Root Socket, Bridge Roots, Ancient Path stones, sand mounds (small and large), Memory Seed, Mirror Fragment, Fragment Shrine; mirror room with mirror and shard pieces; river water shader (two materials, dry and wet).
Our Kyan3D Blender models (ruins, stones, tree) can be reused where they fit.

## 15. VFX list [VFX]
Mirror crack and shatter; sand sink dust; water flow and splash; ripples; plant sprouting; root growth; fragment glow burst; memory vignette; desert brightening; river glow; mirror assemble; fireflies and ash/dust motes.

## 16. Technical notes [TECH]
- One scene, two areas far apart, two cameras with separate culling and separate post-processing.
- Scripts: PlayerController, Interactable, SandMound, WaterGate, RiverTouch, RiverTransfer, RootSocket, BridgeRoots, FragmentPickup, WorldProgress (brightening), OpeningSequence, EndingSequence, HUD.
- Mirrored world state shared through one manager.
- Windows build tested on a second computer if possible.

## 17. Two-day schedule mapping [DESIGN]
**Day 1:** project and scene, both worlds blocked out, characters, split screen, movement, interaction, river, abilities, Puzzles 1-3 working with placeholder art.
**Day 2:** opening, effects, brightening, ending, sound and music, UI, full test, bug fixing, build.

## 18. Scope cuts if time runs out (in this order) [DESIGN]
1. Memory still image (use a colour flash and the text only).
2. Ripple and transfer visuals (keep the logic).
3. Animation variety (one idle and one walk each).
4. Ending reveal (a simple blend of the two portraits).

## 19. Decisions I made that need your approval (PROPOSED)
1. Puzzle 1-3 details, including that wet sand is required before digging.
2. Puzzle 3 cooperation: the mound sinks only while the river flows.
3. Walk speeds, dig time, growth time and gate time.
4. The memory line "You were tired. You were allowed to be."
5. The ending showing one glowing shard and four faint outline slots.
6. Names: Pale Desert, Green Valley, Memory River, River Stone, Water Gate, Root Socket, Bridge Roots, Memory Seed, Fragment Shrine.
