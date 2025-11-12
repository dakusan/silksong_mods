<a id="readme"></a>
<div align="center">

# 🌐 Web Server
<i>Crowdsource Silksong data</i>
</div>

1. [Overview](#overview)
1. [Installation](#installation)
1. [Usage](#usage)
1. [Developer Notes](#developer-notes)
1. [Contributing, Credits, and License](#contributing-credits-license)<br clear="all">
---
<a id="overview"></a>
## 📖 Overview
A lightweight web server for receiving and managing Pharloom Atlas user contribution data, enabling community crowdsourcing.

[See Nexus Mods page for further details.](https://www.nexusmods.com/hollowknightsilksong/mods/755)

<a id="installation"></a>
## ⚙️ Installation
1. Requires a php server and mysql/mariadb
1. Create a database and run `DB.sql`
1. Copy `Config.Default.php` to `Config.php` and set your database parameters
1. You’re all set up to receive at /Submit.php
1. The [Pharloom Atlas mod](../PharloomAtlas) would have to be redirected to your web server.

<a id="usage"></a>
## 🧭 Usage
- See [Pharloom Atlas ](../PharloomAtlas/#readme): [Web Integration](../PharloomAtlas/#web-integration) and [Additional Contributions](../PharloomAtlas/#additional-contributions)

<a id="developer-notes"></a>
## 🧩 Developer Notes
- Source code organized under `WebServer/`.

<a id="contributing-credits-license"></a>
## 🤝 Contributing, Credits, and License
See [root project README](../#contributing) for details