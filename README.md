# Medieval Potato 🥔

A lightweight, data-driven medieval simulation game built with **Godot 4.6.2 (C#)**. Inspired by "Manor Lords", but optimized for performance using a "Ghost Pop" visual agent system.

## 🌟 Key Features
- **Statistical Simulation**: Population, Food, and Wood stats calculated globally (Paper Stats).
- **Ghost Pop System**: Visual agents that move via Tweens instead of expensive physics/AI, allowing for hundreds of villagers with zero CPU lag.
- **RTS Camera**: Smooth top-down navigation with WASD, Zoom (Mouse Wheel), and Rotation (Q/E).
- **Dynamic Ambience**: Streets automatically populate with random travelers as your population grows.
- **Data-Driven Houses**: Residences act as the source of truth for your population.

## 📂 Project Structure
- `assets/`: Textures and sprites.
- `scenes/`: Game levels and UI.
- `scripts/`: C# logic files.
- `scripts/entities/`: Logic for houses and visual agents.

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
