# RICOCHET

An augmented reality survival shooter for Android, made with Unity 6 and AR Foundation.

You place a beacon on your real floor and set up mirrors around your room. Zombies then rise out of the floor, and you fight them with a laser that bounces off your real walls and your mirrors; bank shots score more. Survive until the timer runs out.

**Author:** Eseosa Kay-Uwagboe Pascal

## Project layout
| Folder | Contents |
|---|---|
| `Assets/Scenes` | `Game.unity`, the only scene |
| `Assets/Scripts` | The ten game scripts (namespace `Ricochet`) |
| `Assets/ScriptableObjects` | `GameConfig`, which holds all difficulty, enemy, weapon and sound settings |
| `Assets/Prefabs` | Enemies, projectiles, mirrors, cards, beacon and AR plane |
| `Assets/Prefabs/Materials` | Materials, plus a `Textures` folder inside |
| `Assets/Audio` | All sound effects and the music loop |
| `Submission` | Technical document (PDF) and the code walkthrough script |

## Running it
- **Phone:** File → Build Profiles → Android → Build And Run, using an ARCore phone.
- **Editor:** open `Assets/Scenes/Game.unity` and press Play. XR Simulation stands in for the camera.

| Editor key | Action |
|---|---|
| B | Place the beacon |
| Click | Place mirrors during setup |
| Space | Fire |
| P | Pause |
