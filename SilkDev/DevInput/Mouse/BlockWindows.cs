using BepInEx.Configuration;
using UnityEngine;

namespace SilkDev.DevInput.Mouse;

//Blocks mouse events from passing through UniverseLib (Unity Explorer) windows. Automatically initiated by the plugin.
internal class BlockWindows_UniverseLib(string Title, string CanvasRootName, ConfigEntry<bool>? CE, int Priority) : Window(Title, true, Priority)
{
	private static readonly RectOffset ResizeBorder=new(9, 9, 8, 9); //Help account for resizing (which they allow outside the friggin window)
	protected string CanvasRootName=CanvasRootName;
	private readonly ConfigEntry<bool>? CE=CE;
	public override bool Visible => true;

	protected override void DoLayout(int ID, Event CurEv) { }
	protected override bool IsMouseOverWindow(Vector2 MPos)
	{
		if(CE?.Value!=true)
			return false;

		Transform? PanelHolder=GameObject.Find("UniverseLibCanvas")?.transform;
		foreach(string Name in new string[] { CanvasRootName, "PanelHolder", Misc.Empty })
			if(!(PanelHolder?.gameObject.activeSelf ?? false))
				return false;
			else if(Name!=Misc.Empty)
				PanelHolder=PanelHolder.Find(Name)?.transform;

		foreach(Transform Child in PanelHolder!) {
			if(!Child.gameObject.activeSelf)
				continue;
			RectTransform RT=Child.GetComponent<RectTransform>();
			if(
				new Rect(RT.position, RT.rect.size).SetY(Screen.height-RT.position.y).Add( //The actual window rect
				new Rect(-ResizeBorder.left, -ResizeBorder.top, ResizeBorder.horizontal, ResizeBorder.vertical) //Resize border
			).Contains(MPos))
				return true;
		}

		return false;
	}
}

internal class BlockWindows_UnityExplorer(int Priority) :
	BlockWindows_UniverseLib(nameof(BlockWindows_UnityExplorer), "com.sinai.unityexplorer_Root", Internal.Config.C.BlockMouse_UnityExplorer, Priority) { }
internal class BlockWindows_BepInExConfigManager(int Priority) :
	BlockWindows_UniverseLib(nameof(BlockWindows_BepInExConfigManager), "com.sinai.BepInExConfigManager_Root", Internal.Config.C.BlockMouse_BepInExConfig, Priority) { }