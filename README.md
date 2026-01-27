# ESCAPE - Co-op 2D Puzzle Platformer

A local co-op puzzle platformer inspired by *Fireboy and Watergirl*, built with **Unity Engine** and **C#**. Two players with unique abilities must work together to solve puzzles, defeat enemies, and escape through increasingly challenging levels.

## Gameplay Demo

[![ESCAPE Gameplay Video](https://img.youtube.com/vi/jY7CTSlAO1w/maxresdefault.jpg)](https://www.youtube.com/watch?v=jY7CTSlAO1w)

*Click the image above to watch gameplay*

## Features

- **Local Co-op Multiplayer** - Two players control distinct characters simultaneously on the same keyboard
- **Unique Character Abilities**
  - **Player 1 (Shooter):** Can shoot fireballs to destroy enemies after collecting power-ups
  - **Player 2 (Shape-Shifter):** Can transform into a solid platform, allowing Player 1 to reach higher areas
- **Puzzle Mechanics** - Lever-activated bridges, environmental hazards, and enemy obstacles requiring teamwork
- **Progressive Difficulty** - 3 levels with unlockable progression system
- **Enemy AI** - Bats, ghosts, and skeletons with unique movement patterns and attack behaviors

## Tech Stack

| Technology | Usage |
|------------|-------|
| **C#** | Core game logic, player controllers, AI behavior |
| **Unity Engine** | 2D physics, scene management, animation |
| **Object-Oriented Design** | Inheritance, polymorphism, component-based architecture |

## Architecture Highlights

- **Singleton Pattern** - Audio manager persists across scenes with `DontDestroyOnLoad`
- **Static Registry Pattern** - Player instances stored in dictionaries for global access via `GetNearest()`, `Get(player_id)` methods
- **Event-Driven Systems** - `UnityAction` delegates for death, hit, jump, and land callbacks
- **State Management** - Coroutines handle respawning, level transitions, and game over sequences
- **Component-Based Design** - Loose coupling via `GetComponent<T>()` with `RequireComponent` attributes

## Game Controls

| Action | Player 1 | Player 2 |
|--------|----------|----------|
| Move | Arrow Keys | Arrow Keys |
| Jump | Up Arrow | Right Ctrl |
| Special Ability | Spacebar (Shoot) | Right Shift (Transform) |

## Project Structure

```
Assets/
├── Scripts/                    # Core game management
│   ├── GameState.cs           # Level progression, win/lose conditions
│   ├── GameInitializer.cs     # PlayerPrefs initialization
│   └── LevelMenu.cs           # Level selection UI
├── IndieMarc/PlatformerDemo/
│   └── Scripts/
│       ├── PlayerCharacter1.cs  # Player 1 movement, shooting, health
│       ├── PlayerCharacter2.cs  # Player 2 movement, transformation
│       ├── Lever.cs             # Toggle puzzle mechanics
│       └── Bridge.cs            # Dynamic platform control
└── GrottoEscapeArtwork/
    └── Scripts/
        ├── Bat.cs              # Flying enemy with projectiles
        ├── Ghost.cs            # Floating enemy AI
        └── SkeletonMovement.cs # Patrolling ground enemy
```

## How to Run

1. Clone the repository
2. Open in **Unity 2022.3** or later
3. Load `Menu Scene.unity` from `Assets/Scenes/`
4. Press Play or build for your target platform

## What I Learned

- Implementing local multiplayer with distinct control schemes
- Designing complementary character abilities for cooperative gameplay
- Building modular, reusable game systems using OOP principles
- Managing game state across multiple scenes with PlayerPrefs
- Creating enemy AI with raycasting and physics-based movement
