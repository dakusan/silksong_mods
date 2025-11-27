<a id="readme"></a>
<div align="center">

# 🧩️ SilkDev — Plugin Developer Tools & Helpers
*Help develop your plugins*
</div>

<img src=Internal/LogoSmall.jpg alt="SilkDev Logo" align=right>

1. [Overview](#overview)
1. [Core Highlights](#core-highlights)
1. [Installation](#installation)
1. [Usage](#usage)
1. [Developer Notes](#developer-notes)
1. [Contributing, Credits, and License](#contributing-credits-license)
1. [Source Code Class Documentation](#source-code-class-documentation)
<br clear="all">

---
<a id="overview"></a>
## 📖️ Overview
**SilkDev** *[Plugin Developer Tools]* contains both code classes and in-game features for plugin development including: tools to simplify mod creation like for debugging, configurations, input controls, events, and windows.

[See Nexus Mods page for further details.](https://www.nexusmods.com/hollowknightsilksong/mods/510)

Built on top of the **[BepInEx](https://github.com/BepInEx/BepInEx/)** framework.

<a id="core-highlights"></a>
## ✨️ Core Highlights
- Block game input (great for Unity Explorer)
- Quickly enter into your save slot and skip intro screens
- Mouse cursor visibility and passthrough fixes
- Extract all textures **in memory** to `PLUGIN_PATH/Textures`
- Extract sprite textures — from original sprite sheet, by sprite render, or entire sprite sheet
- Debugging message log level and stack traces
- Tons of classes and functions to make development easier [See Documentation](#source-code-class-documentation). Most useful include:
    - Configs
        - Configurations properly sorted by order
            - Configurations can have a timeout inbetween saving to file (immediate on game exit)
        - Dynamic String->String config entries
        - Per-user-saveslot configurations
    - User input
        - Mouse visibility
        - Joystick direction and magnitude
        - Input repeat delays
    - Events
        - Event registration priority based callbacks
        - Event callbacks for major game events
    - Textures/Sprites
        - Extract sprite textures
        - Useful texture extensions
    - Windows
        - Powerful IMGUI Window class
            - Mouse events are only called if the mouse is over the window, or it is dragging
                - Custom handle all mouse events in order of zOrder
                - Adds mouse events: MouseMove, MouseEnterWindow, and MouseLeaveWindow
                - Takes into account UniverseLib (Unity Explorer) windows since they do not cancel the mouse themselves
            - Overridable event callbacks for GameEvents and all OnGUI event types
                - Strict event call ordering by window order and priority
                    - Can give priority that sets windows to bottom or topmost
            - Safe window moving and resizing with saving via a ConfigEntry
            - Fake windows can be created just for [mouse handling] events
        - Simple dialog, popup, and progress bar windows
        - Draw geometry based objects on screen

*(See the [Nexus Mods page](https://www.nexusmods.com/hollowknightsilksong/mods/510) for further details.)*

<a id="installation"></a>
## ⚙️ Installation
1. Install **[BepInEx](https://www.nexusmods.com/hollowknightsilksong/mods/26)**.
1. [Download](https://www.nexusmods.com/hollowknightsilksong/mods/510?tab=files) and extract this mod into: `Hollow Knight Silksong/BepInEx/plugins/dakusan`<br>
You should end up with paths like: `Hollow Knight Silksong/BepInEx/plugins/dakusan/SilkDev.dll`
1. (Optional) If you use other *Dakusan* mods — such as [NoClip](https://www.nexusmods.com/hollowknightsilksong/mods/478) — place them in the same `dakusan` directory.
1. Run the game

<a id="usage"></a>
## 🧭️ Usage
- Open the config window in-game with **F1** to toggle/set options.
- Default mouse toggle hotkey: **F10**.
- Default block game input hotkey: **None**.

<a id="developer-notes"></a>
## 🧩️ Developer Notes
- Source code organized under `SilkDev/`.

<a id="contributing-credits-license"></a>
## 🤝🏻 Contributing, Credits, and License
See [root project README](../#contributing) for details

---
<a id="source-code-class-documentation"></a>
# 📘️ Source Code Class Documentation
> These classes are designed to be useful across any Unity project. I plan to move them into a universal Unity plugin/mod in the near future.
* 📂️ `Configs`:
    * 📦️ `ConfigEntryT`: A ConfigEntry wrapper class that allows getting/setting the value without using `.Value`.
    * 📦️ `DynamicEnumConfig`: Creates a ConfigEntry<Enum> with dynamic values.
        * 💡️ Dictionary values are only used as display text in the configuration interface. Everything else uses the dictionary keys.
    * 📦️ `OrderedConfig`: A drop in wrapper for the `ConfigFile` class with the `.Bind()` functions. It optionally orders the config file sections and items by adding numbers to their front.
    * 📦️ `PerSaveConfig`: Enabled configurations **per-user-saveslot** with **backups**.
    * 📦️ `SpoilerPair`: Toggles the visibility of a pair of settings based upon if the spoiler has been reached yet.
* 📂️ `DevInput`:
    * 📂️ `Mouse`:
        * 📦️ `BlockWindows`: Blocks mouse events from passing through UniverseLib (Unity Explorer) windows
          * 💡️ Automatically initiated by the plugin.
        * 📦️🔢️ `Button`: Mouse button enums.
        * 📦️ `Dragger`: Keeps track of mouse dragging states and distances.
        * 📦️ `ResizeDragControl`: Adds controls on window’s for drag resizing, and can also handle window moving just like `GUI.DragWindow()`.
            * 💡️ Can save to a config variable when moving/resizing finishes.
        * 📦️ `Visibility`: Handle mouse cursor visibility.
            * 🗒️ Provides a delegate to force mouse visibility via functions subscriptions.
    * 📦️ `BlockInput`: Blocks keyboard and controllers from getting to the game.
        * 💡️ Can block all or specific `InControl` actions or joystick movements.
        * 💡️ Can be set to fully block via shortcut key.
        * 💡️ Has an unobstructive translucent popup that shows when keys are blocked.
        * 💡️ Automatically initiated by the plugin.
    * 📦️ `Joystick`:
        * 🔢️ `Direction`: Joystick directions enum.
        * ⚙️ `GetOrdinalDirectionAndMagnitude`: Get the direction a joystick is pointed in and the magnitude.
            * 💡️ Includes a minimum magnitude and an angle deviation that the joystick angle range must be within for triggering.
    * 📦️ `InputRepeatDelay<EmbeddedType>`: Returns key presses after a repeat delay. Change in keys ignores delay.
        * 🧾️🗒️ Types it can monitor:
            * 📦️ `ConfigEntry<KeyboardShortcut>`
            * 📦️ `KeyCode`
            * 📦️ `IInputControl` (from `InControl`, e.x. `InputManager.ActiveDevice.LeftTrigger`)
            * 📦️ `Joystick.Direction`+`IsLeftStick`
                * 💡️ `AngleDeviation` and `MinMagnitude` sent to `GetOrdinalDirectionAndMagnitude` are set in the `InputRepeatDelay` parent.
        * 💡️ Each `InputType` can have an embedded value of type `<EmbeddedType>` that is returned from `IsReadyValue`.
        * 💡️ This class is also very useful as a generic IsPressed key watcher, even with `RepeatDelay` set to 0.
        * 🗒️ All operations act on the assumption only 1 key can be pressed at once, except `IsReadyInputTypes` and `GetAllPressedInputs` which work on multiple.
    * 📦️ `Util`:
        * ⚙️ `AnyKeyOrButtonPressed`: Check if any key or button is currently pressed.
        * ⚙️ `MousePos`: Get the mouse position in normal screen coordinates (upper left=0,0).
* 📂️ `Events`:
    * 📦️ `EventRegister`: Register multiple events against a generic key.
    * 📦️ `GameEvents`: Subscribe to game events (Currently: `Update`, `Game Loaded`, `Game Saved`).
    * 📦️ `PrioritizedEvents` (base `PrioritizedValues`): Event lists with call priority and exception handling.
    * 📦️ `SingleDelegate`: Wrapper for singlecast delegates with custom equality checking.
* 📂️ `Windows`:
    * 📦️ `Window`:
        * 🧾️🗒️ Abstract class for a Unity based `GUI.Window()`. Features:
            * 🗒️ Makes sure windows have a unique ID and custom handle all mouse events in order of zOrder. All other events are processed naturally.
            * 🗒️ Mouse events are only called if the mouse is over the window, or it is dragging. Also adds `MouseMove`, `MouseEnterWindow`, and `MouseLeaveWindow`.
            * 🗒️ Safe window moving and resizing.
            * 🗒️ Optionally saves/restores window position via a ConfigEntry.
            * 🗒️ Has a close button with optional event action.
            * 🗒️ Can give priority that sets windows to bottom or topmost.
            * 🗒️ Takes into account UniverseLib (Unity Explorer) windows at `Priority=-100` since they do not cancel the mouse themselves.
            * 🗒️ Strict event call ordering by window order and priority. Full event system call ordering is available at the top of `Window.cs`.
            * 🗒️ Options to call `PreOnGUI` and `Update` even if not visible.
            * 🗒️ Fake windows can be created just for mouse handling.
            * 🗒️ Overridable event callbacks for `GameEvents` and `OnGUI` event types.
        * 🧾️ TODO:
            * 🗒️ Catch events before UniverseLib so we can cancel events to their focused windows.
            * 🗒️ Catch all windows and insert them into the chain, even if they aren’t made as Windows.
    * 📦️ `PopupMessage`:
        * 💡️ Only 1 popup message shows at a time, determined by Stack (FILO).
        * 💡️ Popup messages animate opening and closing (2 popups show at a time during this).
        * 💡️ Automatic “Press any key to close this message.” message.
    * 📦️ `DialogWindow`: A window that contains a message and optionally ok/cancel buttons.
    * 📦️ `ProgressBarWithLogs`: A progress bar window with a message line and and 2 logs below it (an error and a normal).
        * 💡️ Any public variables can be updated at any time and the window will auto adjust on the next frame draw.
    *  📦️ `DrawGeometry`: Draws on-screen geometry measured in pixels. Mouse interaction is ignored.
        * 💡️ Current shapes: Dot, Square, Rectangle
        * 💡️ Supports both colors and textures for both main/border and background.
        * 💡️ Border side textures are rotated 90° clockwise so a single texture works for top/bottom + sides.
* 📂️ `Textures`:
    *  📦️ GameObjectSprites.cs: Opens the **Game Object Sprites** window on keyboard shortcut, which allows you to browse and save the sprites/textures that were under your mouse.
    *  📦️ Extensions
        * 📦️🔌️ `Texture2D` extensions:
            * ⚙️ `Rect`.`ConvertTexCoords(Texture2D)`: Convert 2D absolute sprite texture coordinates into scaled 0-1.0 floats.
            * ⚙️ `Texture2D`.`Size()`: Get a Vector2 of the width/height.
            * ⚙️ `Texture2D`.`TDestroy()`: Destroy the texture via `UnityEngine.Object.Destroy()`.
            * ⚙️ `Color`.`MakeTexture()`: Create a 2x2 pixel texture to create solid colors.
            * ⚙️ `Texture2D`.`ReColor(Color)`: Replace the pixels inside a texture with a color.
            * ⚙️ `Texture2D`.`ToReadable(Rect? TexCoords=null, Vector2? ResizeDimensions=null)`: Copy an unreadable texture (`Texture2D.isReadable`) to a readable texture.
                * 🗒️ TexCoords when set will specify the coordinates to extract.
                * 🗒️ ResizeDimensions when set will specify the final texture size.
            * ⚙️ `SpriteRenderer`.`CaptureToTexture`: Renders a SpriteRenderer (with full transparency) into a Texture2D.
* 📂️ `Hooks`:
    * 📦️ `DynamicHook`: Dynamically add a Harmony method hook by class and function name.
        * 🗒️ Allows harmony hooks without including assemblies in compiles.
    * 📦️ `LiveHook`: Safe enable toggling of harmony method hooks.
* 📂️ `JSON`:
    * 📦️ `FieldPropConverter`: Convert a class by setting its fields and properties, no matter their accessibility status.
    * 📦️ `SortedConverter`: Sorts lists and dictionary by numeric or alphabetical order (Good for diffing).
* 📦️ `Catcher`: Used to call delegates wrapped within a try/catch that will output a stack trace when caught (if config is on).
    * 💡️ Also supports lists of actions and multicast delegate chains.
* 📦️ `Extensions`:
    * 💡️ Automatically initiated by the plugin.
    * 📦️🔌️ `Rect` [math] extensions:
        * ⚙️ `Rect`.&#91;(Set, Add)(X, Y, Width, Height)](float), `Rect`.&#91;(Set, Add)(Pos, Size)](Vector2).
            * ⚙️ Also includes `Set` variant for each in the form <code>Set**VAR**(Func<**TYPE**, **TYPE**>)</code>. Example: `.SetX(X => X*5)`
        * ⚙️ `Rect`.[`Add(Rect)`, `Multiply(Rect)`, `Inverse()`, `Grow(float, float)`].
        * ⚙️ `Vector2`.`CenterIn(Vector2)`: Center 2 sized rects against each other by size.
    * 📦️🔌️ Delegate extensions:
        * ⚙️ `Delegate`.`Toggle(Delegate Handler, bool Enable)`: Adds/Removes a delegate from a multicast delegate chain.
    * 🔌️ Generics extensions:
        * ⚙️ `IEnumerable<T>`.`ForEach(Action<T> Action)`: Run `ForEach` on an `Enumerable`.
        * ⚙️ `IEnumerable<(int Index, T Value)>` `IEnumerable<T>`.`Entries()`: Get Index and Value pairs from `Enumerable`.
        * ⚙️ `IEnumerator`.`AsEnumerable<T>()`: Make `IEnumerator` an `Enumerable`.
        * ⚙️ `IEnumerator<T>`.`AsEnumerable<T>()`: Make `IEnumerator` an `Enumerable`.
        * ⚙️ `TValue?` `Dictionary<TKey, TValue>`.`Get(TKey Key)`: Get key from a dictionary, returning null if not found. [Wraps `Dict.GetValueOrDefault`]
    * 🔌️ Unity stuff:
        * ⚙️ `UnityObject<T>`.`NullSafe`: Returns null if the object or unity object are null.
            * 💡️ This allows null operator chaining with UnityObjects that may have been destroyed by Unity.
        * ⚙️ `Vector2` `Screen.Size => new(Screen.width, Screen.height)`;
        * ⚙️ `Task`.`AsCoroutine(Misc.Ref<Exception?>)`: Turn a Task into a Coroutine IEnumerator
    * 📦️🔌️ Streams:
        * ⚙️ `Stream`.`ReadAllAndCloseS()`: Reads the entirety of a stream into a string.
        * ⚙️ `Stream`.`ReadAllAndCloseB()`: Reads the entirety of a stream into a byte array.
* 📦️ `FileOps`:
    * ⚙️ *Generic file operations* so as to not have to include `System.IO`: `WriteFile(byte[] or string)`, `WriteFileAsync(byte[] or string)`, `AppendFile`, `ReadFile`, `ReadFileBytes`, `PathCombine(string...)`, `InvalidNameChars`, `FixFileName`, `GetFileName`, `GetDirectoryName`, `GetDirFiles`, `FileExists`, `DirectoryExists`, `CreateDirectory`, `FileCopy`, `FileMove`, `FileDelete`, `JSON.DeserializeJson`
    * 🧾️ JSON Functions:
        * ⚙️ `SerializeToJSONSorted(object)`: See `JSON.SortedConverter` above
        * ⚙️ `SerializeToJSON(object, bool Compact=false)`: Calls `JsonConvert.SerializeObject`. Changes to unix line encoding.
        * ⚙️ `DeserializeJson<OutputTypeT, FieldPropConverterT>(string Data)`: Deserializes data through `JsonConvert.DeserializeObject<OutputTypeT>`, but runs through `JSON.FieldPropConverter<FieldPropConverterT>` (see above).
        * ⚙️ `SerializeToJSON<T>(object, bool Compact=false, bool OutputNulls=true)`: Runs specified class through `FieldPropConverter<T>`. Changes to unix line encoding.
        * 🧾️ Shorthands:
            * ⚙️ `Ser(object Obj)` => `SerializeToJSON(Obj)`
            * ⚙️ `LogSer(object Obj)` => `Log.Info(SerializeToJSON(Obj))`
    * 🧾️ Loading resources:
        * ⚙️ `LoadEmbeddedResource(string Name)`: Loads an embedded resource by name from the calling assembly.
        * ⚙️ `LoadEmbeddedResource(string Name, Assembly Assembly)`: Loads an embedded resource by name from the given assembly.
        * ⚙️ `LoadLocalFileOrResource(string Name)`: If local file is available, use that. Otherwise, loads an embedded resource by name from the calling assembly.
        * ⚙️ `GetResources()`: Returns the list of resources in the calling assembly.
* 📦️ `Log`:
    * ⚙️ `Log`.`Info(string Message)`, `Log`.`Info(object ObjToSerialize)`: Sends out log lines with log level set by BepInEx configuration.
    * ⚙️ `Log`.`Error(string Message)`: Sends log message as error.
* 📦️ `Misc`:
    * ⚙️ `InitSingleton`: Implement singletons.
    * ⚙️ `SanitizeRichString`: Sanitize a richText string.
    * ⚙️ `SaveToClipboard`: Save to clipboard.
    * ⚙️ `SteamUsername`: Get steam username.
        * 💡️ Note: May not be available until a few seconds after the game loads.
    * ⚙️ `GetPluginPath`: Gets the path of the calling plugin.
    * ⚙️ `UnityExplorer_Inspect`: Open Unity Explorer inspection on game object (if plugin is loaded)
    * ⚙️ `Ref<T>(T Value)`: Simple reference class
    * ⚙️ `IFF(bool Cond, Action CallOnTrue)`: If-statement used for bypassing curley-cue function blocks.
    * ⚙️ `const char NewLine='\n'`;
    * ⚙️ `const string Empty=""`;
* 📦️ `Reflectors`
    * 📦️ `RField<ObjType, FieldType>`, `RProp<ObjType, PropType>`, `RMethod<ObjType, RetType>`: Get via reflection fields, properties, or methods, with attached object for convenient `Get`/`Set`/`Invoke`/`(typecast)`.
        * 💡️ Attached object can be changed via `public ObjType Obj`.
* 📦️ `TypedDisposer<T>(T Target, Action<T> Disposal) : IDisposable`
    * 🗒️ Create disposable objects for RAII use. [Set with `using` for destruction at end of scope].
    * ⚙️ `Detach()`: Detach object so it won’t be disposed.