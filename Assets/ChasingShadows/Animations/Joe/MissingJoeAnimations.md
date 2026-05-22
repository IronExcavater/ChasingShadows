# Joe Animation Coverage

Updated: 2026-05-06

## Newly covered

The Downloads packs have been copied into the Joe animation folder, renamed to title-case qualitative names, and wired into the Joe setup. The setup tool now keeps those clips flat under:

- `Assets/ChasingShadows/Animations/Joe/`

All Joe FBX animation importers are set to Humanoid, `Copy From Other Model`, using the humanoid avatar from `Assets/ChasingShadows/Characters/Joe/Joe.fbx`. Base movement now has idle, forward walk/run, backward walk/run, left/right strafe walk, and left/right strafe run coverage through `Joe_Cinematic.controller`. The chase timeline now uses real clips for run start, run arcs, look-back, jump, turn, stop-at-wall, climb, drop, land, vault, stumble, edge slip, trip, fall impact, and knocked-out hold where usable clips exist.

## Still missing

- Additive leaning poses or clips:
  - `Additive_Lean_Forward`
  - `Additive_Lean_Back`
  - `Additive_Lean_Left`
  - `Additive_Lean_Right`
  - Optional sharper run-corner leans: `Run_Corner_Lean_Left`, `Run_Corner_Lean_Right`

- Diagonal locomotion clips:
  - `Walk_Forward_Left`, `Walk_Forward_Right`
  - `Walk_Back_Left`, `Walk_Back_Right`
  - `Run_Forward_Left`, `Run_Forward_Right`
  - `Run_Back_Left`, `Run_Back_Right`

- A clean knockout transition:
  - A single clip that starts from a stumble/trip impact and settles into an unconscious ground pose.
  - Current setup bridges this with falling/roll plus sleeping idle clips, which is usable for blocking but not a polished knockout.

- Optional polish clips:
  - Direction-specific additive lean poses for sharp chase corners.
  - Wall-climb ledge top-out from hang to standing if the current `Hanging Wall Ascend` does not line up with the target wall.
  - Short stumble recovery variants for near-miss chase beats beyond the current `Jogging Stumble`, `Vault Stumble`, and `Edge Slip` coverage.
