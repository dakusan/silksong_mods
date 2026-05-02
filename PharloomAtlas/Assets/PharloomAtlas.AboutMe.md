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
img.SmallIcon { position:relative; top:3px; margin-bottom:1px; }
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

<meta/><center>![Decore Top](https://static.castledragmire.com/silksong/DecorTop.png)</center>
<br><center>![Praying small](https://static.castledragmire.com/silksong/PrayingSmall.png)   ![Title](https://static.castledragmire.com/silksong/PharloomAtlasTitle.jpg)   ![Holding map small](https://static.castledragmire.com/silksong/HoldingMapSmall.png)</center>
<br><center><i>The ultimate Silksong map overhaul — feature-packed, customizable, and crafted for completionists.</i></center>

<br><br><center><font size=4>📜️ <b>Explore. Track. Master Pharloom.</b></font></center>
<br><center>![Decore bottom](https://static.castledragmire.com/silksong/DecorBottom.png)</center>

<br class=Hide><br class=Hide><font size=5>                <b>Features at a Glance</b></font>
---
* Everything is optional and configurable (**F1**) — toggle features to suit your playstyle.<br>Enabled by default: *Sidebar &amp; map icons* + *Highlight Hornet pin*
* Full support for 🎮️ controllers, ⌨️ customizable keyboard keys, and 🖱️ mouse.
    * Zoom   ![•](https://static.castledragmire.com/silksong/CoinSuperSmall.png)   Pan   ![•](https://static.castledragmire.com/silksong/CoinSuperSmall.png)   Place+Label Markers   ![•](https://static.castledragmire.com/silksong/CoinSuperSmall.png)   Select Icons   ![•](https://static.castledragmire.com/silksong/CoinSuperSmall.png)   Sidebar   ![•](https://static.castledragmire.com/silksong/CoinSuperSmall.png)   Save Values
* **Icons, Markers &amp; Pins**
    * **<img src="https://static.castledragmire.com/silksong/BenchSmall.png" align="middle" class=SmallIcon alt="Bench Icon"> Icons**
        * All notable location data imported from [MapGenie.io](https://mapgenie.io/hollow-knight-silksong/maps/pharloom) and displayed on the in-game map.
        * Progress tracking — found icons automatically become invisible.
        * Icon categories and category groups can be toggled through 3 states:
            * **Show all**: Displays everything; found icons translucent/greyed (config: *Found icon color [HSL]*).
            * **Show Normal**: Hides found icons; hide unaccessible <font size=1>(config: *Hide unaccessible icons*)</font> and undiscovered area <font size=1>(config: *Show undiscovered icons*)</font> icons. *[Both coming soon]*
                * Full data sets incomplete; requirements/needs experimental—see **Saved Values Window** below.
                * *“Hide unaccessible icons” off by default until databases complete.*
            * **Show None**: No icons visible.
        * Detailed info with images for each icon on the sidebar. Includes clickable/selectable item links, selected-item history, and requirements to start/finish wishes, objectives, store items, etc. See [here](https://www.nexusmods.com/hollowknightsilksong/articles/141) for more information.
        * Multiple icon sets supported — add your own by placing “**Icons-\*.png**” files in the plugin directory.
        * <img src="https://static.castledragmire.com/silksong/RingSmall.png" align="middle" class=SmallIcon alt="Ring Icon"> **Search window**: Find icons by name or description. Mouse+Keyboard only.
        * **Saved Values Window**: Watch live save data updates and link discoveries for crowdsourcing final data files.
    * <img src="https://static.castledragmire.com/silksong/CompassIconSmall.png" align="middle" class=SmallIcon alt="Hornet Compass icon"> **Hornet Pin**
        * Always show Hornet’s pin — compass not required.
        * Choose from 5 animations to highlight Hornet for better visibility.
        * Recenter the map on your character instantly.
    * <img src="https://static.castledragmire.com/silksong/SmallMarker.png" align="middle" class=SmallIcon alt="Marker Icon"> **Markers**
        * <img src="https://static.castledragmire.com/silksong/LabelIcon.png" align="middle" class=SmallIcon alt="Label Icon">️ **Marker Labels**: Add labels so as to never forget what a marker means.
        * More markers: Gives 99 of every marker type.
        * Marker mode always available, even without owning any markers.
    * **Set Icon Size**: Adjust icon, pin, and marker size (consistent size across zooms by default).
* <img src="https://static.castledragmire.com/silksong/MapSmall.png" align="middle" class=SmallIcon alt="Map Icon"> **Map Features**
    * **Auto Map**: Auto-reveal visited rooms once you have their map — no benches or quill required.
    * **Unlock Map Options**
        * Unlock *The Moss Grotto* map (the beginning area) for early inventory map access.
        * Unlock all maps.
        * Unlock all markers.
        * <details><summary>Spoiler</summary>Reveal the Abyss map.</details>
    * <img src="https://static.castledragmire.com/silksong/HornetDashAttackSmall.png" align="middle" class=SmallIcon alt="Hornet Dash Attack Icon"> “Infinite” zooming in/out, with configurable speed, around: Markers, icons, mouse cursor, marker cursor, and the screen center.
    * Tunable panning speeds for both normal and marker modes, with unlocked borders for full freedom.
    * Stops map position from jumping around when switching between marker modes.
    * *Note:* The “wide” (general-area view) map is not supported.
* **Other Features**
    * <img src="https://static.castledragmire.com/silksong/PaintSmall.png" align="middle" class=SmallIcon alt="Paint Icon"> Interface color customization.
    * <img src="https://static.castledragmire.com/silksong/DeathCacoonSmall.png" align="middle" class=SmallIcon alt="Death Cacoon Icon"> Per-user-saveslot configs with automatic backups (required for marker labels).
    * <img src="https://static.castledragmire.com/silksong/Phonograph.png" align="middle" class=SmallIcon alt="Phonograph Icon"> User interfaces translated [via LLM] into 21 languages.
    * <img src="https://static.castledragmire.com/silksong/Fireball.png" align="middle" class=SmallIcon alt="Fireball Icon"> Error log upload support. See [here](https://www.nexusmods.com/hollowknightsilksong/articles/141) for more information.

<br class=Hide><br><center><font size=5><img src="https://static.castledragmire.com/silksong/VGAtlasLogo.png" align="middle" class=SmallIcon>                  <b>Web version</b>                  <img src="https://static.castledragmire.com/silksong/VGAtlasLogo.png" align="middle" class=SmallIcon></font></center>
 ---
Fully featured web version [here](https://vgatlas.dakusan.com). No install—just upload your save file.<br>
100% client-side; your data never leaves your browser. <font size=2>(please still consider endorsing!)</font>

<br class=Hide><br><center><font size=5><img src="https://static.castledragmire.com/silksong/KeysSmall.png" align="middle" class=SmallIcon>                    <b>Installation</b>                    <img src="https://static.castledragmire.com/silksong/KeysSmallRev.png" align="middle" class=SmallIcon></font></center>
 ---
1. Install [BepInEx](https://www.nexusmods.com/hollowknightsilksong/mods/26)
1. Extract this mod’s zip into **Hollow Knight Silksong/BepInEx/plugins/dakusan/**
1. You should now have (for example): **Hollow Knight Silksong/BepInEx/plugins/dakusan/SilkDev.dll**
1. If you use my other mods — [Plugin Developer Tools](https://www.nexusmods.com/hollowknightsilksong/mods/510), [NoClip](https://www.nexusmods.com/hollowknightsilksong/mods/478) — move them into the same **dakusan** folder.
1. Run the game and enjoy your upgraded map!

<br><font size=3><ins><b>Plugin Notes</b></ins></font>
<br><font size="1" style="display:inline-block;width:9px">   </font>💡️<font size=1> </font>Tip: disable advanced + shortcut configs to declutter.
* All plugin files now live neatly under the **dakusan** directory.
* Includes my shared library: [Plugin Developer Tools (SilkDev)](https://www.nexusmods.com/hollowknightsilksong/mods/510)

<br><center> <font size=5><img src="https://static.castledragmire.com/silksong/MaskSmallRev.png" align="middle" class=SmallIcon>   <b>Roadmap &amp; Community Goals</b>   <img src="https://static.castledragmire.com/silksong/MaskSmall.png" align="middle" class=SmallIcon></font></center>
 ---
Open sourcing on github soon! *This and the community-voted features got put on the backburner due to NexusMods warning me that I couldn’t tie endorsements to offering new functionality* 😟.

[Community-voted features](https://forums.nexusmods.com/topic/13527956-shape-pharloom-atlas-vote-now-for-your-favorite-feature/).

<br><font size=3><i>Planned poll options (suggest more anytime!):</i></font>
* Mini-map overlay
* Traveled-path / trace recording (like “Hero’s Path Mode” from Zelda: BotW), with persistent history
* Icons on the quick-map
* “Best route” guidance to any icon
* Hollow Knight backport
* NPC tracking *[partially implemented]*
* Add (and submit) new icons
    * Monster tracking
* Add (and submit) icon metadata [requirements, needs, effects, notes, etc]
* Hide icons until an area is mapped *[partially implemented. Currently tied to linked icons]*
* Textured map (Google Maps terrain-style)
* Teleport to benches
* Precise bench visit tracking (currently just checks scene visits; planned: detect actual bench interactions)
* Show only found icons
* Misc non-map features (suggestions welcome!) [I know this game’s codebase pretty intricately at this point.]

<br><center><font size=5><img src="https://static.castledragmire.com/silksong/SilkSpoolSmallRev.png" align="middle" class=SmallIcon>         <b>Help Improve the Atlas</b>         <img src="https://static.castledragmire.com/silksong/SilkSpoolSmall.png" align="middle" class=SmallIcon></font></center>
 ---
Many icons remain unlinked (and unchecked) — and you can help! See the **Saved Values Window** section below for details.
<br>![Unlinked icons](https://static.castledragmire.com/silksong/Unlinked.jpg)

<br>Icon categorization also needs refinement. A first pass is complete, but more cleanup is welcome.

<br>Includes two default icon sets: one with unique icons per item and another (Circles) with a consistent icon for each category.

<br>![Icons - From game](https://static.castledragmire.com/silksong/Icons-FromGame-Small.png)                      ![Icons - Circles](https://static.castledragmire.com/silksong/Icons-Circles-Small.png)

<br>I did my best with the images in the first set, though I’m no graphic designer — some icons don’t scale well at smaller sizes (like 0.5x).

<br>To contribute:
* Download and edit [Icons-FromGame](https://static.castledragmire.com/silksong/Icons-FromGame.png) or [Icons-Circles](https://static.castledragmire.com/silksong/Icons-Circles.png) → place in **BepInEx/plugins/dakusan/**
* See [Categories.json](https://silksong.castledragmire.com/Categories.json) and [Items.json](https://silksong.castledragmire.com/Items.json) for icon mapping numbers.
* Appreciated updates could include better images, per-NPC or per-map icons, and an expanded Circles set with more item-specific icons.
* Expanding the icon set beyond the current image limit will require additional code (quick fix). I’ll also probably need to add bounding shapes for the icons.

<br><center><font size=5><img src="https://static.castledragmire.com/silksong/RunAnimation.webp" align="middle" class=SmallIcon>             <b>Input &amp; Controls</b>             <img src="https://static.castledragmire.com/silksong/RunAnimationRev.webp" align="middle" class=SmallIcon></font></center>
 ---
![Key mappings](https://static.castledragmire.com/silksong/KeyMappingsSmall.png)
<br>All interfaces support 🎮️ controller, ⌨️ keyboard, and 🖱️ mouse for every feature.
<br><font size=1>(Search window does not support controller input.)</font>
<br>All keyboard bindings are customizable in the config.

<br>**Map Mouse Controls:**
* Scroll wheel — Zoom in/out
* Left-click drag — Pan the map
* Left-click icon — Show info on sidebar
* Hover over icon — Show title on sidebar
* Left-click marker or its label — Add/edit label (deleted markers retain labels until save reload). <font size=1>The marker has to be in the exact correct position to restore the label.</font>
* Right-click — Add/delete marker<br>*Note:* Marker placement can be finicky near clustered markers — Team Cherry’s original marker auto-selection logic wasn’t built for mouse use.

<br>Via config you can:
* Auto-enable mouse when sidebar or Saved Values windows are open
* Toggle mouse manually via the included *Plugin Developer Tools* plugin (default: **F10**)

<br><center><font size=5><img src="https://static.castledragmire.com/silksong/MusicBoxSmallRev.png" align="middle" class=SmallIcon>          <b>Saved Values Window</b>          <img src="https://static.castledragmire.com/silksong/MusicBoxSmall.png" align="middle" class=SmallIcon></font></center>
 ---
Community help is crucial to nail every icon! I’ve matched 550/1,908 via my [Pin Finder mod](https://www.nexusmods.com/hollowknightsilksong/mods/772) (error-prone) and spent countless hours on requirement/need chains, but it’s a huge dataset I can’t finish alone.

<br>Linking values is super simple, so pitch in! With endorsements and submissions, we could map all Pharloom’s monsters, secrets, and more. Let’s do this together. 🤝🏻

<br>![Save Values Window](https://static.castledragmire.com/silksong/SavedValuesWindow.png)
<br>Use “Show Linked” on the sidebar to view icons not yet associated with save data.
<br>![Values Window Options](https://static.castledragmire.com/silksong/ValuesWindowOptions.png)

<br>To link icons and values:
* Select an icon on the map (green highlight) and a value in the Saved Values window.
* Click **Save** or **Save+Send**:
    * **Save** → Local JSON
    * **Send** → Uploads data to me for crowdsourcing

<br>This will also improve icon alignment (MapGenie coordinates aren’t always precise).
<br>If this goes well, I’ll automate for in-game JSON downloads — but there will **never** be auto-updates or external calls without consent.

<br>This is the only window that persists without the map open.
<br>🎮️ Controller fully supported here too.
<br>Some values are hard to identify — e.g., some secrets are hidden behind “\*Remasker\*” flags.

<br>**Icon accessibility requirements and fulfillment needs [The Chain System](https://www.nexusmods.com/hollowknightsilksong/articles/141) also need additions and confirmation.** For now that’s only possible via json editing and submission. This feature is also currently experimental.
<br>Here is a small part of the current dependency graph of data I’ve been able to compile.
<br>![Chain System](https://static.castledragmire.com/silksong/ChainSystem.jpg)

<br>**If anyone is interested in helping get the database in this project complete, <font color=green><i>PLEASE contact me</i></font>**. If there is enough interest I’ll put up a discord.

<br><ins>Up-to-date JSON files:</ins>
* Icon data and relationships:                   <font size=1 style="padding-right:0"     >  </font>              [Items.json](https://silksong.castledragmire.com/Items.json) <font            size=1 style="padding-right:2px"  >    </font>        [Compact](https://silksong.castledragmire.com/Items.json?CompactJSON=1)
* Icon categories (used for hiding/filtering):   <font size=1 style="padding-right:1px" >    </font         >     [Categories.json](https://silksong.castledragmire.com/Categories.json)<font   size=1 style="padding-right:0"       > </font     >   [Compact](https://silksong.castledragmire.com/Categories.json?CompactJSON=1)
* Item → save-value bindings (progress tracking):<font size=1 style="padding-right:1px"     ></font             > [ItemFinder.json](https://silksong.castledragmire.com/ItemFinder.json)  <font size=1 style="padding-right:2px"     > </font        >[Compact](https://silksong.castledragmire.com/ItemFinder.json?CompactJSON=1)
* Category groupings and misc progress data:     <font size=1 style="padding-right:0"     >  </font              >[Misc.json](https://silksong.castledragmire.com/Misc.json)            <font   size=1 style="padding-right:1px"     > </font        >[Compact](https://silksong.castledragmire.com/Misc.json?CompactJSON=1)

<br class=Hide><br><center><font size=5><img src="https://static.castledragmire.com/silksong/FireSmall.png" align="middle" class=SmallIcon>                   <b>Bonus Rant</b>                   <img src="https://static.castledragmire.com/silksong/FireSmallRev.png" align="middle" class=SmallIcon></font></center>
 ---
Nexus re-encoded my GIFs into WebP and butchered the quality 😅️
<br>Here’s the original, as intended:
<br>![Hornet marker animations](https://static.castledragmire.com/silksong/SilksongAnimations.gif)

<br><center><font size=5>📜️              <b>Immediate Todos</b>            📜️</font></center>
 ---
<center><b>Coming soon</b></center><br><meta/>

* Finish the chain system:
    * Hide icons until an area is mapped
    * Hide icons until accessible
    * ~~Cross off requirements/needs in sidebar descriptions~~
* ~~Fix link clicking in the search window~~