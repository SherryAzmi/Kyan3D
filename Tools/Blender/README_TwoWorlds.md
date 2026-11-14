# Two Worlds model generator

`build_twoworlds_models.py` builds the Two Worlds environment, props and characters in Blender and exports one FBX each to `Assets/TwoWorlds/Models/`.

```
blender -b -P Tools/Blender/build_twoworlds_models.py -- --preview
blender -b -P Tools/Blender/build_twoworlds_models.py -- --only Human_Present,RootBridge
```

- 1 unit = 1 metre, pivots at the base, modelled facing -Y (+Z in Unity). `--preview` writes PNGs to `previews_twoworlds/`.
- **Terrains** (60 x 60 m, centred): `DesertTerrain`, `ForestTerrain`. Three material slots each: ground, river/stream bed, chasm rock. The chasm/gap is the same strip (Y 14 to 19 in Blender, Z 14 to 19 in Unity) in both worlds. The Memory River runs along X = 20. Water strips: `RiverWater`, `StreamWater`.
- **Characters**: `Human_Present` (slouched, grey) and `Human_Future` (upright, colourful). Joints are empties (`Hip_L`, `Knee_L`, `Shoulder_L`, `Elbow_L`, `Spine`, `Neck`) so Unity scripts can swing limbs for a simple walk.
- **Puzzle objects**: `SandMound_Small/Large`, `AncientPath`, `AncientBridge`, `RiverStone`, `WaterGate` (`Gate`, `Wheel`), `RootSocket`, `MemorySeed`, `RootBridge` (segments `Root_00` to `Root_11` to grow in order), `MirrorFragment`, `FragmentShrine`.
- **Opening**: `MirrorRoom`, `Mirror_Intact`, `Mirror_Shards` (`Shard00` to `Shard19`).
- **Scenery**: desert (rock, chair, picture frame, pot, dead tree) and forest (two trees, bush, fern, glow plant, mushroom, mossy rock, fallen log).
- Materials to remap in Unity: `M_DesertSand`, `M_WetSand`, `M_ForestGround`, `M_RiverWater`, `M_GoldGlow`, `M_ForestGlow`, `M_MirrorGlass`, and the `M_*_Present` / `M_*_Future` human materials.
- First-pass low-poly models, made to be edited and improved by the team. Positions do not match the PDF's 40 x 40 m layout; place them by hand or from the builder scripts.

## Style
Flat-colour low-poly, no textures, to match `Docs/TwoWorlds_Design_and_Development.pdf`. This is the default. Pass `--smooth` for the optional smoother, finer version (subdivision and surface noise; still no textures).

## Characters (from the art team)
The two playable characters are **not** generated here any more. They come from `Art/Source/Characters_v2.blend` (rigged, 19-bone humanoid skeleton, no animations yet): `Player_Depressed` is the Present and `Player_Light` is the Future. Export with:

```
blender -b Art/Source/Characters_v2.blend -P Tools/Blender/export_characters.py -- --preview
```

This writes `Assets/TwoWorlds/Characters/Character_Present.fbx` and `Character_Future.fbx`. In Unity, set each FBX's Rig to **Humanoid** (Animation Type > Humanoid) and use it as a replacement for the capsules. The PDF lists this as optional.Whoever edits the characters should edit the `.blend` and re-run the export.
