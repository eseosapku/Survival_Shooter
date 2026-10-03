# Video shot lists

## A. Device gameplay video (record on the phone; it must start from the Unity splash screen)
To record on a Samsung: swipe down → **Screen recorder** → Sound: *Media sounds* → Start. Then launch the app.

1. App launch → **Unity splash screen** (keep it in frame).
2. Main menu: the title, then tap **EASY / NORMAL / HARD** to show the selection, then open the **Leaderboard** and go back.
3. Tap **START** → Scan screen. Move the phone slowly until the **floor with your name** appears. Pause on it for 2 s.
4. Point at a wall so the **magenta wall grid** appears and the HUD reads "WALLS: 1+".
5. Tap to place the **beacon**. Tap again to show that nothing else is placed.
6. Countdown 3-2-1-GO.
7. Shoot a **Walker** (3 hits) and show the hit flash and the "+100".
8. Bank a shot off a wall to show the **"x1.5 / x2 RICOCHET!"** popup.
9. Let a **Spitter** stop and shoot. Take damage to show the red vignette, the gun shake and the sound. Kill it (5 hits).
10. Let a **Walker** reach you and claw you (melee hit sound).
11. Hold fire until it **overheats**.
12. Walk over a **card**:
    - Multi-Shot to show the fan of bolts.
    - Mirror: place it, then bounce shots off it.
13. **Pause** → show the pool stats → Resume.
14. Either let the timer run out (**SURVIVED**) or die (**OVERRUN**) → End screen.
15. Main Menu → **Leaderboard** shows the new run.
16. *(Optional)* Close the app, reopen it, and show the leaderboard still has the run (persistence).

## B. Technical explanation video (screen capture of Unity + code, about 5–8 min)
1. **Intro (20 s):** the game, the platform, and the tools used.
2. **Project structure (30 s):** the `_Project` folders and the namespaces.
3. **AR (1 min):**
   - The Game scene hierarchy.
   - `ARPlaneStyler`: floor/wall classification, the name texture, layers.
   - `ARPlacementController`: single placement, anchor, switching to vertical detection.
4. **State machine (1 min):**
   - The `IGameState` interface and `GameStateMachine`.
   - Walk through `PlayingState` (Enter/Tick/Exit, the death slow-mo).
   - The `UIManager` observing `StateChanged`.
5. **Object Pool (1 min):**
   - `ObjectPool<T>` with Get/Release/pre-warm.
   - `LaserBolt.OnSpawned` resetting its state.
   - The pool stats on the pause screen in play mode ("grown 0").
6. **Laser ricochet (45 s):** the `LaserBolt.Update` SphereCast loop, `Vector3.Reflect`, and the multiplier.
7. **Enemies (1 min):**
   - The abstract `Enemy` and the `Walker`/`Spitter` overrides (polymorphism).
   - `EnemyFactory.Create` (Factory pattern).
   - `EnemySpawner` difficulty scaling from the ScriptableObject.
8. **Cards (30 s):** `AbilityCard.Activate` overrides.
9. **Audio (30 s):** the `AudioManager` sources, the `SoundLibrary` asset, and the fact that there are no AudioSources on enemies or bullets.
10. **Leaderboard (30 s):** the JSON in persistentDataPath, keeping the latest 5, and handling a corrupt file.
11. **Wrap-up (20 s):** how the patterns fit together.
