# RICOCHET: Code Walkthrough Script (about 5 minutes)

**Before you hit record:**
- Open the project in your code editor with `Assets/Scripts` showing.
- Turn on line numbers.
- Use **Ctrl + G** to jump to a line in Visual Studio or VS Code.

Each step has a **GO TO** line, which tells you where to jump *before* you speak. Then read the **SAY** part.

---

## 1. Intro (about 20 seconds)

**GO TO:** the `Assets/Scripts` folder in the file list.

**SAY:**
> "This is the code for RICOCHET, my AR survival shooter. The whole game is ten scripts, and they all sit in one namespace."

**GO TO:** `ObjectPool.cs`, **line 6**

**SAY:**
> "Every file starts with namespace Ricochet, so all my classes are grouped together and don't clash with Unity's own classes."

---

## 2. Object pooling (about 50 seconds)

**GO TO:** `ObjectPool.cs`, **line 22**

**SAY:**
> "This is my object pool. It's generic, so the same class can pool lasers, acid, enemies, cards and mirrors."

**GO TO:** `ObjectPool.cs`, **line 42**

**SAY:**
> "When the pool is created, this loop makes every object up front and switches it off. So during the game I never call Instantiate or Destroy for bullets."

**GO TO:** `ObjectPool.cs`, **line 57**

**SAY:**
> "Get takes a sleeping object off the stack, moves it into place and turns it on."

**GO TO:** `ObjectPool.cs`, **line 66**

**SAY:**
> "If the pool ever runs out, it grows by one and logs a warning. The pause menu shows this number, and it stays at zero, which proves nothing is created during play."

**GO TO:** `ObjectPool.cs`, **line 78**

**SAY:**
> "Return does the opposite. It switches the object off and puts it back on the stack, ready to be reused."

**GO TO:** `ObjectPool.cs`, **line 8**

**SAY:**
> "Objects that are pooled use this IPoolable interface. OnSpawned is where each object resets itself, so a reused bullet behaves like a brand new one."

---

## 3. Shooting and reusable projectiles (about 40 seconds)

**GO TO:** `Player.cs`, **line 69**

**SAY:**
> "The player creates a pool of forty laser bolts when the game starts."

**GO TO:** `Player.cs`, **line 173**

**SAY:**
> "When I shoot, I first find the point under the crosshair. Then I take that point minus the muzzle position and normalise it. That gives me the direction from the gun to the target."

**GO TO:** `Player.cs`, **line 176**

**SAY:**
> "This is the spread maths for the Multi-Shot card. The start angle shifts the fan to the left by half its width, so the bolts end up centred on the crosshair."

**GO TO:** `Projectile.cs`, **line 49**

**SAY:**
> "This is the bullet itself. In OnSpawned it resets its bounces, its mirror flag and its hit mask, and it clears its trail. That's what makes reuse safe."

---

## 4. Bouncing and reflection (about 50 seconds)

**GO TO:** `Projectile.cs`, **line 120**

**SAY:**
> "The bullet doesn't use physics forces. Every frame it casts a small sphere from where it is to where it's going. That way it can't skip through a thin AR wall."

**GO TO:** `Projectile.cs`, **line 132**

**SAY:**
> "When it hits a wall or a mirror, I first make sure the surface normal points back at the bullet. The dot product tells me if it's facing the wrong way, and if it is I flip it."

**GO TO:** `Projectile.cs`, **line 133**

**SAY:**
> "Then Vector3.Reflect bounces the direction off that normal, just like light off a mirror."

**GO TO:** `Projectile.cs`, **line 135**

**SAY:**
> "If the surface was a mirror, I mark the bullet as a mirror shot. That matters for the Ghost enemy."

**GO TO:** `Projectile.cs`, **line 138**

**SAY:**
> "Each bounce also changes the bullet's colour, so the player can see the ricochet."

**GO TO:** `Projectile.cs`, **line 163**

**SAY:**
> "When it finally hits an enemy, the score multiplier depends on the bounces. One bounce is times one and a half, two is times two, and three is times three."

---

## 5. Enemy system (about 60 seconds)

**GO TO:** `Enemy.cs`, **line 10**

**SAY:**
> "This is the Enemy component. It handles everything every enemy shares: health, rising out of the floor, walking, getting hit and dying."

**GO TO:** `Enemy.cs`, **line 106**

**SAY:**
> "This line is an ease out curve. It makes the zombie grow out of the floor fast at first and then slow down at the end."

**GO TO:** `Enemy.cs`, **line 163**

**SAY:**
> "This is how they walk. I take the flat direction to the player, divide by the distance to get a unit vector, and multiply by speed times delta time. The Min makes sure they stop exactly at their attack range."

**GO TO:** `Enemy.cs`, **line 271**

**SAY:**
> "What each enemy actually does comes from this abstract EnemyAI class. That's my base class for inheritance."

**GO TO:** `Enemy.cs`, **line 125**

**SAY:**
> "The Enemy just calls ai.Tick. It doesn't know which type it is. That's polymorphism."

**GO TO:** `Enemy.cs`, **line 292**

**SAY:**
> "The Walker walks straight at you and claws when it's close."

**GO TO:** `Enemy.cs`, **line 346**

**SAY:**
> "The Spitter is my shooter enemy. It keeps walking until it's within three metres, then stops."

**GO TO:** `Enemy.cs`, **line 363**

**SAY:**
> "Then it spits acid at my head, and the acid comes from a pool too."

**GO TO:** `Enemy.cs`, **line 336**

**SAY:**
> "The Ghost inherits from the Walker, but it overrides one rule: it can only be hurt by a mirror shot."

**GO TO:** `Enemy.cs`, **line 182**

**SAY:**
> "And here the enemy asks its AI if the hit counts. For a Ghost, a direct hit just sparks off."

---

## 6. Factory and spawning (about 25 seconds)

**GO TO:** `Enemy.cs`, **line 423**

**SAY:**
> "This is my factory. The spawner just asks for a type and a position. The factory picks the right pool and sets the enemy up."

**GO TO:** `Enemy.cs`, **line 494**

**SAY:**
> "The spawner picks the type here. Ghosts only come if there's a mirror, and the Spitter chance goes up as the round goes on."

**GO TO:** `GameConfig.cs`, **line 89**

**SAY:**
> "That ramp is a Lerp between the start and end values using how far through the round we are. The numbers come from this ScriptableObject, so each difficulty is just different data."

---

## 7. Singleton and state machine (about 30 seconds)

**GO TO:** `GameManager.cs`, **line 15**

**SAY:**
> "GameManager is a singleton. This static Instance means any script can reach it."

**GO TO:** `GameManager.cs`, **line 58**

**SAY:**
> "If a second one ever appears, it destroys itself."

**GO TO:** `GameManager.cs`, **line 235**

**SAY:**
> "This is my state machine. Change calls Exit on the old state and Enter on the new one, then tells anyone listening."

**GO TO:** `GameManager.cs`, **line 385**

**SAY:**
> "In the Playing state, Tick runs the timer, the spawner and the cards, adds five points a second and ends the round when time runs out."

---

## 8. AR and mirrors (about 30 seconds)

**GO TO:** `ARController.cs`, **line 82**

**SAY:**
> "For AR, every plane ARCore finds comes through here. Floors get the texture with my name, and walls get a grid."

**GO TO:** `ARController.cs`, **line 94**

**SAY:**
> "Walls are put on the reflective layer, and that's what lets the lasers bounce off real walls."

**GO TO:** `ARController.cs`, **line 186**

**SAY:**
> "When I tap, the beacon is anchored to the real floor."

**GO TO:** `ARController.cs`, **line 209**

**SAY:**
> "After that, the game stops looking for new floors and only looks for walls."

**GO TO:** `CardSystem.cs`, **line 264**

**SAY:**
> "In the setup phase, a tap becomes a ray from the camera through my finger."

**GO TO:** `CardSystem.cs`, **line 300**

**SAY:**
> "If it hits a wall, the mirror is rotated to face along the wall's normal, so it sits flat on the wall."

---

## 9. Observer, sound and leaderboard (about 30 seconds)

**GO TO:** `UIManager.cs`, **line 69**

**SAY:**
> "The UI only subscribes to events. That's the observer pattern. The game never talks to the UI directly."

**GO TO:** `AudioManager.cs`, **line 36**

**SAY:**
> "The audio manager makes a small pool of 3D sound sources, so enemies and bullets don't need their own AudioSource."

**GO TO:** `AudioManager.cs`, **line 77**

**SAY:**
> "The modulo here makes it go round in a circle through the sources."

**GO TO:** `GameManager.cs`, **line 622**

**SAY:**
> "For the leaderboard, each new run is added at the front."

**GO TO:** `GameManager.cs`, **line 642**

**SAY:**
> "Trim then cuts it down to the latest five, and it's saved as JSON so it's still there after closing the app."

---

## 10. Outro (about 10 seconds)

**SAY:**
> "So that's the code: a pool for everything that spawns, a state machine for the flow, a factory for enemies, events for the UI, and inheritance for enemies, cards and screens. Thanks for watching."
