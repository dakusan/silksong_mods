<a id="readme"></a>
<!--suppress HtmlDeprecatedAttribute -->
<div align="center">

# 🗺️ VGAtlas
*The ultimate Silksong map — feature-packed, customizable, and crafted for completionists.*<br>
*Explore. Track. Master Pharloom.*
</div>

<img src="LogoSmall.png" alt="VGAtlas Logo" align=right>

1. [Live Demo](#live-demo)
1. [Overview](#overview)
1. [Core Highlights](#core-highlights)
1. [Installation and Compiling](#installation)
1. [Usage](#usage)
1. [Contributing, Credits, and License](#contributing-credits-license)
	- [Additional Credits](#additional-credits)
---
<a id="live-demo"></a>
## 🌐️ Live Demo
https://vgatlas.dakusan.com/

<a id="overview"></a>
## 📖️ Overview
A full-featured web implementation of the *Hollow Knight: Silksong* map, including all [Pharloom Atlas](../PharloomAtlas/#readme) enhancements.<br>

Run the full Atlas experience in any browser—no game or mod installation required.

Planned expansion will make this compatible with other games.

<a id="core-highlights"></a>
## ✨️ Core Highlights
- Highly customizable map experience: toggle UI elements, icon categories, and map behaviors.
- Spoiler-safe “reveal” mechanics for natural exploration.
- Pin tracking via user-uploaded save data.
- Dependency chains for accessibility requirements and progression.
- 100% client-side[^1] — no server required.

<a id="installation"></a>
## ⚙️ Installation
Once compiled, the entire software suite is located in the `dist` subdirectory.<br>
Serve the contents of the `dist` directory from any static web server (Apache, nginx, S3, etc.). The `index.html` entry point will handle the rest.

[^1]: While you technically can just double-click the **index.html** to run the software, most browsers will block requests due to security [CORS](https://en.wikipedia.org/wiki/Cross-origin_resource_sharing) security errors. However, you can upload this to file-only storage like Amazon-S3 and it will work fine.

## 🔨️ Compiling
1. Install npm
	- Debian/Ubuntu (APT): `sudo apt install npm`
	- RHEL/CentOS/Fedora (YUM/DNF):
		- `sudo dnf install npm`
		- (Older systems) `sudo yum install npm`
	- Windows (any of the following):
		- Install [NodeJS](https://nodejs.org/en/download)
		- Install [nvm-windows](https://github.com/coreybutler/nvm-windows)
		- Winget: `winget install OpenJS.NodeJS.LTS`
		- Chocolatey: `choco install nodejs-lts`
1. Run `npm run build` and it builds the software in the `dist` directory.

### 🧰 Vite

This software uses [Vite](https://en.wikipedia.org/wiki/Vite). So for development you can run `npm run dev`.

By using Vite, **all** compiled and static assets (except `index.html`) include content hashes in their filenames. This enables aggressive long-term caching while ensuring updates invalidate correctly.

Assets and source code reloads are separated and will require only an incredibly small amount of overhead data transfer from browsers. The only data the user will generally need to exchange with the web server after first-load will be the ~1.2KB gzipped index.html.

The functionally permanent caching mechanisms are taken care of for Apache via the `.htaccess` file in `dist`.

Source code is bundled into chunks so that only one file load is needed for any interface section, and **all** files are minified.

### 🧱 Using / Generating Assets

All assets are maintained in the [silksong_mods_assets repository](https://github.com/dakusan/silksong_mods_assets).
See `../DataFiles/Notes.txt` for instructions on downloading them.

If you prefer to generate the JSON files locally (without linking to the external repository), you can do so as follows:

1. Install PHP 8.0+
1. Import the SQL schema/data from the [asset repository](https://github.com/dakusan/silksong_mods_assets/raw/refs/heads/master/My%20creations/Silksong.sql)
1. From the project root, run the following: (It will open the SQL config file in an editor.)
```bash
echo 'Removing symlinks to the asset repository'
find VGAtlas/Assets/ -type l -lname '../../DataFiles/*' -print0 | xargs -0 readlink -z | xargs -0 -i rm "VGAtlas/Assets/{}"

echo 'Creating and opening SQL config file'
cd WebServer/
cp Config.Default.php Config.php
"${VISUAL:-${EDITOR:-nano}}" Config.php

echo 'Generating JSON files'
php CreateJsons.php CompactJSON=1 #Omit argument to generate non-minified JSON
php CreateTranslations.php
```

<a id="usage"></a>
## 🧭️ Usage
- Open the map and interact with icons. Clicking the menu button in the upper-left corner opens a popup with additional interface sections.
- See [Pharloom Atlas](../PharloomAtlas/#readme) for full feature details.

<a id="contributing-credits-license"></a>
## 🤝🏻 Contributing, Credits, and License
* See [root project README](../#contributing) for details
* <a id="additional-credits"></a> 📜️ Additional-Credits
	* Initial icons, structure, and inspiration are drawn from community mapping projects like [MapGenie.io](https://mapgenie.io/hollow-knight-silksong/maps/pharloom).
	* All assets © their respective creators (mostly Team Cherry).