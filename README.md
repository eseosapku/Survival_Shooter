# RICOCHET: AR Laser Survival Shooter

A mobile AR first-person survival shooter for Android (ARCore), built with Unity 6 and AR Foundation 6.

You place a glowing beacon on your real floor. Zombies rise out of the floor and close in on you. Your laser blaster fires bolts that **ricochet** off real walls (detected AR vertical planes) and placeable mirrors, and bank shots earn score multipliers. Survive until the timer hits zero.

**Author:** Eseosa Kay-Uwagboe Pascal

## Features
- AR Foundation plane detection (floors + walls). The custom plane visual shows the author's full name on floors and a neon grid on walls.
- Tap-to-place a single anchored arena. Further taps are ignored, and floor detection stops after placement.
- First-person player: health, hold-to-fire laser, heat/overheat, spread tiers, damage feedback (vignette, gun shake, vibration, sound).
- Pooled laser bolts with SphereCast movement and up to 3 reflections (x1 / x1.5 / x2 / x3 score multiplier).
- Two enemy types, each with its own AI class (`WalkerAI` and `SpitterAI`) built on the abstract `EnemyAI`:
  - **Walker** (melee, 3 hits to kill)
  - **Spitter** (ranged acid, 5 hits to kill)
- Difficulty (Easy / Normal / Hard) driven by ScriptableObjects.
- Ability cards you collect by walking over them: Multi-Shot, Mirror, Prism, Freeze Pulse, Med Kit.
- Local leaderboard of the latest 5 runs, saved as JSON so it persists between launches.
- All sounds synthesized procedurally, so they are original and need no licence.

## Architecture
All game code lives in `Assets/_Project/Scripts`, under the namespaces `Ricochet.*`.

| Pattern | Where |
|---|---|
| Object Pool | `Pooling/ObjectPool<T>`: bolts, acid, enemies, cards, gadgets, floating text |
| Singleton | `GameManager`, `AudioManager`, `VfxManager` |
| State | `GameStateMachine` + `IGameState` (Menu, Leaderboard, Scanning, Countdown, Playing, Paused, GameOver) |
| Factory | `EnemyFactory.Create(EnemyType, position)` |
| Observer | C# events: health, score, timer, heat, state changes, arena placed, enemy killed |

See `Assets/_Project/Docs/DEV_NOTES.md` for the full technical notes.

## Code
There are 10 scripts in `Assets/_Project/Scripts`:

| Script | Role |
|---|---|
| `GameManager` | Singleton, state machine, round, leaderboard |
| `GameConfig` | All tuning in one ScriptableObject |
| `ObjectPool` | Generic object pool |
| `ARController` | Custom planes and tap-to-place |
| `Player` | Health, feedback, laser blaster |
| `Projectile` | Laser bolts and acid |
| `Enemy` | Enemy component, Walker/Spitter AI, factory, spawner |
| `CardSystem` | Ability cards, mirrors, prisms |
| `AudioManager` | All sound |
| `UIManager` | All screens |

To balance the game, edit `Assets/_Project/ScriptableObjects/GameConfig.asset`. No code changes are needed.

## Editor testing
1. Open `Assets/_Project/Scenes/Game.unity` and press Play. XR Simulation is used on Windows.
2. Controls:

| Key / input | Action |
|---|---|
| Right-mouse + WASD | Move the simulated camera |
| Left click | Tap |
| B | Drop the arena without a plane (editor only) |
| Space | Fire |
| F1 | Menu |
| F2 | Start |
| F4 | Win the round |
| F5 | Die |
| P | Pause |

## Build (Android)
1. Requirements: Unity 6000.4.5f1 with Android Build Support, and an ARCore-supported phone.
2. Go to **File → Build Profiles → Android → Switch Platform**.
3. Go to **Project Settings → XR Plug-in Management → Project Validation** and click **Fix All**.
4. Click **Build** (or **Build And Run**).
