# Joe Timeline Workflow

Joe should be driven mainly from Timeline. The code is support tooling, not the thing creating the performance.

## Animation

- Put Joe's body-performance clips on the `Joe Animation - replace clips here` track.
- Replace the placeholder clips with real clips: run, jump, vault, climb, drop, stumble, stop.
- For the chase, Timeline animation clips should be the main source of motion. Keyframe Joe or use root-motion clips where needed.
- If Joe is being hand-authored in Timeline, set his controller mode to `External` so the controller does not fight the animation.

## Movement

- Use normal Timeline animation/keyframes for most shots.
- Use `JoeMovementTrack` only when you want the controller to move Joe automatically:
  - `NavMesh`: Joe moves toward a target.
  - `Spline`: Joe follows a spline.
  - `RootMotion`: Joe uses animation root motion for a timed beat.
  - `External`: Timeline or another script owns Joe.
- For cinematic animation work, prefer `External` unless there is a specific reason to use NavMesh or spline.

## IK

- Use the `Joe Cues - triggers and IK` track.
- Add a `JoeCueClip` where Joe needs a head look, hand placement, foot IK weight change, or animator trigger.
- Drag scene targets into the cue clip fields:
  - `Look Target` for head/eye direction.
  - `Left Hand Target` and `Right Hand Target` for vaults, climbing, reaching, touching walls, etc.
- Set weights:
  - `Look Weight`: `0` means no look IK, `1` means fully look at target.
  - `Hand Weight`: `0` means hands follow animation, `1` means hands stick to targets.
  - `Foot Weight`: usually around `0.8` or `0.9`; lower it during jumps/vaults so the feet do not stick to the ground.

## Animator Triggers

- In a `JoeCueClip`, set `Animator Trigger` to fire a trigger like `Jump`, `Vault`, `Climb`, `Stumble`, `RunStart`, or `RunStop`.
- The trigger only fires once when the cue starts.

## Shadow

- The shadow character is a duplicate Joe.
- Its renderers are set to `ShadowsOnly`, so it only casts the shadow.
- It should have its own animation track, usually mirroring or slightly offsetting Joe's chase animation.

## Cameras

- Use the `Camera Shots` Cinemachine track.
- Treat the generated cameras as rough blocking only. Move them, replace them, or add new cameras as needed.

## Recommended Chase Workflow

1. Block the chase with rough run/jump/vault/climb clips.
2. Put markers where each action happens.
3. Add IK targets for hands and look direction.
4. Add `JoeCueClip`s over moments where hands/head/feet need help.
5. Polish the camera cuts last, after the body motion reads clearly.
