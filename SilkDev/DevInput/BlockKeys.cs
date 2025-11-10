using HarmonyLib;
using SilkDev.Internal;
using UnityEngine;

namespace SilkDev.DevInput;

//Blocks the keyboard from getting to the game
internal class BlockKeys : Window
{
	private readonly GUIStyle TextStyle=new(GUI.skin.label) { fontSize=40, alignment=TextAnchor.MiddleCenter, normal={ textColor=Color.red } };
	private const int Width=350, Height=65, HeightOffset=40;
	private static Config Conf => Config.C;

	public BlockKeys() : base(nameof(BlockKeys), true, 2000)
	{
		(BGTex, UnboundDraw, AlwaysCallUpdate)=(new Color(0, 0, 0, .5f).MakeTexture(), true, true);
		Visible=Conf.BlockGameInput;
		Conf.BlockGameInput.SettingChanged += (_, _) =>
			Visible=Conf.BlockGameInput;
	}

	//Add event listeners not handled elsewhere
	protected override void OnUpdate() => Misc.IFF(
		Conf.Key_BlockInput.IsDown(), //Handle keyboard shortcuts
		() => Conf.BlockGameInput.V=!Conf.BlockGameInput
	);

	protected override void DoLayout(int ID, Event Ev)
	{
		if(!Conf.ShowMessageWhenGameInputBlocked)
			return;
		Rect WindowRect=new(Screen.width/2-Width/2, HeightOffset, Width, Height);
		GUI.DrawTexture(WindowRect, BGTex);
		GUI.Label(WindowRect, "Keyboard blocked", TextStyle);
	}

	protected override bool IsMouseOverWindow(Vector2 MPos) => false; //Ignore mouse
}

//Block the keyboard from getting to the game
[HarmonyPatch(typeof(InControl.InputControlState), nameof(InControl.InputControlState.Set), [typeof(float), typeof(float)])]
internal static class Patch_InputControlState_Set {
	private static bool Prefix() => !(Config.C?.BlockGameInput ?? false);
}