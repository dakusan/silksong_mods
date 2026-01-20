<a id="readme"></a>
# Hollow Knight: Silksong Mods by Dakusan

> A collection of mods designed to expand and enhance the **Hollow Knight: Silksong** experience.<br>
> Each module is self-contained with its own documentation and purpose — ranging from gameplay utilities to full-scale feature overhauls.

1. [Repository Structure](#repository-structure)
   - [Pharloom Atlas](#pharloom-atlas)
   - [SilkDev (Plugin Developer Tools)](#silkdev-plugin-developer-tools)
   - [No Clip](#no-clip)
   - [Pin Finder](#pin-finder)
   - [Web Server](#web-server)
1. [Compiling](#compiling)
1. [Notes](#notes)
1. [Contributing](#contributing)
1. [Credits](#credits)
1. [License](#license)

---
<a id="repository-structure"></a>
### ⚙️ Repository Structure
---

|Local description|Link to Readme|Description|[Nexus Mod page](https://www.nexusmods.com/)|
|-----------------|--------------|-----------|--------|
|**[Pharloom Atlas](#pharloom-atlas)**          |[PharloomAtlas/](PharloomAtlas/#readme)|Main mod (map overhaul)                |[Nexus #755](https://www.nexusmods.com/hollowknightsilksong/mods/755)|
|**[Silk Dev](#silkdev-plugin-developer-tools)**|[SilkDev/](SilkDev/#readme)            |Plugin developer tools and helpers     |[Nexus #510](https://www.nexusmods.com/hollowknightsilksong/mods/510)|
|**[No Clip](#no-clip)**                        |[NoClip/](NoClip/#readme)              |Noclip gameplay mod                    |[Nexus #478](https://www.nexusmods.com/hollowknightsilksong/mods/478)|
|**[Pin Finder](#pin-finder)**                  |[PinFinder/](PinFinder/#readme)        |Pin location gathering utility         |[Nexus #772](https://www.nexusmods.com/hollowknightsilksong/mods/772)|
|**[Web Server](#web-server)**                  |[WebServer/](WebServer/#readme)        |Remote data receiver for Pharloom Atlas|[Nexus #755](https://www.nexusmods.com/hollowknightsilksong/mods/755)|

---
<a id="pharloom-atlas"></a>
# 🗺️ Pharloom Atlas
**Tagline:** *The ultimate Silksong map overhaul — feature-packed, customizable, and crafted for completionists.*

A complete reimagining of Silksong’s map system with advanced tracking, markers, controls, and exploration tools.<br>
  📂️ [Read more →](PharloomAtlas/README.md#readme)<br>
  Requires: [SilkDev](#silkdev-plugin-developer-tools)

<a id="silkdev-plugin-developer-tools"></a>
# 🧩️ SilkDev (Plugin Developer Tools)
A toolkit of useful utilities for Silksong mod developers — includes tools to simplify mod creation like for debugging, configurations, input controls, events, windows.<br>
  📂️ [Read more →](SilkDev/README.md#readme)

<a id="no-clip"></a>
# 👻️ No Clip
Adds noclipping capabilities to Silksong, allowing players to freely explore or debug game areas.<br>
  📂️ [Read more →](NoClip/README.md#readme)<br>
  Requires: [SilkDev](#silkdev-plugin-developer-tools)

<a id="pin-finder"></a>
# 📍️ Pin Finder
Searches through all scenes to locate map pins, assisting developers with world data exploration.<br>
  📂️ [Read more →](PinFinder/README.md#readme)<br>
  Requires: [SilkDev](#silkdev-plugin-developer-tools), [NoClip](#no-clip)

<a id="web-server"></a>
# 🌐️ Web Server
A lightweight web server for receiving and managing **Pharloom Atlas** user contribution data, enabling community crowdsourcing.<br>
  📂️ [Read more →](WebServer/README.md#readme)

---

<a id="compiling"></a>
## 🔨️ Compiling

### Prerequisites
- Windows
- Visual Studio 2022 (or newer) with **.NET desktop development**
- .NET SDK matching the solution (auto-restored by VS)

### Steps

1. Symlink the game directory into the repository root

Run from an **elevated Command Prompt**:

```bat
   set STEAM_PATH="C:\Program Files (x86)\Steam" & REM Fill this in with your steam path that contains the `Hollow Knight Silksong` folder
   mklink /J "Hollow Knight Silksong" "%STEAM_PATH%\steamapps\common\Hollow Knight Silksong"
```
The repository root should now contain a `Hollow Knight Silksong/` folder pointing to your Steam install.

2. Open and build
- Open `SilkSong.sln` in Visual Studio
- Select `Release | x64` (recommended)
- Build the solution

#### Output
The compiled DLL(s) will be placed in the appropriate `BepInEx/plugins/dakusan/` subdirectory under the symlinked
`Hollow Knight Silksong` folder, ready to run in-game.

---

<a id="notes"></a>
## 🐞️ Notes
- Each module can be built and used independently.
- See individual `README.md` files for setup and usage instructions.
- This project is built for fun exploration and learning.

<a id="contributing"></a>
## 🛠️ Contributing
Contributions, suggestions, and feedback welcome!

- Report issues or feature requests via GitHub.

### Before opening a pull request
Please ensure the following:
- Run `.githooks/hook-setup.sh` once before making any commits.
- Acknowledge that this project is licensed under the **BSD-3-Clause** license.
- Follow the existing code style and conventions.
   - The project includes a `.editorconfig` file that Visual Studio will respect, though not all styling rules can be fully expressed via EditorConfig.

<a id="credits"></a>
## 📜️ Credits
* **Dakusan** (the author) — Developer, tinkerer, and longtime Hollow Knight fan.
* **[BepInEx]( https://github.com/BepInEx/BepInEx/)** for making modding much more accessible.
* **[Team Cherry](https://www.teamcherry.com.au/)** for creating the amazing *Hollow Knight* series.

<a id="license"></a>
## 🧠️ License
This repository is open-source and intended for educational and community modding purposes.
Licensed under [BSD 3 clause](LICENSE).