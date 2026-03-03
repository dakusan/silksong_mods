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

<br><center><font size=6>**Hollow Knight Silksong: Pin Finder**</font></center>
<center><font size=4>Automatically scans every scene and dumps the exact position + save-flag of every collectible, bench, switch, lore tablet, etc.</font></center>

<br class=Hide><br><center><font size=5>📖️ **Overview** 📖️</font></center>
 ---

Developer utility used to create accurate data files for mods like [Pharloom Atlas](https://www.nexusmods.com/hollowknightsilksong/mods/755).
<br>Runs once (or on demand), writes a clean JSON to the plugin folder. Process can be paused and restarted without losing anything.
<br>You can find my latest copy of the processing, with synced changes from the last 3 months, [here](https://silksong.castledragmire.com/Pins.json).
<br>Settings dialog translated [via LLM] into 20 languages.

<br class=Hide><br><center><font size=5>⚙️ **Installation** ⚙️</font></center>
 ---
1. Install [BepInEx](https://www.nexusmods.com/hollowknightsilksong/mods/26)
1. Extract this mod’s zip into **Hollow Knight Silksong/BepInEx/plugins/dakusan/**
1. You should now have (for example): **Hollow Knight Silksong/BepInEx/plugins/dakusan/SilkDev.dll**
1. If you use my other mods — [Plugin Developer Tools](https://www.nexusmods.com/hollowknightsilksong/mods/510), [NoClip](https://www.nexusmods.com/hollowknightsilksong/mods/478) — move them into the same **dakusan** folder.
1. Launch game → open config (F1) → click “Start Process”

Includes my shared library: [Plugin Developer Tools (SilkDev)](https://www.nexusmods.com/hollowknightsilksong/mods/510)

<br class=Hide><br><center><font size=5>️🗣️ **Bla bla bla** 🗣️</font></center>
 ---
This was my first attempt (from back in September) at matching save values to marker locations from MapGenie.io for my [Pharloom Atlas mod](https://www.nexusmods.com/hollowknightsilksong/mods/755).

<br>While this won’t be useful to most people, the basis behind it could be useful for other projects (like if and when I backport Pharloom Atlas to Hollow Knight). Source code coming soon (when I release the source for Pharloom Atlas).

<br class=Hide><br><br><center>![Pin Finder Logo](https://static.castledragmire.com/silksong/PinFinderLogoSmall.jpg)</center>
<meta/><center>![Decor Bottom](https://static.castledragmire.com/silksong/DecorBottom.png)</center>