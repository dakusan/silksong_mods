<a id="readme"></a>
<div align="center">

# 📍️ Pin Finder
*Developer utility – auto-discover every collectible position & save flag*
</div>

<img src=Assets/LogoSmall.jpg alt="Pin Finder Logo" align=right>

1. [Overview](#overview)
1. [Installation and Compiling](#installation)
1. [Usage](#usage)
1. [Developer Notes](#developer-notes)
1. [Contributing, Credits, and License](#contributing-credits-license)<br clear="all">
---
<a id="overview"></a>
## 📖️ Overview
Serially loads every scene in the game and records the exact world position + persistent save-flag data of game objects that change save data including: benches, collectibles, lore tablets, switches, etc. Outputs as JSON.

[See Nexus Mods page for further details.](https://www.nexusmods.com/hollowknightsilksong/mods/772)

Settings dialog translated [via LLM] into 20 languages.

Built on top of the **[BepInEx](https://github.com/BepInEx/BepInEx/)** framework.

<a id="installation"></a>
## ⚙️ Installation
1. Install **[BepInEx](https://www.nexusmods.com/hollowknightsilksong/mods/26)**.
1. [Download](https://www.nexusmods.com/hollowknightsilksong/mods/772?tab=files) and extract this mod into: `Hollow Knight Silksong/BepInEx/plugins/dakusan`<br>
You should end up with paths like: `Hollow Knight Silksong/BepInEx/plugins/dakusan/NoClip.dll`
1. (Optional) If you use other *Dakusan* mods — such as [Plugin Developer Tools](https://www.nexusmods.com/hollowknightsilksong/mods/510) or [NoClip](https://www.nexusmods.com/hollowknightsilksong/mods/478) — place them in the same `dakusan` directory.
1. Run the game

## 🔨️ Compiling
* See [root project README](../#compiling) for details

<a id="usage"></a>
## 🧭️ Usage
- Open the config window in-game with **F1**.
- Default process start key: **None** (Run “Start Process” in config).

<a id="developer-notes"></a>
## 🧩️ Developer Notes
- Source code organized under `PinFinder/`.
- Built using shared utilities from [`SilkDev/`](../SilkDev/#readme) and [`NoClip/`](../NoClip/#readme).

<a id="contributing-credits-license"></a>
## 🤝🏻 Contributing, Credits, and License
See [root project README](../#contributing) for details