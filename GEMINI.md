# Gemini Code Assistant Context

## Project Overview

This is a C# mod for the game "WorldBox - God Simulator", adding a "Cultivation" (修仙) system. The mod introduces features like spiritual roots, energy absorption, and progression through various cultivation realms (e.g., Foundation, Golden Core, Nascent Soul).

**Key Technologies & Architecture:**

*   **Language:** C# 12
*   **Framework:** .NET Framework 4.8
*   **Game Engine:** Unity (inferred from `UnityEngine.dll` reference)
*   **Mod Loader:** [NeoModLoader](https://github.com/NeoModLoader/NeoModLoader)
*   **Patching:** [Harmony](https://github.com/pardeike/Harmony) for runtime patching of game methods.
*   **Core Architecture:** The mod is built upon an Entity Component System (ECS) architecture provided by `Friflo.Engine.ECS`. It extends core game objects (like actors, cities, and tiles) by associating them with custom ECS entities and components.
*   **Extensibility:** A key design pattern is the use of `ExtendComponent<T>` and `ExtendComponentManager` to attach custom data and logic to base game objects. Content is loaded dynamically using reflection, where classes implementing `ICanInit` are automatically initialized.

## Building and Running

**Building:**

The project is a C# library (`.dll`). To build it, you will need a .NET development environment.

*   **IDE:** Open `Cultiway.sln` or `Solution.sln` in a compatible IDE like Visual Studio (with .NET Framework 4.8 targeting pack) or JetBrains Rider.
*   **Build Action:** Build the `Cultiway` project in `Release` configuration.

The build process will produce `Cultiway.dll` in the `bin/Release` directory.

**Running the Mod:**

1.  This project is a mod for the game WorldBox. You must have the game installed.
2.  You also need to have [NeoModLoader](https://github.com/NeoModLoader/NeoModLoader) installed for WorldBox.
3.  Place the entire mod folder, including the `mod.json` file, and all asset directories (`Content`, `GameResources`, `Locales`), into the NeoModLoader mods directory (`<WorldBox_Install_Path>/Mods`).
4.  Launch WorldBox. The mod should be loaded by NeoModLoader.

```
// TODO: Add a script to automate the deployment process.
```

## Development Conventions

*   **Code Style:** The codebase is well-structured and follows standard C# conventions. The use of `namespace` and a clear folder structure helps to organize the code.
*   **ECS Usage:** The mod heavily relies on an ECS pattern. When adding new features, it's important to understand how to create and manage entities, components, and systems. The `Source/Core` directory contains many examples of this.
*   **Extending Game Objects:** To add custom data or logic to existing game objects (like actors or cities), use the `ExtendComponent` pattern. You can find examples in `Source/Core/ActorExtend.cs` and `Source/Core/CityExtend.cs`.
*   **Content Initialization:** To add new content (like skills, items, or status effects), create a new class that implements `ICanInit` and add it to the `Source/Content` directory. The `Content/Manager.cs` will automatically discover and initialize it.
*   **Hot-Reloading:** The mod supports hot-reloading. The `IReloadable` interface and the `Reload` method in `ModClass.cs` are used to reload content and configuration changes without restarting the game. This is useful for faster development and iteration.
*   **Localization:** Text strings are managed through localization files in the `Locales` directory. Use the `LM` class to load and access localized strings.
*   **Debugging:** The `ModClass` includes a debug mode that is enabled for a specific username ("Inmny"). This provides access to additional logging and debug features.
