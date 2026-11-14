# Puzzle 2: The Dry Bed

Second puzzle of *Two Worlds*. Follows the same rules as Puzzle 1 (The Broken Bridge) in `TwoWorlds_Design_and_Development.pdf`: flat-colour primitives, the same 40 × 40 m layout (forest = desert shifted 300 m along X), the scene generated from code, the same HUD, light beams, hold bar, respawn and fragment cutscene.

| Item | Value |
|---|---|
| Scene | `Assets/Scenes/Sh/Puzzle2.unity` (loaded by Puzzle 1's fragment cutscene) |
| Builder | **Tools → Kyan → Build Dry Bed Puzzle (Puzzle 2)**, `Scripts/Editor/DryBedPuzzleBuilder.cs` |
| Fragment | 2 of 5, **Accepting the past**, collected by the **Future** (roles swap from Puzzle 1) |
| Length | About 2 minutes |
| River jobs | Communication (ripples), object transfer, flow control, brightening |
| Next scene | `Puzzle3` (still a placeholder) |

## Who does what, in order

1. **Present signals.** The Memory River bed is dry. The Present touches it with empty hands: ripples appear in both worlds and the forest's **Root Wheel** starts to glow.
2. **Future opens the spring.** Hold Power on the Root Wheel for 5 s. The **Spring Gate** sinks and water fills the river bed in both worlds over 6 s.
3. **Present digs.** The 3 **Sand Mounds** beside the river were too hard to dig; once the water arrives they turn dark and soft. Hold Power on each for 3 s (any order). The last one uncovers a broken **Picture Frame**.
4. **Present sends the frame.** Pick it up, walk to the river, press Interact. It appears on the forest bank.
5. **Future places it.** Carry the frame to the **Frame Pedestal** and press Interact. The **Root Cage** around the forest altar sinks over 3 s.
6. **Future takes the fragment.** The Mirror Fragment on the forest altar can now be collected. Fragment cutscene: a flower grows beside the Future, then the note.

> *It happened. I can't change it. / I can carry it without letting it carry me. / The past is part of me, and it is not all of me.*

After the fragment, the desert river turns bright cyan and the spring glows (brightening).

## Layout (local x, z, same in both worlds unless noted)

| Landmark | Position | Desert | Forest |
|---|---|---|---|
| Play area | x, z from -20 to 20, invisible walls | Sand | Grass |
| Spawn | (0, -15) | Present | Future |
| Memory River | x = 14, z from -20 to 3 | Dry bed, fills with dull water | Dry bed, fills with bright water |
| Spring Gate | (14, -18.4) | n/a | Stone slab in front of the spring rocks |
| Root Wheel | (10.4, -17) | n/a | Pedestal with a standing root wheel |
| Sand Mounds × 3 | (8.2, -12), (9.4, -7.2), (8.0, -2.5) | Wet patches spread under them | n/a |
| Frame Pedestal | (-3.4, 2.4) | n/a | Stone pedestal with a glowing ring |
| Altar | (-6, 15.5) | Empty, dim pink glow | Fragment inside 8 cage roots |
| Memory Flower | (-3.4, 13.6) | n/a | Grows in the cutscene |

## Tuning (Inspector)

| Object | Setting |
|---|---|
| Root Wheel | `turnTime` 5 s, range 2.4 m |
| River fill | `fillTime` 6 s (on `DryBedPuzzle`) |
| Sand Mound × 3 | `digTime` 3 s, range 2.6 m |
| Picture Frame | range 2 m |
| Frame Pedestal | range 2.3 m |
| Root Cage | sinks 3.6 m over 3 s |
| Mirror Fragment | range 2.2 m |

## Scripts

| Script | Job |
|---|---|
| `DryBedPuzzle` | Shared state of both worlds, river fill, cage, guidance beams, messages |
| `RootWheel` | Future: stuck until woken, then hold Power to open the spring |
| `WetSandMound` | A `SandMound` that can't be dug until the river has flowed |
| `FramePedestal` | Future: takes the Picture Frame and opens the cage |

Small additive hooks were added to shared scripts so Puzzle 2 can listen without touching Puzzle 1's logic: `SandMound.onDug`, `MemoryRiver.onTouched` and `onCrossed`, `MirrorFragment.onCollected`. `BridgePuzzleBuilder` became a `partial` class so the new builder reuses its helpers.

Messages are in `DryBedPuzzle.cs`.

## Play-test checklist

- [ ] Present touches the dry river: ripples in both worlds, the root wheel glows
- [ ] Before that, the wheel says it won't budge and shows no hold bar
- [ ] Future holds Power: the bar fills, the wheel spins, the gate sinks, water flows down in both worlds
- [ ] Before the water arrives, mounds say the sand is too hard; afterwards they turn dark and can be dug
- [ ] The last mound reveals the picture frame; it can be picked up, put down, and sent through the river
- [ ] Future places the frame: the cage roots sink, the fragment light brightens
- [ ] Future takes the fragment: the cutscene plays in the forest, then Puzzle 3 loads
- [ ] Light beams always point each player at the next step
- [ ] Console shows no errors
