# Medieval Potato 🥔

A lightweight, data-driven medieval simulation game built with **Godot 4.6.2 (C#)**. Inspired by "Manor Lords", but optimized for performance using a "Ghost Pop" visual agent system and a custom blocky terrain architecture.

## 🌟 Key Features

- **Localized Resource Logistics**: A decentralized economy where workplace buildings (Forager Hut, Fishing Hut, Woodcutter Hut, etc.) store resources locally.
- **Dedicated Transporters**: Specialized villager roles that handle the logistics loop, moving goods from local workplaces to the central Town Center.
- **Blocky Terrain System**: A Minecraft-inspired 3D terrain with discrete height levels and stable building snapping to prevent clipping and floating.
- **Ghost Pop System**: Performance-optimized visual agents that move via Tweens instead of expensive physics/AI, supporting hundreds of villagers with minimal CPU impact.
- **RTS Camera & UI**: Smooth top-down navigation with WASD, Zoom, and Rotation, paired with a clean UI for town management.
- **Dynamic Ambience**: Living streets that populate with travelers as your settlement grows.

## 📂 Project Structure

- `assets/`: 3D models, textures, and sprites.
- `scenes/`: Godot scenes for the game world, UI, and buildings.
- `src/`: Core C# logic:
    - `src/agents/`: Villager logic and Ghost Pop system.
    - `src/buildings/`: Workplace buildings, residences, and logistics (BuildingManager, TownCenter).
    - `src/core/`: Global simulation state and project-wide utilities.
    - `src/environment/`: Terrain generation and environmental objects.
    - `src/camera/`: RTS camera implementation.
    - `src/ui/`: Game HUD and management menus.

## 🛠️ Requirements

- Godot 4.6.2 (Mono/.NET Edition)
- .NET 8 SDK

## 🚀 Getting Started

1. Clone the repository.
2. Open `project.godot` in Godot 4.6.2 (Mono).
3. Build the project (C#) from the Godot editor.
4. Press **F5** to run the simulation!

## 📜 License

MIT
