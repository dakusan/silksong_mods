using InControl;
using SilkDev.Textures;
using System.Linq;
using UnityEngine;

namespace SilkDev.DevInput;

//Blocks the keyboard from getting to the game
public class BlockKeys : Windows.Window
{
	private static Internal.Config Conf => Internal.Config.C;
	private const int HeightOffset=40;

	private readonly GUIStyle TextStyle=new(GUI.skin.label) { fontSize=40, wordWrap=false, alignment=TextAnchor.MiddleCenter, normal={ textColor=Color.red } };

	//Check callers for action to take
	public readonly struct CAParams(string ActionName=default!, float Value=default, Vector2 StickValue=default)
	{
		public readonly string	ActionName	=ActionName ; //Only set in Check_Actions
		public readonly float	Value		=Value		; //Only set in Check_Actions
		public readonly Vector2	StickValue	=StickValue	; //Only set in Check_LStickMove, Check_RStickMove
	}
	public enum CAResults { Ignore, Allow, Block }
	public class CheckActions(string Name) : Events.PrioritizedEvents<System.Func<CAParams, CAResults>>(Name)
	{
		public bool AllowResult(string ActionName=default!, float Value=default, Vector2 StickValue=default)
		{
			CAParams P=new(ActionName, Value, StickValue);
			foreach(var H in Handlers)
				switch(H.Handler(P)) {
					case CAResults.Allow : return true;
					case CAResults.Block : return false;
					case CAResults.Ignore: continue;
				}
			return true;
		}
	}
	public static CheckActions Check_All		=new(nameof(Check_All		));
	public static CheckActions Check_Actions	=new(nameof(Check_Actions	));
	public static CheckActions Check_LStickMove	=new(nameof(Check_LStickMove));
	public static CheckActions Check_RStickMove	=new(nameof(Check_RStickMove));

	//Handle block game input config option
	static BlockKeys()
	{
		_=Check_All.Toggle(BlockAllConfig, Conf.BlockGameInput);
		Conf.BlockGameInput.SettingChanged += static (_, _) =>
			Check_All.Toggle(BlockAllConfig, Conf.BlockGameInput);
	}
	private static CAResults BlockAllConfig(CAParams _) => CAResults.Block;

	//Init
	internal BlockKeys() : base(nameof(BlockKeys), false, 2000, true) { }
	protected override void OnInit() =>
		(BGTex, UnboundDraw)=(new Color(0, 0, 0, .5f).MakeTexture(), true);

	//Window events
	protected override void OnUpdate()
	{
		//Handle block game input shortcut key
		if(Conf.Key_BlockInput.IsDown())
			Conf.BlockGameInput.V=!Conf.BlockGameInput;

		//Set window visibility based upon if any action lists have items
		Visible=
			Conf.MessageOnInputBlocked
			&& (   Check_All		.HasAny
				|| Check_Actions	.HasAny
				|| Check_LStickMove	.HasAny
				|| Check_RStickMove	.HasAny
			);
	}

	protected override void DoLayout(int ID, Event Ev)
	{
		//Create the message
		GUIContent Message=new("Game action blocks: "+(
			  Check_All.HasAny ? "All"
			: string.Join(", ", [.. new string?[] {
				Check_Actions	.HasAny ? "Some actions": null,
				Check_LStickMove.HasAny ? "LStick Move"	: null,
				Check_RStickMove.HasAny ? "RStick Move"	: null,
			}.Where(static Str => Str!=null)])
		));

		//Draw the message
		Vector2 StrSize=TextStyle.CalcSize(Message)+new Vector2(1, 1), GrowSize=new(10, 3);
		Rect WindowRect=new(new Vector2((Screen.width-StrSize.x)/2, HeightOffset+GrowSize.y), StrSize);
		GUI.DrawTexture(WindowRect.Grow(GrowSize), BGTex);
		GUI.Label(WindowRect, Message, TextStyle);
	}

	protected override bool IsMouseOverWindow(Vector2 MPos) => false; //Ignore mouse

	//Useful shortcut functions
	public static CAResults AllowAction	(CAParams P, string ActionName			) => P.ActionName==				ActionName	? CAResults.Allow : CAResults.Block;
	public static CAResults AllowActions(CAParams P, params string[] ActionNames) => ActionNames.Contains(P.	ActionName)	? CAResults.Allow : CAResults.Block;
	public static CAResults BlockAction	(CAParams P, string ActionName			) => P.ActionName!=				ActionName	? CAResults.Allow : CAResults.Block;
	public static CAResults BlockActions(CAParams P, params string[] ActionNames) => !ActionNames.Contains(P.	ActionName)	? CAResults.Allow : CAResults.Block;
}

//Patches for blocking
[HarmonyLib.HarmonyPatch(typeof(OneAxisInputControl), nameof(OneAxisInputControl.UpdateWithValue))]
internal static class Patch_OneAxisInputControl_UpdateWithValue {
	private static bool Prefix(OneAxisInputControl __instance, float value) =>
		value==0 || __instance is not PlayerAction PA || BlockKeys.Check_Actions.AllowResult(ActionName:PA.Name, Value:value);
}

[HarmonyLib.HarmonyPatch(typeof(InputDevice))]
internal static class Patch_InputDevice_Blockers
{
	[HarmonyLib.HarmonyPrefix][HarmonyLib.HarmonyPatch(nameof(InputDevice.UpdateLeftStickWithValue))]
	private static bool UpdateLeftStickWithValue_Prefix	(Vector2 value) => BlockKeys.Check_LStickMove.AllowResult(StickValue:value);
	[HarmonyLib.HarmonyPrefix][HarmonyLib.HarmonyPatch(nameof(InputDevice.UpdateRightStickWithValue))]
	private static bool UpdateRightStickWithValue_Prefix(Vector2 value) => BlockKeys.Check_RStickMove.AllowResult(StickValue:value);
}
[HarmonyLib.HarmonyPatch(typeof(InputControlState), nameof(InputControlState.Set), [typeof(float), typeof(float)])]
internal static class Patch_InputControlState_Set {
	private static bool Prefix() => BlockKeys.Check_All.AllowResult();
}