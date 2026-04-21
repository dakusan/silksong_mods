using SilkDev;
using SilkDev.JSON;
using SilkDev.Windows;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using UnityEngine;
using UnityEngine.SceneManagement;
using static SilkDev.DevStrings;

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
	public readonly struct ItemFinderItem(int ID, bool ForStarting) : IEquatable<ItemFinderItem>
	{
		public readonly int		ID									=  ID;
		public readonly bool	ForStarting							=  ForStarting;
		public			string	ToKey								=> ID.ToString()+(ForStarting ? '~' : null);
		public override string	ToString	()						=> ID.ToString()+(ForStarting ? $" [{Tr.TDef("FLAG_STARTED", "ItemFields", "STARTED", true)}]" : null);
		public override int		GetHashCode	()						=> !ForStarting ? ID : ID*-1;
		public override bool	Equals		(object Obj)			=> Obj is ItemFinderItem Other && Equals(Other);
		public			bool	Equals		(ItemFinderItem Other)	=> ID==Other.ID && ForStarting==Other.ForStarting;
		public static	bool	operator ==	(ItemFinderItem Left, ItemFinderItem Right) => Left.Equals(Right);
		public static	bool	operator !=	(ItemFinderItem Left, ItemFinderItem Right) => !(Left==Right);
	}
	private class ItemFinder
	{
		public readonly List<string> IgnorePlayerNamedValues=[];
		public readonly Dictionary<ItemFinderItem, string> MatchedIcons=[];
		[NonSerialized] public readonly Dictionary<string, ItemFinderItem> MatchedIconsReverse=[];
	}
	private static readonly string JsonFileName="ItemFinder.json", JsonFilePath=FileOps.PathCombine(FileOps.GetPluginPath, JsonFileName);
	private static readonly string LogFilePath=FileOps.PathCombine(FileOps.GetPluginPath, "ItemFinder.log");
	private readonly ItemFinder IF=null!;
	private const string WebAddress="https://silksong.castledragmire.com/Submit.php";
	private const string PlayerDataStr=nameof(PlayerData);

	//Members
	private string CurrentSceneName=null!;
	private DateTime LastCheck_PersistentObject=DateTime.MinValue, LastCheck_PlayerData=DateTime.MinValue;
	private EnumSaveData.PerObject[]? CurSceneObjects=null;
	private readonly EnumSaveData.PlayerDataValue[] PlayerDataValues=EnumSaveData.PlayerDataValues;

	//Callbacks
	public event Action<SaveItem> OnValueChanged=delegate {}; //Listen for any value change
	public readonly SilkDev.Events.EventRegister<FromNamePair, Action<SaveItem>> RegisterValueChanged=new(nameof(RegisterValueChanged)); //Listen for a single value change

	//Get the dictionaries
	public ReadOnlyDictionary<ItemFinderItem, string> GetMatchedIcons			=> new(IF.MatchedIcons);
	public ReadOnlyDictionary<string, ItemFinderItem> GetMatchedIconsReverse	=> new(IF.MatchedIconsReverse);

	//Things to ignore
	private bool IsFirstSceneValueCheck=false; //We don’t report anything on the first scene value load
	private static readonly string?[] IgnoreScenes=[null, "Pre_Menu_Loader", "Pre_Menu_Intro", "Menu_Title", "Quit_To_Menu"];
	private bool IsIgnoredScene => IgnoreScenes.Contains(CurrentSceneName);
	private const float LastProfileIDSetGracePeriod=0.2f; //We don’t report anything within the grace period of when "profileID" is set
	internal const string ProfileIDStr="profileID";
	private DateTime LastProfileIDSet=DateTime.MinValue;

	//Translations
	private static readonly Translations Tr=Config.C.Tr;
	private static string TSan(string Str, params object[] FormatList)	=> Tr.T(Str, nameof(MonitorSaveValues), true , FormatList);
	private static string TrT (string Str, params object[] FormatList)	=> Tr.T(Str, nameof(MonitorSaveValues), false, FormatList);
	private static string San (string Str)								=> SafeRich(Str);

	//Initialize the monitoring
	internal MonitorSaveValues()
	{
		Misc.InitSingleton(this, ref _Self);
		try {
			IF=JsonUtils.Deserialize<JsonConverter_ItemFinder>(FileOps.ReadFile(JsonFilePath)).ToItemFinder();
			foreach((ItemFinderItem MI_ID, string MI_Val) in IF.MatchedIcons)
				IF.MatchedIconsReverse[MI_Val]=MI_ID;
		} catch(Exception e) {
			string DefaultMessage=$"Loading item finder json failed! Everything will be marked as not found and you’ll be getting excessive save value finds.{NewLine}{{0}}";
			Log.Error(string.Format(DefaultMessage, Catcher.GetOutputException("Item finder loading", e)));
			_=new PopupMessage(Tr.TDef("ItemFinderLoadFailed", nameof(MonitorSaveValues), DefaultMessage, true, Catcher.GetRelevantException(e).Message));
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
		Log.Debug($"Found {CurSceneObjects.Length} objects in scene {scene.name}");
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

		foreach(EnumSaveData.PlayerDataValue PDV in PlayerDataValues) {
			//Check if value has changed
			object LastValue=PDV.LastValue, NewValue=PDV.CurrentValue(PD);
			if(AreObjectsTheSame(LastValue, NewValue))
				continue;

			//On profile load null out all old values and do not report on them for a bit
			if(PDV.Name==ProfileIDStr) {
				LastProfileIDSet=DateTime.Now;
				foreach(EnumSaveData.PlayerDataValue PDV2 in PlayerDataValues)
					PDV2.LastValue=null!;
			}

			//Store updated values
			PDV.LastValue=NewValue;

			//If value is a HashSet then get the differences and add to our current HashSet
			bool IsHashSet=PDV.Type==EnumSaveData.PlayerDataValue.PDType.PDHashSet;
			string[] HashSetDiff=null!;
			if(IsHashSet) {
				HashSet<string> CurHS=LastValue as HashSet<string> ?? [];
				PDV.LastValue=CurHS;
				HashSetDiff=[.. (NewValue as HashSet<string>).Except(CurHS)];
				CurHS.UnionWith(HashSetDiff);
			}

			//Do not report if...
			if(!(
				!IsIgnoredScene && //On ignored scenes
				(DateTime.Now-LastProfileIDSet).TotalSeconds>LastProfileIDSetGracePeriod && //ProfileID has been set within LastProfileIDSetGracePeriod
				!IF.IgnorePlayerNamedValues.Contains(PDV.Name) //Is in the ignored value name list
			))
				continue;

			//Pass through the values
			if(!IsHashSet)
				ValueChanged(new SaveItem(PlayerDataStr, PDV.Name, LastValue, NewValue));
			else
				foreach(string Str in HashSetDiff)
					ValueChanged(new SaveItem(PlayerDataStr, $"{PDV.Name}__{Str}", false, true));
		}
	}

	//Called if monitored value has changed for player or scene
	private void ValueChanged(SaveItem SI)
	{
		//Send to the delegates
		OnValueChanged.Invoke(SI);
		_=RegisterValueChanged.Run(SI.FromName, CW => CW(SI));

		//If this is mapped to an icon then set its value
		string FullName=SI.FullName;
		if(!IF.MatchedIconsReverse.TryGetValue(FullName, out ItemFinderItem ItemID))
			return;
		Item? I=MapControl.Self?.DS.Items.Get(ItemID.ID);
		I?.SetStatusFlag(ItemID.ForStarting, IsValueCompleted(SI.NewValue));
		Log.Info(
			$"Item found: [{ItemID}] {FullName} :: "+(
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

		string[] Parts;
		foreach(Item I in MapControl.Self.DS.Items.Values)
			foreach(bool ForStarting in (bool[])[false, true])
				I.SetStatusFlag(
					ForStarting,
					   IF.MatchedIcons.TryGetValue(new(I.ID, ForStarting), out string GameName)												//Match was found
					&& ((Parts=GameName.Split('.')).Length==2 || Misc.PassThru(() => Log.Error($"Invalid name found: {GameName}"), false))	//Match value is in proper format (dot separated)
					&& GetLiveCompletedValue(new(Parts[0], Parts[1]))																		//Is value completed
				);
	}

	//Simple value comparitor
	private static bool AreObjectsTheSame(object? a, object? b) =>
		  a==null || b==null	? a ==			b
		: a is bool		v		? v ==(bool)	b
		: a is int		v1		? v1==(int)		b
		: a is string	v2		? v2==(string)	b
		: a is HashSet<string> v3 && v3.Count==(b as HashSet<string>)?.Count; //Since the hash sets are only ever added to, we can just check the count
		//: false;

	private static bool IsValueCompleted(object? a) =>
		//a==null ? false
		  a is bool v ? v
		: a is int v1 ? v1==0
		: a is string v2 && v2!=string.Empty;
		//: false;

	//JSON converter for ItemFinder
	private class JsonConverter_ItemFinder()
	{
		public readonly List<string> IgnorePlayerNamedValues=[];
		public readonly Dictionary<string, string> MatchedIcons=[];
		public JsonConverter_ItemFinder(ItemFinder IF) : this()
		{
			IgnorePlayerNamedValues.AddRange(IF.IgnorePlayerNamedValues);
			foreach((ItemFinderItem ID, string Value) in IF.MatchedIcons)
				MatchedIcons[ID.ToKey]=Value;
		}
		public ItemFinder ToItemFinder()
		{
			ItemFinder IF=new();
			IF.IgnorePlayerNamedValues.AddRange(IgnorePlayerNamedValues);
			bool IsForStarging;
			foreach((string Key, string Value) in MatchedIcons)
				if(Key.Length>0 && int.TryParse((IsForStarging=(Key[^1]=='~')) ? Key[..^1] : Key, out int KeyID))
					IF.MatchedIcons[new ItemFinderItem(KeyID, IsForStarging)]=Value;
				else
					Log.Error($"Invalid key found in {JsonFileName}: {Key}");
			Clear();
			return IF;
		}
		public void Clear() { IgnorePlayerNamedValues.Clear(); MatchedIcons.Clear(); }
	}

	//------------------Save and possibly send the currently selected icon’s new value------------------
	private class MSException(string Message) : Exception(Message) {}
	//Save and possibly send the currently selected icon’s new value (wrapper to output messages)
	public void SaveIconValue(bool IsSendingToo) =>
		Task.Run(() => SaveIconValueThread(IsSendingToo));

	//Thread for processing new saved value
	private class SaveValuePopup() : PopupMessage(Tr.T("Initializing", "ErrorLog", true))
	{
		public bool IsProcessing=true;
		protected override string PressAnyKeyString => IsProcessing ? $"<color=red><size=20>{TSan("Waiting for process to finish")}</size></color>" : base.PressAnyKeyString;
		protected override bool BlockClose => IsProcessing;
		public string PopupMessage
		{
			get; set => Message=(field=value)+(SendingToServer ? $"{NewLine}<color=red>{TSan("Sending to server now")}</color>" : null);
		} = string.Empty;
		public bool SendingToServer {
			get; set { field=value; PopupMessage+=null; }
		} = false;
	}
	private async Task SaveIconValueThread(bool IsSendingToo)
	{
		//Process the new value and get the log and popup window messages and update JSON file
		SaveValuePopup SVP=new();
		string LogMessage;
		bool NoValue=false;
		try {
			SVP.PopupMessage=LogMessage=SaveIconValueReal();
			try {
				FileOps.WriteFile(JsonFilePath, JsonUtils.Serialize(new JsonConverter_ItemFinder(IF), Sorted:true, TrailingCommas:true));
			} catch {
				const string ErrMsg="Could not save to JSON file!";
				SVP.PopupMessage+=$"{NewLine}{NewLine}<color=red>{TSan(ErrMsg)}</color>";
				LogMessage+=NewLine+ErrMsg;
			}
		} catch(MSException e) {
			SVP.PopupMessage=TSan(e.Message);
			LogMessage=e.Message;
			NoValue=true;
			if(IsSendingToo)
				SVP.PopupMessage+=$"{NewLine}<color=red>{TSan("Send to server canceled")}</color>";
		} catch(Exception e) {
			const string ErrMsg="An unknown error occurred: {0}";
			SVP.PopupMessage=TSan(ErrMsg, e.Message);
			LogMessage=string.Format(ErrMsg, e.Message);
		}

		//Send web request
		if(!NoValue) {
			SVP.SendingToServer=true;
			(string AddMessage, string AddPopupMessage)=await SendWebRequestAndFormatReturns(IsSendingToo);
			SVP.SendingToServer=false;
			LogMessage+=AddMessage;
			SVP.PopupMessage+=AddPopupMessage;
		}

		//Write message to the log
		try {
			LogMessage=Regex.Replace(LogMessage.Replace("\n\n", "\n"), @"</?(size|color|b)\b.*?>", string.Empty); //Remove double newlines and richText tags
			Log.Info(LogMessage);
			if(!FileOps.FileExists(LogFilePath))
				FileOps.WriteFile(LogFilePath, "Initializing log file");
			FileOps.AppendFile(LogFilePath,
				DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss: ")+ //Adds the timestamp to the beginning of the log line
				LogMessage
			);
		} catch(Exception e) {
			SVP.PopupMessage+=$"{NewLine}{NewLine}<color=red><size=15>{TSan("Could not write to log file.")}</color>: {San(e.Message)}</size>";
		}
		SVP.IsProcessing=false;
	}

	//Update live states from new selected icon value and return result messages for logs/user message
	private string SaveIconValueReal()
	{
		Item SelectedItem=MapControl.Self.SelectedItem ?? throw new MSException("You must select a map item to match it to your selected value");
		if(!(SaveValuesWindow.Self?.Visible ?? false))
			throw new MSException("The value window must be open for you to choose the item to match to the icon.");
		SaveItem SelectedValue=SaveValuesWindow.Self.SelectedItem ?? throw new MSException("There are no save values in your window");

		//See if the icon or item was already matched
		string AddStringStatement=null!;
		DataStorage DS=MapControl.Self.DS;
		if(IF.MatchedIcons.TryGetValue(new(SelectedItem.ID, false), out string PreviousMatch))
			AddStringStatement+=$"{NewLine}{NewLine}<size=-15>{TrT("Warning: Icon previously matched to “<b>{0}</b>”", San(PreviousMatch))}</size>";
		if(IF.MatchedIconsReverse.TryGetValue(SelectedValue.FullName, out ItemFinderItem PreviousMatch2))
			AddStringStatement+=$"{NewLine}{NewLine}<size=-15>{TrT("Warning: SaveValue was previously matched to #{0} “<b>{1}</b>”", PreviousMatch2, San(DS.Items.Get(PreviousMatch2.ID)?.Title ?? TrT("INVALID ITEM")))}</size>";

		//Save the values and run icon updates
		if(PreviousMatch2.ID!=0) {
			DS.Items.Get(PreviousMatch2.ID)?.SetStatusFlag(PreviousMatch2.ForStarting, false);
			if(!PreviousMatch2.ForStarting)
				_=DS.Items.Get(PreviousMatch2.ID)?.IsLinked=false;
			_=IF.MatchedIcons.Remove(PreviousMatch2);
		}
		SelectedItem.IsFound=GetLiveCompletedValue(SelectedValue.FromName);
		SelectedItem.IsLinked=true;

		//Update the dictionaries
		IF.MatchedIcons[new(SelectedItem.ID, false)]=SelectedValue.FullName;
		IF.MatchedIconsReverse[SelectedValue.FullName]=new(SelectedItem.ID, false);

		//Return the message
		return TrT("{0} set to {1}{2}", San(SelectedItem.Title), San(SelectedValue.FullName), AddStringStatement);
	}

	//Get if a named value is completed
	public static bool GetLiveCompletedValue(FromNamePair FN) =>
		  FN.From==PlayerDataStr
		? GetLiveCompletedValue_PlayerData(FN.Name, PlayerData.instance)
		: GetLiveCompletedValue_SceneData(FN.From, FN.Name, SceneData.instance);
	private static bool GetLiveCompletedValue_PlayerData(string Name, PlayerData PD) =>
		(Name.Split("__", 2) is string[] P) && P.Length>1 ? //HashSet contains a __ between the variable name and the string lookup
			(typeof(PlayerData).GetField(P[0])?.GetValue(PD) as HashSet<string>)?.Contains(P[1]) ?? false : //HashSet<string>
			IsValueCompleted(typeof(PlayerData).GetField(Name)?.GetValue(PD) ?? null); //Scalars
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
			("Username", Config.C.AnonymousSubmissions ? UsernameErrorString : SteamUsername),
		}.Select(static Tuple => HttpUtility.UrlEncode(Tuple.Key)+"="+HttpUtility.UrlEncode(Tuple.Value)));

		//Send the web request and format returns
		string WebSubmit;
		return
			!IsSendingToo
				? (null!, $"{NewLine}<color=green><size=15>{TSan("Please consider contributing by sending your data")}</size></color>")
			: "SUCCESS"==(WebSubmit=await SendWebRequestReal($"{WebAddress}?{UrlQuery()}"))
				? (null!, $"{NewLine}<color=green><size=15>{TSan("Thank you for your submission!")}</size></color>")
				: ($"{NewLine}Web submission failed: {WebSubmit}", $"{NewLine}<color=red>{TrT("Web submission failed: <b>{0}</b>", San(WebSubmit))}</color>");
	}

	//Send the web request
	private async Task<string> SendWebRequestReal(string Url)
	{
		try {
			using HttpClient Client=new();
			HttpResponseMessage Response=await Client.GetAsync(Url);
			_=Response.EnsureSuccessStatusCode();
			return await Response.Content.ReadAsStringAsync();
		} catch(HttpRequestException HRE) {
			SocketException? SE=
				   HRE.InnerException as System.Net.Sockets.SocketException
				?? HRE.InnerException?.InnerException as System.Net.Sockets.SocketException;
			return SE==null ? HRE.ToString() :
				 $"HttpRequestException: {HRE.Message}\n"
				+$"SocketException: {SE.SocketErrorCode} ({SE.ErrorCode})\n"
				+$"NativeErrorCode: {SE.NativeErrorCode}\n"
				+$"SocketMessage: {SE.Message}";
		} catch(Exception e) {
			return e.Message;
		}
	}
}