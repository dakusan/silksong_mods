using SilkDev;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PharloomAtlas;

public class MonitorSaveValues
{
	private static MonitorSaveValues _Self=null!; public static MonitorSaveValues Self => _Self; //Singleton

	//Content data
	public readonly struct FromNamePair(string From, string Name)
	{
		public readonly string From=From, Name=Name; //From=PlayerData|SceneName, Name=PlayerData.Member|SceneName.ID
		public override readonly string ToString() => $"{From}.{Name}";
	}
	public class SaveItem(string From, string Name, object OldValue, object NewValue)
	{
		public readonly FromNamePair FromName=new(From, Name);
		public readonly object OldValue=OldValue, NewValue=NewValue;
		public readonly DateTime TheTime=DateTime.Now;
		public override string ToString() => $"{TheTime:HH:mm:ss}: {From}.{Name}: {OldValue} -> {NewValue}";
		public string FullName => FromName.ToString();
		public string From => FromName.From;
		public string Name => FromName.Name;
	}

	//Data loaded (and written) to JSON and web
	private class ItemFinder
	{
		public readonly List<string> IgnorePlayerNamedValues=[];
		public readonly Dictionary<int, string> MatchedIcons=[];
		[NonSerialized] public readonly Dictionary<string, int> MatchedIconsReverse=[];
	}
	private static readonly string JsonFilename=FileOps.PathCombine(Plugin.GetMyPath, "ItemFinder.json");
	private static readonly string LogFile=FileOps.PathCombine(Plugin.GetMyPath, "ItemFinder.log");
	private readonly ItemFinder IF=null!;
	private const string WebAddress="https://www.castledragmire.com/silksong/Submit.php";

	//Members
	private string CurrentSceneName=null!;
	private DateTime LastCheck_PersistentObject=DateTime.MinValue, LastCheck_PlayerData=DateTime.MinValue;
	private EnumSaveData.PerObject[]? CurSceneObjects=null;
	private readonly EnumSaveData.PlayerDataValue[] PlayerDataValues=EnumSaveData.PlayerDataValues;

	//Callbacks
	public event Action<SaveItem> OnValueChanged=delegate {}; //Listen for any value change

	//Get the dictionaries
	public ReadOnlyDictionary<int, string> GetMatchedIcons			=> new(IF.MatchedIcons);
	public ReadOnlyDictionary<string, int> GetMatchedIconsReverse	=> new(IF.MatchedIconsReverse);

	//Things to ignore
	private bool IsFirstSceneValueCheck=false; //We don’t report anything on the first scene value load
	private static readonly string?[] IgnoreScenes=[null, "Pre_Menu_Loader", "Pre_Menu_Intro", "Menu_Title", "Quit_To_Menu"];
	private bool IsIgnoredScene => IgnoreScenes.Contains(CurrentSceneName);
	private const float LastProfileIDSetGracePeriod=0.2f; //We don’t report anything within the grace period of when "profileID" is set
	private const string ProfileIDStr="profileID";
	private DateTime LastProfileIDSet=DateTime.MinValue;

	//Initialize the monitoring
	internal MonitorSaveValues()
	{
		Misc.InitSingleton(this, ref _Self);
		try {
			IF=FileOps.DeserializeJson<ItemFinder>(FileOps.ReadFile(JsonFilename));
			foreach((int MI_ID, string MI_Val) in IF.MatchedIcons)
				IF.MatchedIconsReverse[MI_Val]=MI_ID;
		} catch(Exception e) {
			string Message=$"Loading item finder json failed! Everything will be marked as not found and you’ll be getting excessive save value finds.{Misc.NewLine}{e.Message}";
			Log.Error(Message);
			_=new PopupMessage(Message);
			IF=new ItemFinder();
		}

		SceneManager.sceneLoaded += OnSceneLoaded;
		_=Catcher.ExecCoroutine("WatchForUpdates", WatchForUpdates());
		//TODO: Do I need to watch the follow PlayerData?: HashSet<string> scenesEncounteredBench, scenesEncounteredCocoon;
	}

	//On scene load, clear the previous scene and enumerate the current scene in a new thread
	private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
	{
		CurSceneObjects=null;
		CurrentSceneName=scene.name;
		LastCheck_PersistentObject=LastCheck_PlayerData=DateTime.MinValue; //NOTE: Changes could be missed here if the Config.C.QueryTime_* are too high
		IsFirstSceneValueCheck=true;
		if(!IsIgnoredScene)
			_=Catcher.ExecCoroutine("GetSceneData", GetSceneData(scene));
	}

	//Enumerate the current scene in a new thread
	private IEnumerator GetSceneData(Scene scene)
	{
		while(!scene.isLoaded)
			yield return new WaitForSecondsRealtime(0.1f);
		yield return new WaitForSecondsRealtime(1f);

		CurSceneObjects=[.. EnumSaveData.FindPersistentObjectsInScene(scene)];
		Log.Info($"Found {CurSceneObjects.Length} objects in scene {scene.name}");
	}

	//Thread to watch for updates
	private IEnumerator WatchForUpdates()
	{
		while(true) {
			float ConfigPO=Config.C.QueryTime_PersistentObj, ConfigPD=Config.C.QueryTime_PlayerData;
			if((DateTime.Now-LastCheck_PersistentObject).TotalSeconds>=ConfigPO)
				RunUpdate_PersistentObject();
			if((DateTime.Now-LastCheck_PlayerData).TotalSeconds>=ConfigPD)
				RunUpdate_PlayerData();

			yield return new WaitForSecondsRealtime(Mathf.Min(
				ConfigPO-(float)(DateTime.Now-LastCheck_PersistentObject).TotalSeconds,
				ConfigPD-(float)(DateTime.Now-LastCheck_PlayerData).TotalSeconds
			));
		}
	}

	//Look for updates in persistent objects
	private void RunUpdate_PersistentObject()
	{
		LastCheck_PersistentObject=DateTime.Now;

		//Make sure scene is loaded
		if(CurSceneObjects==null || IsIgnoredScene)
			return;

		foreach(EnumSaveData.PerObject PO in CurSceneObjects) {
			object LastValue=PO._LastValue;

			if(PO.Type==EnumSaveData.PerObject.POType.PermInt)
				((EnumSaveData.PerObjectI)PO).GetField.Obj.SaveStateNoCondition();
			else if(PO.Type==EnumSaveData.PerObject.POType.PermBool)
				((EnumSaveData.PerObjectB)PO).GetField.Obj.SaveStateNoCondition();

			object NewValue=PO.CurrentObjValue;
			if(AreObjectsTheSame(NewValue, LastValue))
				continue;
			if(!IsFirstSceneValueCheck)
				ValueChanged(new SaveItem(CurrentSceneName, PO.ObjName, LastValue, NewValue));
			PO._LastValue=NewValue;
		}
		IsFirstSceneValueCheck=false;
	}

	//Look for updates in player data
	private void RunUpdate_PlayerData()
	{
		LastCheck_PlayerData=DateTime.Now;

		//Make sure player is loaded
		PlayerData PD=PlayerData.instance;
		if(PD==null)
			return;

		foreach(var PDV in PlayerDataValues) {
			object LastValue=PDV.LastValue, NewValue=PDV.CurrentValue(PD);
			if(AreObjectsTheSame(LastValue, NewValue))
				continue;
			PDV.LastValue=NewValue;
			if(PDV.Name==ProfileIDStr)
				LastProfileIDSet=DateTime.Now;
			if( //Do not report if...
				!IsIgnoredScene && //On ignored scenes
				(DateTime.Now-LastProfileIDSet).TotalSeconds>LastProfileIDSetGracePeriod && //ProfileID has been set within LastProfileIDSetGracePeriod
				!IF.IgnorePlayerNamedValues.Contains(PDV.Name) //Is in the ignored value name list
			)
				ValueChanged(new SaveItem("PlayerData", PDV.Name, LastValue, NewValue));
		}
	}

	//Called if monitored value has changed for player or scene
	private void ValueChanged(SaveItem SI)
	{
		//Send to the delegates
		OnValueChanged.Invoke(SI);

		//If this is mapped to an icon then set its value
		string FullName=SI.FullName;
		if(!IF.MatchedIconsReverse.TryGetValue(FullName, out int ItemID))
			return;
		Item? I=MapControl.Self?.DS.Items.Get(ItemID);
		_=I?.IsFound=IsValueCompleted(SI.NewValue);
		Log.Info(
			$"Item found: {FullName} :: "+(
				  MapControl.Self==null ? "MAP NOT YET LOADED"
				: I?.Title ?? "INVALID ICON"
			)+$" = {SI.OldValue} -> {SI.NewValue} ({(IsValueCompleted(SI.NewValue) ? "COMPLETE" : "NOT COMPLETE")})"
		);
	}

	//When a game is loaded update all icon IsFound states from the player and scene data
	internal void UpdateAllUsedValuesOnLoad()
	{
		if(MapControl.Self==null)
			return;

		PlayerData PD=PlayerData.instance;
		SceneData SD=SceneData.instance;
		DataStorage DS=MapControl.Self.DS;
		foreach((int LocalID, string GameName) in IF.MatchedIcons) {
			string[] Parts=GameName.Split('.');
			if(Parts.Length!=2)
				Log.Error($"Invalid name found: {GameName}");
			else
				_=DS.Items.Get(LocalID)?.IsFound=IsValueCompleted(Parts[0]=="PlayerData"
					? GetLiveCompletedValue_PlayerData(Parts[1], PD)
					: GetLiveCompletedValue_SceneData(Parts[0], Parts[1], SD)
				);
		}
	}

	//Simple value comparitor
	private static bool AreObjectsTheSame(object? a, object? b) =>
		a==null || b==null ? a==b
		: a is bool v ? v==(bool)b
		: a is int v1 ? v1==(int)b
		: a is string v2 && v2==(string)b;
		//: false;

	private static bool IsValueCompleted(object? a) =>
		//a==null ? false
		a is bool v ? v
		: a is int v1 ? v1==0
		: a is string v2 && v2!=Misc.Empty;
		//: false;

	//------------------Save and possibly send the currently selected icon’s new value------------------
	private class MSException(string Message) : Exception(Message) {}
	//Save and possibly send the currently selected icon’s new value (wrapper to output messages)
	public void SaveIconValue(bool IsSendingToo) =>
		Task.Run(() => SaveIconValueThread(IsSendingToo));

	//Thread for processing new saved value
	private async Task SaveIconValueThread(bool IsSendingToo)
	{
		//Process the new value and get the log and popup window messages and update JSON file
		string LogMessage, PopupMessage;
		try {
			PopupMessage=LogMessage=SaveIconValueReal();
			try {
				FileOps.WriteFile(JsonFilename, FileOps.SerializeToJSONSorted(IF));
			} catch {
				PopupMessage+="\n\n<color=red>Could not save to JSON file!</color>";
				LogMessage+="\nCould not save to JSON file!";
			}
		} catch(MSException e) {
			PopupMessage=LogMessage=e.Message;
		} catch(Exception e) {
			PopupMessage=LogMessage="An unknown error occurred: "+Misc.SanitizeRichString(e.Message);
		}

		//Send web request
		(string AddMessage, string AddPopupMessage)=await SendWebRequestAndFormatReturns(IsSendingToo);
		LogMessage+=AddMessage;
		PopupMessage+=AddPopupMessage;

		//Write message to the log
		try {
			if(!FileOps.FileExists(LogFile))
				FileOps.WriteFile(LogFile, "Initializing log file");
			FileOps.AppendFile(LogFile,
				DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss: ")+ //Adds the timestamp to the beginning of the log line
				Regex.Replace(LogMessage.Replace("\n\n", "\n"), @"</?(size|color|b)\b.*?>", Misc.Empty) //Remove double newlines and richText tags
			);
		} catch(Exception e) {
			PopupMessage+=$"\n\n<color=red><size=15>Could not write to log file.</color>: {e.Message}</size>";
		}

		//Open the popup
		_=new PopupMessage(PopupMessage);
	}

	//Update live states from new selected icon value and return result messages for logs/user message
	private string SaveIconValueReal()
	{
		Item SelectedItem=MapControl.Self.SelectedItem ?? throw new MSException ("You must select a map item to match it to your selected value");
		if(!(SaveValuesWindow.Self?.Visible ?? false))
			throw new MSException("The value window must be open for you to choose the item to match to the icon.");
		SaveItem SelectedValue=SaveValuesWindow.Self.SelectedItem ?? throw new MSException("There are no save values in your window");

		//See if the icon or item was already matched
		string AddStringStatement=Misc.Empty;
		DataStorage DS=MapControl.Self.DS;
		if(IF.MatchedIcons.TryGetValue(SelectedItem.ID, out string PreviousMatch))
			AddStringStatement+=$"\n\n<size=-15>Warning: Icon previously matched to “<b>{PreviousMatch}</b>”</size>";
		if(IF.MatchedIconsReverse.TryGetValue(SelectedValue.FullName, out int PreviousMatch2))
			AddStringStatement+=$"\n\n<size=-15>Warning: SaveValue was previously matched to “#{PreviousMatch2} <b>{DS.Items.Get(PreviousMatch2)?.Title ?? "INVALID ITEM"}</b>”</size>";

		//Save the values and run icon updates
		if(PreviousMatch2!=0) {
			_=DS.Items.Get(PreviousMatch2)?.IsFound=false;
			_=DS.Items.Get(PreviousMatch2)?.IsLinked=false;
			_=IF.MatchedIcons.Remove(PreviousMatch2);
		}
		SelectedItem.IsFound=IsValueCompleted(GetLiveCompletedValue(SelectedValue.FromName));
		SelectedItem.IsLinked=true;

		//Update the dictionaries
		IF.MatchedIcons[SelectedItem.ID]=SelectedValue.FullName;
		IF.MatchedIconsReverse[SelectedValue.FullName]=SelectedItem.ID;

		//Return the message
		return $"{SelectedItem.Title} set to {SelectedValue.FullName}{AddStringStatement}";
	}

	//Get if a named value is completed
	public static bool GetLiveCompletedValue(FromNamePair FN) =>
		  FN.From=="PlayerData"
		? GetLiveCompletedValue_PlayerData(FN.Name, PlayerData.instance)
		: GetLiveCompletedValue_SceneData(FN.From, FN.Name, SceneData.instance);
	private static bool GetLiveCompletedValue_PlayerData(string Name, PlayerData PD) =>
		IsValueCompleted(typeof(PlayerData).GetField(Name)?.GetValue(PD) ?? null);
	private static bool GetLiveCompletedValue_SceneData(string From, string Name, SceneData SD) =>
		SD.PersistentBools.TryGetValue(From, Name, out PersistentItemData<bool> BoolVal) ? BoolVal.Value :
		SD.PersistentInts.TryGetValue(From, Name, out PersistentItemData<int> IntVal) && IntVal.Value==0;
	//: false;

	//Prepare (then send) the web request and format popup/log returns
	private async Task<(string, string)> SendWebRequestAndFormatReturns(bool IsSendingToo)
	{
		//Build the URL query string
		static string UrlQuery() => string.Join('&', new List<(string Key, string Value)> {
			("ItemID", MapControl.Self.SelectedItem!.ID.ToString()),
			("ItemName", SaveValuesWindow.Self.SelectedItem!.FullName),
			("Username", Misc.SteamUsername),
		}.Select(static Tuple => HttpUtility.UrlEncode(Tuple.Key)+"="+HttpUtility.UrlEncode(Tuple.Value)));

		//Send the web request and format returns
		string WebSubmit;
		if(!IsSendingToo)
			return (Misc.Empty, "\n<color=green><size=15>Please consider contributing by sending your data</size></color>");
		else if("SUCCESS"==(WebSubmit=await SendWebRequestReal($"{WebAddress}?{UrlQuery()}")))
			return (Misc.Empty, "\n<color=green><size=15>Thank you for your submission!</size></color>");
		else
			return ($"{Misc.NewLine}Web submission failed: {WebSubmit}", $"{Misc.NewLine}<color=red>Web submission failed: <b>{Misc.SanitizeRichString(WebSubmit)}</b></color>");
	}

	//Send the web request
	private async Task<string> SendWebRequestReal(string Url)
	{
		try {
			using HttpClient Client=new();
			HttpResponseMessage Response=await Client.GetAsync(Url);
			_=Response.EnsureSuccessStatusCode();
			return await Response.Content.ReadAsStringAsync();
		} catch(Exception e) {
			return e.Message;
		}
	}
}