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

---

## How the project is generated (Editor builder)
`Scripts/Editor/RicochetBuilder*.cs` adds the menu **Ricochet → Build Everything**, which generates everything below. It is repeatable: asset GUIDs are kept, and tuning ScriptableObjects are only created once, so later inspector tweaks survive a rebuild.
- `TextureFactory`: draws textures pixel by pixel. The floor texture uses a custom 5x7 pixel font to print the full name on a dark band.
- `SfxSynth`: synthesizes every sound and writes WAV files.
- Materials: URP Unlit, Simple Lit and Particles/Unlit, configured in code (opaque, alpha or additive).
- Prefabs: placeholder models made from primitives. The visuals are always children of the prefab root and the scripts sit on the root, so real models can be swapped in without touching code.

---

## Milestone 2: Custom name plane, reticle, tap-to-place, walls
**Classes:** `ARPlaneStyler`, `ARPlacementController`, `ArenaContext`, `UIHitTest`

- `P_RicochetPlane` has the same components as *XR → AR Default Plane* (ARPlane, ARPlaneMeshVisualizer, MeshFilter, MeshRenderer, MeshCollider, LineRenderer) plus `ARPlaneStyler`. It is assigned to `ARPlaneManager.planePrefab`, so the visual only exists once ARCore detects a plane.
- `ARPlaneStyler` classifies each plane by `alignment`:
  - **Floor** (HorizontalUp): `M_FloorName`, layer `ARFloor`. The name is tiled 2x2 per metre, so it appears every 50 cm.
  - **Wall** (Vertical): `M_WallGrid`, layer `ReflectiveWall`. Its MeshCollider is what lasers bounce off.
  - **Other** (ceilings, tilted): hidden with `forceRenderingOff` and the collider is disabled. `ARPlaneMeshVisualizer` toggles `renderer.enabled` every frame, so `enabled` can't be used for hiding.
  - `SetDimmed()` fades the visual with a `MaterialPropertyBlock` (no material copies). Colliders stay active.
  - A static `WallCountChanged` event drives the HUD hint "WALLS: N".
- `ARPlacementController`:
  - Ray-casts from the screen centre and accepts only HorizontalUp planes that are at least 0.6 m on their smallest side.
  - The first touch that is not over UI places `P_Arena`, anchored with `ARAnchorManager.AttachAnchor(plane, pose)` and rotated to face the player.
  - `HasArena` blocks every later placement.
  - After placement it sets `requestedDetectionMode = Vertical` (stop finding floors, keep finding walls), dims the floors and raises `ArenaPlaced(Transform, ARPlane)`.
  - Placement is only enabled by `ScanningState`.
- `ArenaContext` (on the arena root) answers spatial questions: floor height, `ProjectToFloor`, and `IsOnFloor` (ray-cast down onto ARFloor, falling back to a radius around the beacon).
- Input: the New Input System **EnhancedTouch**, with `TouchSimulation` in the editor. Touches over UI are always ignored.

## Milestone 3: Core architecture
**Classes:** `GameManager` (Singleton), `GameStateMachine`, `IGameState`/`GameStateBase`, the 7 states, `GameEvents`, `RoundTimer`, `ScoreSystem`, `ObjectPool<T>`, `IPoolable`, `PoolRegistry`. ScriptableObjects: `DifficultySettings` (x3), `EnemyStats` (x2), `WeaponStats`, `SoundLibrary`.

- **State pattern:** each state switches its systems on in `Enter()` and off in `Exit()`. For example, Scanning turns placement on, and Playing turns on the spawner, weapon and cards. `GameStateMachine.StateChanged` notifies the `UIManager`.
- **Singleton:** `GameManager` destroys duplicates. It is the single entry point for UI commands (StartGame, Pause, Restart...).
- **Observer:** C# events throughout. Gameplay never references UI.
- **Object Pool:** `ObjectPool<T> where T : Component, IPoolable`.
  - All instances are pre-warmed in `Awake`.
  - `Get()` activates an instance and calls `OnSpawned()`, which resets all of its state. `Release()` calls `OnDespawned()` and deactivates it.
  - If a pool runs empty it grows by one and logs a warning.
  - `PoolRegistry` lists every pool. The pause screen shows `active/total, grown`, which proves nothing is instantiated during play.
- Editor debug keys: F1 menu, F2 start, F4 win, F5 die, P pause, B debug-place, Space fire.

## Milestone 4: UI
**Classes:** `UIPanel` (abstract), `UIManager`, `MainMenuPanel`, `LeaderboardPanel`, `LeaderboardRow`, `ScanPanel`, `CountdownPanel`, `HUDPanel`, `PausePanel`, `EndPanel`, `FireButton`, `SafeArea`, `FloatingText`, `FloatingTextPool`

- One Screen Space Overlay canvas (Scale With Screen Size 1080x1920, match 0.5), with everything inside a `SafeArea` rect.
- `UIManager` listens to `StateChanged` and shows exactly one panel per state. It also adds a click sound to every button.
- Flow: Menu → Start → Scanning (first time only) → Countdown → Playing → End. Restart skips scanning if the arena exists. Main Menu keeps the arena.
- The difficulty and vibration settings are saved in PlayerPrefs (`DifficultyStore`, `GameSettings`).

## Milestone 5: Player and laser
**Classes:** `PlayerController`, `PlayerHealth` (IDamageable), `DamageFeedback`, `LaserBlaster`, `LaserBolt` (IPoolable), `HeatSystem`, `SpreadPattern`, `VfxManager`

- The AR camera is the player. Its `Hurtbox` child is a SphereCollider (r = 0.25 m) with a kinematic Rigidbody, on layer PlayerHurtbox.
- Damage feedback:
  - The HUD shows a red vignette.
  - The gun viewmodel shakes. The AR camera itself is never moved.
  - `Handheld.Vibrate()` fires if vibration is enabled.
  - A hurt sound plays.
- Death: death sound, then `Time.timeScale = 0.35` for 0.9 s of unscaled time while the screen fades red, then the End screen.
- `LaserBlaster`:
  - Hold-to-fire at 5 shots/s.
  - Aims from the muzzle to whatever is under the crosshair.
  - Spread tiers Single/Twin/Tri/Quad, with bolts 8° apart.
  - Heat: +12% per shot, cools 35%/s, overheating locks firing for 1.5 s.
- `LaserBolt` (pool of 40):
  - Moves with `Physics.SphereCast` from the last position to the next one. No Rigidbody and no tunnelling.
  - Wall/Mirror hits use `Vector3.Reflect` (max 3 bounces).
  - Prism hits pass through and split once.
  - Enemy hits deal exactly 1 damage plus the ricochet multiplier (x1 / x1.5 / x2 / x3).
  - `OnSpawned` resets the bounces, mask and split flag, and calls `trail.Clear()`.
- `Physics.queriesHitBackfaces = true`, so AR walls are hit from either side.
- `VfxManager` emits particles with `ParticleSystem.Emit`, so no objects are spawned for effects.

## Milestone 6: Enemies
**Classes:** `Enemy` (abstract, IPoolable, IDamageable), `WalkerEnemy`, `SpitterEnemy`, `AcidGlob`, `EnemyFactory`, `EnemySpawner`, `EnemyContext`, `HitFlash`

- **Inheritance and polymorphism:** `Enemy` contains all shared behaviour:
  - health
  - rising out of the floor (scaled up from the feet pivot over 0.6 s)
  - moving on the floor toward the player and facing them
  - knockback, hit flash and slow effects
  - death, and returning to the pool

  Subclasses override `TickBehaviour()` and `Attack()`.
- **Walker:** 3 hits, 0.5 m/s, claw range 0.8 m, 10 damage, 1.5 s cooldown, 100 points. A 0.3 s wind-up means damage only lands if the player is still close. It plays the WalkerAttackHit sound.
- **Spitter:** 5 hits, 0.4 m/s, stops at 3 m, pooled acid globs at 3 m/s for 8 damage on a 2.5 s cooldown, 150 points. Its mouth swells before it fires.
- **Factory:** `EnemyFactory.Create(EnemyType, position)` hides which prefab and pool are used. It pools 12 of each enemy type and 20 acid globs.
- `EnemySpawner`: the spawn interval and Spitter chance are interpolated over the round from `DifficultySettings`, and it respects the max-alive limit. Spawn points are 2.5–4 m from the player and validated on the floor plane, with the beacon radius as a fallback.
- `HitFlash`: a per-material `MaterialPropertyBlock` with an HDR white flash and a blue tint while frozen. It works with real models too.

## Milestone 7: Score, timer, win/lose
- Kill score = base score x ricochet multiplier. Survival adds +5 per second. A win adds +500.
- Win when the timer reaches 0 (SURVIVED). Lose when health reaches 0 (OVERRUN).
- `GameOverState` wipes every enemy, projectile, card and gadget back to its pool.

## Milestone 8: Leaderboard
- `SessionResult` is `[Serializable]` and wrapped in `SessionResultList` for `JsonUtility`.
- `LeaderboardService` saves to `Application.persistentDataPath/leaderboard.json`.
  - A new result is inserted at the front and the list is trimmed to 5 (the latest 5, not the top 5).
  - A missing or corrupt file gives an empty board.
- The best score is kept in PlayerPrefs and used for "NEW BEST!".

## Milestone 9: Audio
- `AudioManager` (Singleton) has 1 looping music source, 1 2D one-shot source, and a pool of 10 3D sources moved to the event position.
- **No AudioSource on enemies or bullets**, so there are no duplicated components.
- `SoundLibrary` (ScriptableObject) maps each `SoundId` to clips, volume, pitch range and a 2D/3D flag.

| SoundId | File | Plays when |
|---|---|---|
| PlayerShoot (required) | SFX_PlayerShoot | trigger pull |
| PlayerDeath (required) | SFX_PlayerDeath | health reaches 0 |
| EnemySpawn (required) | SFX_EnemySpawn | enemy rises (3D) |
| SpitterShoot (required) | SFX_SpitterShoot | Spitter fires (3D) |
| WalkerAttackHit (required) | SFX_WalkerAttackHit | Walker claw damages player |
| LaserBounce | SFX_LaserBounce | bolt reflects (3D) |
| EnemyHit | SFX_EnemyHit | bolt hits enemy (3D) |
| EnemyDeath | SFX_EnemyDeath | enemy dies (3D) |
| PlayerHurt | SFX_PlayerHurt | player damaged |
| CardPickup | SFX_CardPickup | card collected |
| MirrorPlace | SFX_MirrorPlace | gadget placed (3D) |
| Overheat | SFX_Overheat | gun overheats |
| CountdownBeep / Go | SFX_CountdownBeep/Go | 3-2-1-GO |
| RoundWin | SFX_RoundWin | survived |
| UIClick | SFX_UIClick | any button |
| Music | MUS_Loop | background loop |

**Source and licence of every clip:** procedurally synthesized by `SfxSynth.cs`, written for this project. This is original work, so no third-party licence applies.

## Milestone 10: Ability cards, Mirror, Prism
**Classes:** `AbilityCard` (abstract), `MultiShotCard`, `MirrorCard`, `PrismCard`, `FreezeCard`, `MedKitCard`, `PlayerContext`, `CardSpawner`, `AbilityPlacer`, `PlacedGadget` (abstract), `Mirror`, `Prism`

- **Polymorphism:** `CardSpawner` calls `card.Activate(PlayerContext)` without knowing which card it is.
- Cards spawn every 20 s within 1.5 m of the beacon (pooled, max 2 on the floor, expire after 18 s). The player collects one by walking until the camera is within 0.5 m horizontally.
- What each card does:
  - **Multi-Shot**: spread +1 tier for 12 s.
  - **Mirror / Prism**: +1 charge, placed with HUD buttons at the crosshair, facing the camera.
  - **Freeze Pulse**: enemies within 2 m are 70% slower for 4 s.
  - **Med Kit**: +30 HP.
- The Mirror reflects lasers like a wall, so the game is fully playable with zero detected walls. The Prism splits a bolt into 3 (±25°), and each bolt can only split once. Both are pooled and last 30 s.
