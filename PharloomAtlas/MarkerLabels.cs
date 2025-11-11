using HarmonyLib;
using SilkDev;
using SilkDev.DevInput.Mouse;
using SilkDev.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static SilkDev.DevInput.Joystick;

namespace PharloomAtlas;

public class MarkerLabels : Window
{
	private static readonly Reflectors.RField<GameMap, GameObject[,]> FSpawnedMapMarkers=new(null, "spawnedMapMarkers");
	private static readonly MapControl MC=MapControl.Self;
	private static Config Conf => Config.C;
	private readonly Dictionary<string, string> Labels=[];
	private readonly Dictionary<string, string> DeletedLabels=[];
	private string LabelToRemove=Misc.Empty;
	private MarkerPos? LastMarkerOver
	{
		get;
		set {
			if(field==value)
				return;
			_=GetMarkerGOFromPos(field)?.GetComponent<SpriteRenderer>().color=Color.white;
			_=GetMarkerGOFromPos(field=value)?.GetComponent<SpriteRenderer>().color=Color.green;
		}
	}
	private MarkerPos? SelectedMarker
	{
		get;
		set {
			if(field==value)
				return;
			if(field!=null) {
				SaveConfig(true);
				ClearFocus(true); //If this was called by ClearFocus, this will cause a double clear, but that’s ok
			}
			field=value;
		}
	}
	private bool TextHasFocus => SelectedMarker!=null;
	public GUIStyle LabelStyle=new(GUI.skin.label)
		{  normal={textColor=Conf.Color_MarkerLabelText}, alignment=TextAnchor.MiddleCenter	};
	public GUIStyle TextFieldStyle=new(GUI.skin.textField)
		{ focused={textColor=Conf.Color_MarkerLabelText}, alignment=TextAnchor.MiddleLeft	};

	public string DefaultLabel="New label";
	internal MarkerLabels() : base("MarkerLabels", false, -250)
	{
		UnboundDraw=true;
		Reload();

		LabelStyle.normal.background=TextFieldStyle.focused.background=BGTex=Conf.Color_MarkerLabelBG.V.MakeTexture();
		Conf.Color_MarkerLabelBG.SettingChanged		+= (_, _) =>
			OnNextFrame(() => BGTex.ReColor(Conf.Color_MarkerLabelBG));
		Conf.Color_MarkerLabelText.SettingChanged	+= (_, _) =>
			LabelStyle.normal.textColor=TextFieldStyle.focused.textColor=Conf.Color_MarkerLabelText;
	}

	//Remove focus when window is hidden
	public override bool Visible {
		get => base.Visible;
		set => Misc.IFF(!(base.Visible=value), () => ClearFocus());
	}

	//Get the vector list of currently placed markers
	public static Vector2[] MarkerPositions =>
		[.. PlayerData.instance.placedMarkers.AsEnumerable().SelectMany(MarkerList => MarkerList.List)];

	//Get the Marker GameObject from its position
	public static GameObject? GetMarkerGOFromPos(MarkerPos? Pos)
	{
		if(Pos==null)
			return null;

		(int, int) GetIndex(WrappedVector2List[] OuterList)
		{
			foreach((int OuterIndex, WrappedVector2List VL) in OuterList.Entries())
				foreach((int InnerIndex, Vector2 LPos) in VL.List.Entries())
					if(new MarkerPos(LPos)==Pos)
						return (OuterIndex, InnerIndex);
			return (-1, -1);
		}
		(int Index1, int Index2)=GetIndex(PlayerData.instance.placedMarkers);
		if(Index1==-1)
			return null;
		FSpawnedMapMarkers.Obj=MC.GameMap;
		return FSpawnedMapMarkers.Get()[Index1, Index2];
	}

	//Converts back and forth between vector and custom string
	public class MarkerPos(Vector2 Vec)
	{
		public readonly Vector2 Vec=Vec;
		public MarkerPos(string Pos) : this(Vector2.zero)
		{
			string[] Parts=Pos.Split(',');
			try {
				Vec=new Vector2(float.Parse(Parts[0]), float.Parse(Parts[1]));
			} catch(Exception e) {
				Catcher.OutputException($"Parsing Vector2 failed: {Pos}", e);
			}
		}
		public override string ToString() => $"{Vec.x:F2},{Vec.y:F2}";
		public static implicit operator string(MarkerPos MP) => MP.ToString();
		public static bool operator ==(MarkerPos? MP1, MarkerPos? MP2) => (MP1?.ToString() ?? "")==(MP2?.ToString() ?? "");
		public static bool operator !=(MarkerPos? MP1, MarkerPos? MP2) => !(MP1==MP2);
		public override bool Equals(object? Obj2) => Obj2 is MarkerPos MP2 && this==MP2;
		public override int GetHashCode() => ToString()?.GetHashCode() ?? 0;
	}

	protected override void DoLayout(int ID, Event Ev)
	{
		//Determine which labels to show
		List<(MarkerPos MP, string LabelText)> DrawList=[];
		if(Conf.AlwaysShowMarkerLabels)
			DrawList.AddRange(Labels.Select(KVP => (new MarkerPos(KVP.Key), KVP.Value)));
		else {
			HashSet<string?> Candidates=[
				SelectedMarker?.ToString(),
				CollidingMarkerPos?.ToString(),
				LastMarkerOver?.ToString(),
			];
			foreach(string? MP in Candidates)
				if(Labels.TryGetValue(MP ?? string.Empty, out string LabelText))
					DrawList.Add((new MarkerPos(MP!), LabelText));
		}

		//Draw the labels
		string? SelectedString=SelectedMarker?.ToString();
		foreach((MarkerPos MP, string LabelText) in DrawList)
			ShowMarkerLabel(MP, LabelText, MP.ToString()==SelectedString);
	}

	private void ShowMarkerLabel(MarkerPos MP, string LabelText, bool IsSelected)
	{
		//If not within the screen bounds, nothing to do
		Rect LabelBox=GetMarkerLabelRect(MP, LabelText);
		if(!new Rect(Vector2.zero, Misc.ScreenSize).Overlaps(LabelBox)) {
			if(IsSelected)
				ClearFocus();
			return;
		}

		//If not selected and editing, just draw a label
		if(!IsSelected) {
			GUI.Label(LabelBox, LabelText, LabelStyle);
			return;
		}

		//Handle the selected marker
		WindowRect=new Rect(LabelBox.position, TextFieldStyle.CalcSize(new GUIContent(LabelText))).AddWidth(10);
		GUI.SetNextControlName("MarkerLabel");
		string NewLabel=GUI.TextField(WindowRect, LabelText, TextFieldStyle);
		if(GUI.GetNameOfFocusedControl()!="MarkerLabel") {
			GUI.FocusControl("MarkerLabel");
			TextEditor TE=(TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);
			OnNextFrame(
				  NewLabel==DefaultLabel ? TE.SelectAll //It is already selected by default, but just in case
				: () => TE.selectIndex=TE.cursorIndex=NewLabel.Length,
			false);
		}

		//Save updates
		if(NewLabel!=LabelText) {
			Labels[MP]=NewLabel;
			SaveConfig(false);
		}
	}

	//Get the coordinates for the label
	public Rect GetMarkerLabelRect(MarkerPos MP, string LabelText) => new(
		MC.MapPositionToScreenCoordsI(MP.Vec),
		LabelStyle.CalcSize(new GUIContent(LabelText))+new Vector2(10, 0)
	);

	//Get the position of the marker the marker cursor is over
	public static MarkerPos? CollidingMarkerPos { get
	{
		if(MC.MMM.collidingMarkers.Count==0)
			return null;
		GameObject Marker=MC.MMM.collidingMarkers[^1];
		InvMarker InvMarker=Marker.GetComponent<InvMarker>();
		return new MarkerPos(PlayerData.instance.placedMarkers[(int)InvMarker.Colour].List[InvMarker.Index]);
	} }

	//Handle shortcut key
	protected override void OnUpdate()
	{
		//If there is a label that needs removing, do it
		if(LabelToRemove.Length!=0) {
			_=Labels.Remove(LabelToRemove);
			LabelToRemove=Misc.Empty;
			SaveConfig(false);
		}

		//If the left stick is moved or clicked while in TextHasFocus mode then remove the focus
		static bool LStickClick() => ActiveDevice.LeftStickButton.WasPressed;
		if(TextHasFocus && (LStickClick() || ((Vector2)ActiveDevice.LeftStick).magnitude>.001f)) {
			ClearFocus();
			return;
		}

		//Only need to update anything when Shortcut_EditMarkerLabel|JoyStickLeftButton is pressed
		if(!LStickClick() && !Conf.Shortcut_EditMarkerLabel.IsUp())
			return;

		//If no colliding marker then nothing to do
		if((SelectedMarker=CollidingMarkerPos)==null)
			return;

		//If label already exists, all we need to do is focus it
		if(Labels.ContainsKey(SelectedMarker!))
			return;

		//Create a label and focus it on the next frame
		Labels[SelectedMarker!]=DefaultLabel;
		SaveConfig(false);
	}

	//Remove focus on enter key
	protected override void OnKeyPress(Event Ev) => Misc.IFF(
		Ev.type==EventType.KeyUp && (Ev.keyCode is KeyCode.Return or KeyCode.Escape),
		() => ClearFocus()
	);

	//Check for clicking of markers
	public const float MarkerRadius=.37f;
	protected override void OnMouseEvent(Event Ev)
	{
		//Nothing to do on mouse check
		if(Ev.type==EventType.TouchStationary)
			return;

		//Determine the marker that we are the “most over”
		MarkerPos? Closest=MC.FindClosestVector(
			MarkerPositions.Select(V => new MapControl.VItem<MarkerPos>(V, new MarkerPos(V))),
			MC.MouseCursorWorldPosition,
			MarkerRadius, 0
		);

		//Recolor if mouse over has changed
		if(Ev.type==EventType.MouseMove)
			LastMarkerOver=Closest;

		//Only left click supported for label selecting
		if(Ev.type!=EventType.MouseUp || Button.CurrentButton!=Button.Enum.Left)
			return;

		//If there is already a marker that is selected and we clicked on its label then keep it selected
		if(TextHasFocus && GetMarkerLabelRect(SelectedMarker!, Labels.Get(SelectedMarker!) ?? "").Contains(Ev.mousePosition))
			return;

		//See if we’ve clicked on a label
		if(Conf.AlwaysShowMarkerLabels)
			foreach((string Pos, string Label) in Labels) {
				if(!GetMarkerLabelRect(new MarkerPos(Pos), Label).Contains(Ev.mousePosition))
					continue;
				SelectedMarker=new MarkerPos(Pos);
				return;
			}

		//Select the marker
		if((SelectedMarker=Closest)==null)
			return;

		//If label already exists, all we need to do is focus it
		if(Labels.ContainsKey(SelectedMarker!))
			return;

		//Create a label and focus it on the next frame
		Labels[SelectedMarker!]=DefaultLabel;
		SaveConfig(false);
	}

	//Save to the config
	private void SaveConfig(bool CheckForEmpty)
	{
		//If the label is empty, remove it
		if(
			   CheckForEmpty
			&& TextHasFocus
			&& Labels.Get(SelectedMarker!)?.Trim().Length==0
		) {
			LabelToRemove=SelectedMarker!;
			return;
		}

		//Save to the config
		string NewConfigValue=FileOps.SerializeToJSON(Labels, true);
		if(NewConfigValue!=Conf.MarkerLabels)
			Conf.MarkerLabels.V=NewConfigValue;
	}

	//Make sure focus is cleared
	private void ClearFocus(bool DoNotSetSelectedMarker=false)
	{
		if(!TextHasFocus)
			return;
		WindowRect=Rect.zero;
		if(!DoNotSetSelectedMarker) //Prevent infinite recursion on SelectedMarker setter
			SelectedMarker=null;
		GUI.FocusControl("NONE");
	}

	//Load the labels from the config
	public void Reload()
	{
		Labels.Clear();
		try {
			//Create list of current markers. For any that do not exist, move the label to the deleted list
			List<string> CurMarkers=[.. MarkerPositions.Select(V => new MarkerPos(V).ToString())];
			foreach((string Key, string Val) in FileOps.DeserializeJson<Dictionary<string, string>>(Conf.MarkerLabels))
				if(CurMarkers.Contains(Key))
					Labels[Key]=Val;
				else
					DeletedLabels[Key]=Val;
		} catch(Exception e) {
			_=new PopupMessage(
				"<color=red>Your marker labels failed to load.</color> There are backups at <color=green>"
				+$"{Conf.PSC.ConfigFileName}{SilkDev.Configs.PerSaveConfig.BackupExtension}*</color>: {e.Message}"
			);
		}
	}

	//Need this for MapControl when moving from a marker to an icon
	internal void ClearLastMarkerOver() => LastMarkerOver=null;

	//Switch label between live/deleted lists
	private void SwapMarkerLists(Dictionary<string, string> From, Dictionary<string, string> To)
	{
		SelectedMarker=null;
		MarkerPos? NewMarker=CollidingMarkerPos;
		if(NewMarker==null || !From.TryGetValue(NewMarker!, out string LabelText))
			return;
		To[NewMarker!]=LabelText;
		_=From.Remove(NewMarker!);
	}

	//Window stuff
	protected override bool IsMouseOverWindow(Vector2 _) => true; //Watches the entire screen

	//Handle adding and deleting markers
	private void MarkerPlaced() => //Runs the frame after a new marker is placed
		SwapMarkerLists(DeletedLabels, Labels); //Attempt to recover deleted messages
	private void MarkerDeleting() => //Runs before a marker is deleted
		SwapMarkerLists(Labels, DeletedLabels); //Move the label to the deleted list
	[HarmonyPatch(typeof(MapMarkerMenu))]
	private static class Patch_MapMarkerMenu
	{
		[HarmonyPostfix][HarmonyPatch("PlaceMarker" )] private static void PlaceMarker_Postfix() => OnNextFrame(MC.MarkerLabels.MarkerPlaced   );
		[HarmonyPrefix] [HarmonyPatch("RemoveMarker")] private static void RemoveMarker_Prefix() =>				MC.MarkerLabels.MarkerDeleting();
	}
}