<a id="readme"></a>
<!--suppress HtmlDeprecatedAttribute -->
<div align="center">

# 🌐️ Web Server
<i>Crowdsource Silksong data</i>
</div>

1. [Overview](#overview)
1. [Installation](#installation)
1. [Usage](#usage)
1. [Developer Notes](#developer-notes)
1. [Contributing, Credits, and License](#contributing-credits-license)<br clear="all">
---
<a id="overview"></a>
## 📖️ Overview
* **Mod pages**: For further details, see [Pharloom Atlas’](../PharloomAtlas/#readme): [Nexus Mods page](https://www.nexusmods.com/hollowknightsilksong/mods/755) or [Silksong Mods by Dakusan](https://silksong.dakusan.com/#PharloomAtlas-Description)
	* **Submit.php**: A lightweight web server for receiving and managing Pharloom Atlas user contribution data, enabling community crowdsourcing.
	* **SubmitErrorLog.php**: Logs Silksong Mod errors from users. *(Must be manually sent from the mod’s configuration dialog)*
* **JSON creation**: Create JSON data files from the database
	* **CreateJson.php**: JSON data files
	* **CreateTranslateJson.php**: JSON translation files
* **Other**:
	* **RenderIndex.php**: Renders the webpage for **IndexAssets/index.html** for https://silksong.dakusan.com/
	* **DGraph.php**: An AI attempt at generating a relation graph of the Silksong items *(Found in assets’ repo)*

<a id="installation"></a>
## ⚙️ Installation
1. Requires a php server and mysql/mariadb
1. Create a database and run `DB.sql`
1. Copy `Config.Default.php` to `Config.php` and set your database parameters
1. For scripts…
	* **Submit.php** and **SubmitErrorLog.php**
		* You’re all set up to receive from the [Pharloom Atlas mod](../PharloomAtlas/#readme)
		* The [Pharloom Atlas mod](../PharloomAtlas/#readme) would have to be redirected to your web server.
	* **CreateJson.php**, **CreateTranslateJson.php**, and **RenderIndex.php**
		* See [**VGAtlas** - Using / Generating Assets](../VGAtlas/#-using--generating-assets) for more details on populating the database.

<a id="usage"></a>
## 🧭️ Usage
- See [Pharloom Atlas](../PharloomAtlas/#readme): [Web Integration](../PharloomAtlas/#web-integration) and [Additional Contributions](../PharloomAtlas/#additional-contributions)

<a id="developer-notes"></a>
## 🧩️ Developer Notes
- Source code organized under `WebServer/`.

<a id="contributing-credits-license"></a>
## 🤝🏻 Contributing, Credits, and License
See [root project README](../#contributing) for details