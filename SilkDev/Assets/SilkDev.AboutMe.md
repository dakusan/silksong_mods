<div class=Delete>NOTE: HTML rendered via /Compile/MdToHtml.sh with pandoc and .md is viewable in Microsoft Visual Studio. This will not look correct on Github.<details><summary class=Hide></summary>
<style>
@import url("https://fonts.googleapis.com/css2?family=Inter:ital,opsz,wght@0,14..32,100..900;1,14..32,100..900&display=swap");
@import url("https://fonts.googleapis.com/css2?family=Noto+Color+Emoji&display=swap");
html, body { width:100%; margin:0; padding:0; background-color:black; font-size:14px; line-height:1.3; letter-spacing:0.3px; font-weight:300; }
body #___markdown-content___ {
	max-width:1020px;
	margin: 0 auto;
	padding:20px clamp(0px, calc((100vw - 1020px) / 2), 140px);
	color:white; background-color:#292929;
	font-family:"Inter", "Noto Color Emoji", system-ui, -apple-system, "Segoe UI", "Segoe UI Emoji", "Segoe UI Symbol", "Apple Color Emoji", sans-serif;
}
body #___markdown-content___ p { margin-bottom:0; }
#___markdown-content___ blockquote, #___markdown-content___ ul, #___markdown-content___ ol, #___markdown-content___ dl, #___markdown-content___ table, #___markdown-content___ pre, #___markdown-content___ details { margin-bottom:2px; }
body #___markdown-content___ { line-height:1.3; }
body #___markdown-content___ li + li { margin-top:0; }
body #___markdown-content___ img { background-color:transparent; }
body #___markdown-content___ a { color:rgb(217, 143, 64) !important; font-weight:500; text-decoration:none !important; transition:color .5s; }
body #___markdown-content___ a:hover { color:white !important; }
html body #___markdown-content___ ul, html body #___markdown-content___ ol { margin-left:15px; padding-left:15px; }
html body ul ul, html body ul ol, html body ol ul, html body ol ol { margin-bottom:0; }
body #___markdown-content___ ul, body #___markdown-content___ ol { margin-top:0; }
.Hide, .Delete { display:none; }
b, strong { font-weight:700; }
ul>li { list-style-type:disc !important; }
::marker { font-size:15px; }
</style></details></div>

<meta/><center>![Decor Top](https://static.castledragmire.com/silksong/DecorTop.png)</center>

<br><center><font size=6>**Hollow Knight Silksong: Plugin Developer Tools (SilkDev)**</font></center>
<center><font size=4>The backbone of all my Silksong mods — with handy player utilities and a massive quality-of-life toolkit for modders.</font></center>

<br class=Hide><br><center><font size=5>✨️ **Player-Facing Goodies** ✨️</font></center>
 ---
* Skip intro & auto-load save slot
* Block game input while typing (great with Unity Explorer)
* Force-show mouse cursor (**F10** by default)
* Extract individual sprites — single frame, rendered sprite, or full sheet (see “Extracting Sprites” section below)
* User interfaces translated [via LLM] into 20 languages
* One-click extraction of all textures
    * Press **F1** → **Extract all textures**
    * Dumps to _**<font color=green>PLUGIN\_PATH</font>**/Textures/**<font color=green>NAME</font>**-**<font color=green>MD5</font>**.png_ (no duplicates)

<br class=Hide><br><center><font size=5>🛠️<font size=1> </font>**Modder-Facing Power Tools**<font size=1> </font>🛠️</font></center>
 ---
<meta/><center>Debugging   ![•](https://static.castledragmire.com/silksong/CoinSuperSmall.png)   Configurations   ![•](https://static.castledragmire.com/silksong/CoinSuperSmall.png)   Input   ![•](https://static.castledragmire.com/silksong/CoinSuperSmall.png)   Events   ![•](https://static.castledragmire.com/silksong/CoinSuperSmall.png)   Textures   ![•](https://static.castledragmire.com/silksong/CoinSuperSmall.png)   Windows</center><br><meta/>

* Configurable log levels + full stack traces on errors
* Tons of classes and functions to make development easier. Most useful include:
	* Configs
		* Configuration translations and properly sorted by order
			* Configurable delayed saving (instant on exit)
		* Dynamic string→string entries
		* Per-user-saveslot configs with backups
	* Input helpers (mouse visibility, joystick vector, input repeat delays, block specific input actions, etc.)
	* Priority-based event system with callbacks for major game events
	* Texture/sprite extraction and other texture extensions
	* Robust IMGUI window base class
		* Proper ordered mouse handling even over Unity Explorer windows
		* New mouse events: MouseMove, MouseEnterWindow, MouseLeaveWindow
		* Overridable event callbacks for GameEvents and all OnGUI event types
			* Strict event call ordering by window z-order and priority
			* Priority flagging to force windows topmost or bottommost
		* Safe drag/resize with config saving
		* Fake windows for mouse-only capture
		* Simple dialog, popup & progress bar windows
		* Screen-space geometry drawing
	* Reflection shortcuts, safe callbacks and coroutines, JSON utilities, translations & more

<font size=3>Full source + detailed class docs in the GitHub repo (coming soon)</font>

<br class=Hide><br><center><font size=5>⚙️ **Installation** ⚙️</font></center>
 ---
1. Install [BepInEx](https://www.nexusmods.com/hollowknightsilksong/mods/26)
1. Extract this mod’s zip into **Hollow Knight Silksong/BepInEx/plugins/dakusan/**
1. You should now have (for example): **Hollow Knight Silksong/BepInEx/plugins/dakusan/SilkDev.dll**
1. If you use my other mods — [NoClip](https://www.nexusmods.com/hollowknightsilksong/mods/478) — move them into the same **dakusan** folder.
1. Run the game – done!

You may also want to check out my [NoClip mod](https://www.nexusmods.com/hollowknightsilksong/mods/478) for development help.
<br>

<br><center><font size=5>🧩️ **Extracting Sprites** 🧩️</font></center>
 ---
* Set a shortcut in config (default: none – I ship it disabled because it’s included in many of my mods)
* **L**/**R**/**M**-click (when window open) *OR* press shortcut key → Shows list ordered by cursor distance (closest auto-selected and on top)
* Config **Show boxes around sprites …**: red outlines on hovered sprites, blue fill + name on closest <font size=1>[required for in-game-sprite clicking]</font>
* Click list item *OR* in-game-sprite to display:
	* Left → single sprite from sprite sheet <font size=1>(may include surrounding sheet parts)</font>
	* Right → rendered sprite <font size=1>(can be twitchy)</font>
	* Middle → full sprite sheet <font size=1>(lots of sprites)</font>
	* Double-left → open sprite in Unity Explorer <font size=1>(if installed; list item only)</font>
	* Repeated clicks on animated sprites cycle through different frames
* Left click image to save to: _**<font color=green>PLUGIN\_PATH</font>**/Textures/**<font color=green>DATE</font>**\_**<font color=green>TIME</font>**·**<font color=green>NAME</font>**.png_
* Right click sheet to toggle highlight

<br>![Silksong Sprites](https://static.castledragmire.com/silksong/SilksongSprites.webp)

<br class=Hide><br><br><center>![SilkDev Logo](https://static.castledragmire.com/silksong/SilkDevLogoSmall.jpg)</center>
<meta/><center>![Decor Bottom](https://static.castledragmire.com/silksong/DecorBottom.png)</center>