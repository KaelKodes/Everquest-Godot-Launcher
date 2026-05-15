# EverQuest.Godot Launcher (EQ.gd)
<img width="1161" height="664" alt="image" src="https://github.com/user-attachments/assets/eddc7304-1bd4-4a85-baa5-6a3665846417" />


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

## Install path and Administrator (Windows)

During **Install** or **Update**, the launcher downloads **LanternExtractor** (EQ Lantern) into your chosen game folder (`<install path>\LanternExtractor\`). The client uses it to extract zones and assets from your EverQuest install at runtime.

**Some install locations are not writable without elevated rights**, for example:

- `C:\Program Files\` (or `Program Files (x86)`)
- The root of `C:\` or other system-protected directories
- Folders owned by another user or locked by policy

If install fails with access denied or LanternExtractor errors, either:

1. **Run EQ.gd Launcher as Administrator** (right-click the `.exe` → **Run as administrator**), or  
2. **Pick a folder you own**, such as `C:\Games\EQ.gd`, `D:\Games\EQMUD`, or `Documents\EQ.gd` (no admin required).

You do not need Administrator for a normal user-writable path. The in-app install bar shows the same reminder.


## Releases and Installing

Prebuilt Windows binaries will be attached to **[Releases](https://github.com/KaelKodes/Everquest-Godot-Launcher/releases)** on this repository when they are ready. That is the recommended path for players who are not building from source.

## Contributing

- **Launcher:** [EverQuest godot LAUNCHER issues](https://github.com/KaelKodes/Everquest-Godot-Launcher/issues)  
- **Server, database, and hosting issues:** [EverQuest godot SERVER Issues](https://github.com/KaelKodes/Everquest-Godot-Server/issues)
- **Client Issues:** [EverQuest godot CLIENT issues](https://github.com/KaelKodes/Everquest-Godot-Client/issues)
- **Dev Blog:** [EQ.gd Announcements](https://github.com/KaelKodes/Everquest-Godot-Launcher/discussions/categories/announcements)
- **Discord** [dxvAvKg7FZ](https://discord.gg/dxvAvKg7FZ)

