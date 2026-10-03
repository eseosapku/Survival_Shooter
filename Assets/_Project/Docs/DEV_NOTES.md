# RICOCHET: Development Notes

AR laser survival shooter · Unity 6000.4.5f1 · AR Foundation 6.4 · ARCore · URP · Android (Samsung Galaxy A26)
Author: Eseosa Kay-Uwagboe Pascal

---

## What makes it AR
- The game happens in the player's **real room**.
- The phone camera feed (`ARCameraBackground` + the URP `ARBackgroundRendererFeature`) is the background.
- ARCore detects the real floor and walls.
- The beacon is anchored to the real floor (`ARAnchor`).
- Zombies grow up out of that floor and walk across it toward the player.
- Lasers bounce off the real walls.
- The phone **is** the player: walking around moves you, and you walk over cards to collect them.
- In the editor there is no camera, so **XR Simulation** renders a virtual room as a stand-in. That is only for testing on a laptop.

---

## Code structure: 10 scripts (`Assets/_Project/Scripts`, namespace `Ricochet`)

| # | File | Unity component / asset | Plain C# classes inside |
|---|---|---|---|
| 1 | `GameManager.cs` | `GameManager` (Singleton) | `GameStateMachine`, `IGameState`, `GameStateBase`, 7 states, `RoundTimer`, `ScoreSystem`, `GameEvents`, `IDamageable`, `DamageInfo`, `SessionResult`, `LeaderboardService`, `GameSettings`, `Vfx`, `UIHitTest` |
| 2 | `GameConfig.cs` | `GameConfig` (ScriptableObject) | `DifficultySettings`, `EnemyStats`, `WeaponStats`, `CardSettings`, `SoundEntry`, `SoundId` |
| 3 | `ObjectPool.cs` | – | `ObjectPool<T>`, `IPoolable`, `PoolRegistry` |
| 4 | `ARController.cs` | `ARController` | `Arena` |
| 5 | `Player.cs` | `Player` (IDamageable) | `HeatSystem` |
| 6 | `Projectile.cs` | `Projectile` (IPoolable) | – |
| 7 | `Enemy.cs` | `Enemy` (IPoolable, IDamageable) | `EnemyAI` (abstract), `WalkerAI`, `SpitterAI`, `EnemyFactory`, `EnemySpawner`, `EnemyContext` |
| 8 | `CardSystem.cs` | – | `AbilityCard` (abstract) + 5 cards, `CardSystem`, `PlayerContext` |
| 9 | `AudioManager.cs` | `AudioManager` (Singleton) | – |
| 10 | `UIManager.cs` | `UIManager` | `UIPanel` (abstract) + 7 panels |

**Why so few files?** Unity requires each component or ScriptableObject class to live in a file with the same name, so there are 8 such files. Everything that doesn't need to be a component (states, AI behaviours, cards, panels, factory, spawner, services) is a plain C# class inside the file it belongs to. Plain classes are also cheaper: they have no GameObject and no Update overhead.

**Generated content:** all textures (including the name texture), sounds, materials, prefabs and the UI panels were generated once by an editor tool and saved as assets. The tool was then removed to keep the project to 10 scripts.

---

## OOP

| Principle | Where |
|---|---|
| **Encapsulation** | Tuning values are `private [SerializeField]` fields with read-only properties (`GameConfig`, `DifficultySettings`, `EnemyStats`...). State changes only happen through methods (`TakeDamage`, `Heal`, `AddPoints`). |
| **Abstraction** | `EnemyAI`, `AbilityCard`, `UIPanel`, `GameStateBase` (abstract classes); `IGameState`, `IPoolable`, `IDamageable` (interfaces) |
| **Inheritance** | `WalkerAI`/`SpitterAI : EnemyAI`; `MultiShotCard`, `MirrorCard`, `PrismCard`, `FreezeCard`, `MedKitCard : AbilityCard`; 7 panels `: UIPanel`; 7 states `: GameStateBase` |
| **Polymorphism** | `Enemy` calls `_ai.Tick()`, which does melee or ranged behaviour. `CardSystem` calls `card.Activate()`. Projectiles call `IDamageable.TakeDamage()` the same way on the player and on enemies. The state machine calls `Enter/Tick/Exit` on whichever state is current. |

## Design patterns

| Pattern | Where | Why |
|---|---|---|
| **Object Pool** | `ObjectPool<T>`: laser bolts (40), acid (20), Walkers (12), Spitters (12), cards (2 per type), mirrors/prisms (4), floating text (12), plus 10 pooled 3D audio sources | No Instantiate/Destroy during play, so no garbage-collection stutter. `OnSpawned` resets all state. The pause screen shows "grown 0" as proof. |
| **Singleton** | `GameManager`, `AudioManager` (duplicates destroy themselves) | One global access point |
| **State** | `GameStateMachine` + 7 `IGameState` classes | Each phase turns its own systems on in `Enter` and off in `Exit` |
| **Factory** | `EnemyFactory.Create(EnemyType, position)`; `EnemyAI.Create(type)` | Callers never see prefabs or pools |
| **Observer** | C# events: `Player.HealthChanged/Damaged/Died`, `ScoreSystem.ScoreChanged/PointsAwarded`, `RoundTimer.TimeChanged`, `HeatSystem.HeatChanged`, `GameEvents.EnemyKilled`, `ARController.ArenaPlaced/WallCountChanged`, `GameStateMachine.StateChanged` | The UI only listens, so gameplay never references UI |
| **Data-driven** | `GameConfig` ScriptableObject | Balance changes need no code |

---

## Feature notes

### AR (`ARController`)
- **Custom plane tracker:** `P_RicochetPlane` (ARPlane + ARPlaneMeshVisualizer + MeshCollider + LineRenderer) is assigned to `ARPlaneManager.planePrefab`. `ARController` listens to `trackablesChanged` and styles each plane:
  - **Floor** (HorizontalUp): `M_FloorName`, which shows the full name tiled every 50 cm, on layer ARFloor.
  - **Wall** (Vertical): `M_WallGrid` on layer ReflectiveWall. Lasers bounce off its collider.
  - **Ceiling / tilted:** hidden with `forceRenderingOff`, collider disabled.
- **Tap-to-place:**
  - The reticle follows floors at least 0.6 m in size at the screen centre.
  - The first touch that isn't over UI places the beacon with `ARAnchorManager.AttachAnchor(plane, pose)`, facing the player.
  - `HasArena` blocks any further placement.
  - Afterwards `requestedDetectionMode = Vertical` (no new floors, walls keep coming) and the floors are dimmed.
- `Arena`: floor height, `ProjectToFloor`, and `IsOnFloor` (ray-cast down onto ARFloor, falling back to a radius around the beacon).

### Player (`Player`)
- Hurtbox child: SphereCollider r = 0.25 m with a kinematic Rigidbody, on layer PlayerHurtbox.
- Damage feedback:
  - red vignette (HUD)
  - **gun shake** (the AR camera is never moved)
  - `Handheld.Vibrate()` (can be turned off)
  - hurt sound
- Death: death sound, 0.9 s of slow motion while the screen fades red, then the End screen.
- Blaster:
  - Hold-to-fire at 5 shots/s, aiming from the muzzle to whatever is under the crosshair.
  - Spread tiers Single/Twin/Tri/Quad (8° apart).
  - Heat: +12% per shot, cools 35%/s, overheating locks firing for 1.5 s.

### Projectile
- Moves with a per-frame `Physics.SphereCast`, so it never tunnels through walls and needs no Rigidbody.
- Laser:
  - Wall/Mirror: `Vector3.Reflect`, max 3 bounces.
  - Prism: splits into 3 (±25°), each bolt only once.
  - Enemy: exactly 1 damage, with a ×1/×1.5/×2/×3 multiplier depending on bounces.
- Acid hits the hurtbox.

### Enemies
| | Walker | Spitter |
|---|---|---|
| Hits to kill | 3 | 5 |
| Speed | 0.5 m/s | 0.4 m/s |
| Behaviour | walks to you; claw at 0.8 m (0.3 s wind-up) | stops at 3 m, spits pooled acid (3 m/s) |
| Damage / cooldown | 10 / 1.5 s | 8 / 2.5 s |
| Score | 100 | 150 |

- Both rise out of the floor over 0.6 s (scaled up from the feet).
- On hit: white flash (`MaterialPropertyBlock`), knockback, sparks and a hit sound.
- On death: a burst of particles and `GameEvents.EnemyKilled`.
- `EnemySpawner`:
  - The spawn interval shrinks and the Spitter chance grows linearly over the round.
  - The max-alive limit is respected.
  - Spawn points are 2.5–4 m from the player, mostly in front, validated on the floor.

### Difficulty (GameConfig)
| | Easy | Normal | Hard |
|---|---|---|---|
| Round | 120 s | 150 s | 180 s |
| Spawn interval | 3.5 → 1.8 s | 2.8 → 1.2 s | 2.0 → 0.8 s |
| Max alive | 6 | 9 | 12 |
| Spitter chance | 15→30% | 25→45% | 35→60% |
| Enemy damage | ×0.75 | ×1.0 | ×1.3 |
| Player HP | 120 | 100 | 80 |

### Score, win/lose
- Score: kill score × ricochet multiplier, plus 5 per second survived, plus 500 for a win.
- **SURVIVED** when the timer reaches 0. **OVERRUN** when health reaches 0.
- At game over, everything returns to its pool.

### Leaderboard
- `SessionResult` is `[Serializable]` and wrapped for `JsonUtility`, saved to `persistentDataPath/leaderboard.json`.
- New results are inserted at the front and the list is trimmed to the **latest** 5.
- A missing or corrupt file gives an empty board.
- The best score is kept in PlayerPrefs for the "NEW BEST!" tag.

### Cards (`CardSystem`)
- A card spawns every 20 s within 1.5 m of the beacon. The player collects it by **walking** until within 0.5 m.
- What each card does:
  - **Multi-Shot**: spread +1 for 12 s.
  - **Mirror** / **Prism**: +1 charge, placed with HUD buttons at the crosshair.
  - **Freeze Pulse**: enemies within 2 m are 70% slower for 4 s.
  - **Med Kit**: +30 HP.
- Mirrors make the game fully playable even when no walls are detected.

### Audio (`AudioManager`)
- Sources: 1 music source, 1 2D one-shot source, and 10 pooled 3D sources.
- **No AudioSource on enemies or projectiles.** Clip settings live in `GameConfig.sounds`.

| SoundId | Plays when |
|---|---|
| PlayerShoot (required) | trigger pull |
| PlayerDeath (required) | health reaches 0 |
| EnemySpawn (required) | enemy rises (3D) |
| SpitterShoot (required) | Spitter fires (3D) |
| WalkerAttackHit (required) | Walker claw damages player |
| LaserBounce, EnemyHit, EnemyDeath | (3D) |
| PlayerHurt, CardPickup, MirrorPlace, Overheat, CountdownBeep/Go, RoundWin, UIClick, Music | (2D, MirrorPlace 3D) |

**Sound sources:** every clip in `Assets/_Project/Audio` was procedurally synthesized for this project (oscillators, noise and envelopes). They are original work and no third-party licence applies.

### UI (`UIManager`)
- One Screen Space Overlay canvas (1080×1920, match 0.5) with a SafeArea.
- `UIManager` shows exactly one panel per state (it observes `StateChanged`) and adds a click sound to every button.
- The menus dim the camera feed only lightly, so the real room stays visible.
- The fire button uses Unity's built-in `EventTrigger` for hold-to-fire.

### Editor test keys
| Key | Action |
|---|---|
| B | place the beacon without a plane |
| Space | fire |
| P | pause |
| F1 | menu |
| F2 | start |
| F4 | win |
| F5 | die |
| Right mouse + WASD | move the XR Simulation camera |
