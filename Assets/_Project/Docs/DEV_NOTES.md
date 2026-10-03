# RICOCHET — Development Notes

AR laser survival shooter · Unity 6000.4.5f1 · AR Foundation 6.4 · ARCore · URP · Android (Samsung Galaxy A26)
Author: Eseosa Kay-Uwagboe Pascal

---

## Milestone 1 — Project config & AR rig

**What was built**
- Folder structure under `Assets/_Project/` (Scenes, Prefabs, Materials, Textures, Audio, Models, Docs, ScriptableObjects, Scripts/{Core, AR, Pooling, Player, Weapons, Enemies, Cards, UI, Audio, Data, Editor}). All original work lives here, separate from the AR Mobile template content.
- New single scene `Assets/_Project/Scenes/Game.unity` (the only scene in the build). Menus are UI panels, not separate scenes, so the AR session and the placed arena never get torn down.
- Hierarchy:
  - `AR Session`: owns the ARCore session lifecycle.
  - `XR Origin (Mobile AR)`: `ARPlaneManager` (horizontal + vertical), `ARRaycastManager` (tap/reticle hit tests), `ARAnchorManager` (anchors the arena). Child `Main Camera` (tag MainCamera, near clip 0.05 m) with `ARCameraManager`, `ARCameraBackground`, `TrackedPoseDriver`.
  - `Directional Light` (no shadows, for mobile performance).
  - `EventSystem` with `InputSystemUIInputModule` (New Input System).
  - `Managers`: empty parent for the game's manager objects.
- I chose a clean rig over the template's `XR Origin (AR Rig)` because the template rig contains XR Interaction Toolkit interactors (object spawner, ray interactor, pinch/rotate) that would compete with our own touch input.

**Physics layers**

| # | Layer | Purpose |
|---|---|---|
| 8 | ARFloor | Detected floor planes (reticle/placement raycasts, spawn validation) |
| 9 | ReflectiveWall | Detected wall planes; lasers bounce off |
| 10 | Mirror | Player-placed mirrors; lasers bounce off |
| 11 | Prism | Splits lasers |
| 12 | Enemy | Enemy hit colliders |
| 13 | PlayerHurtbox | Sphere on the camera that receives damage |
| 14 | PlayerProjectile | Laser bolts |
| 15 | EnemyProjectile | Acid globs |

Collision matrix: PlayerProjectile ignores PlayerHurtbox/PlayerProjectile/EnemyProjectile; EnemyProjectile ignores Enemy/EnemyProjectile; Enemy ignores Enemy. Projectiles also use explicit LayerMasks in their SphereCasts, so they can never hit their own side.

**Settings**
- Player: Company `Eseosa`, Product `Ricochet`, package `com.eseosa.ricochet`, IL2CPP, ARM64 only, Portrait, Unity splash on, Min API 30.
- Active Input Handling: Input System Package (New).
- XR Plug-in Management: Android → Google ARCore; Windows → XR Simulation (editor testing).
- ARCore: Requirement = Required, Depth = Optional.
