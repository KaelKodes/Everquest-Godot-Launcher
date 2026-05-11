# EverQuest.Godot Launcher (EQ.gd)

This repository is the **public entry point** for **EQ.gd**—the place players and contributors should land first. It contains the **desktop launcher**: a Godot 4.6 + C# app that will keep the game install updated, show release notes and news, and offer a convenient **Play** shortcut to the same client you could start manually.

## Repositories

| Repository | What it is |
|------------|------------|
| **[Everquest-Godot-Launcher](https://github.com/KaelKodes/Everquest-Godot-Launcher)** | **This project** — the desktop launcher (start here for players). Update flow and `blogs.json` news ship from here; install/update wiring is still in progress. |
| **[Everquest-Godot-Server](https://github.com/KaelKodes/Everquest-Godot-Server)** | **Game server** — Node stack, database, hosting, and server-side docs for **EQ.gd**. |

The launcher does not replace the legal requirement to own EverQuest data for the client; that policy lives with the game and server documentation in those repositories.

## Requirements (development)

- **Godot 4.6** with **.NET** / **C#** enabled  
- **.NET 8** SDK (matches `New Game Project.csproj` / Godot .NET export)  
- **Git** on your PATH (planned for install/update once the launcher is wired to real clone/pull)

Open this folder as the project root in the Godot editor. Main scene: `LauncherMain.tscn`.

## Player-facing workflow (intent)

1. **Install / Update** — Sync the game files to the chosen install directory (implementation: real `git` or packaged updates; UI exists, plumbing still to be completed).  
2. **News** — `blogs.json` is updated **with** releases so patch notes travel with the build, not from a separate live service.  
3. **Play** — Optional shortcut that starts the same exported client you would run from the install folder.  
4. **Discord** — Not wired yet; the button is reserved for when the community link is ready.

## Releases

Prebuilt Windows binaries will be attached to **[Releases](https://github.com/KaelKodes/Everquest-Godot-Launcher/releases)** on this repository when they are ready. That is the recommended path for players who are not building from source.

## Contributing

- **Launcher:** [Everquest-Godot-Launcher issues](https://github.com/KaelKodes/Everquest-Godot-Launcher/issues)  
- **Server, database, hosting:** [Everquest-Godot-Server issues](https://github.com/KaelKodes/Everquest-Godot-Server/issues)

