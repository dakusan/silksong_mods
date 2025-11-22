using HutongGames.PlayMaker;
using NoClip;
using SilkDev;
using SilkDev.Windows;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PinFinder;

public static class FindPins
{
	//This can only run one at a time
	public static bool CurrentlyRunning { get; private set; } = false;
	private static ProgressBarWithLogs PBWL=null!;
	private const string MustClose="<color=red><size=50>You <size=60><b>MUST</b></size> now close this game with alt+f4 and restart it</size>";

	//Initialize
	internal static void Init()
	{
		//Handle other setting changes
		Config MyConfig=Config.C;
		bool DialogOpen=false;
		MyConfig.StartPinFindingProcess.SettingChanged += (_, _) => Window.OnNextFrame(() => {
			//Confirm we are ready to start
			bool StartFindConfig=MyConfig.StartPinFindingProcess;
			MyConfig.StartPinFindingProcess.V=false;
			if(!StartFindConfig || DialogOpen)
				return;

			//If the JSON file does not already exist, start the process without prompting the user
			if(!FileOps.FileExists(FileOps.PathCombine(Misc.GetPluginPath, Config.PinsJson))) {
				_=Catcher.ExecCoroutine("Find Pins", StartProcess());
				return;
			}

			//Ask the user if they want to start if the final file already exists
			DialogOpen=true;
			_=new DialogWindow
				($"{Config.PinsJson} already exists. Are you sure you wish to overwrite it? (A backup will be made)") {
					ConfirmationDialogCallback=Confirmed =>
						(DialogOpen, _)=(false, Confirmed ? Catcher.ExecCoroutine("Find Pins", StartProcess()) : null)
				};
		});
	}

	//The data on found objects
	public class FoundObj(string SceneName, string ObjName, object Value, Vector2 LocalPosition, FoundObj.FOType Type)
	{
		public enum FOType { PermBool, PermInt, Hero, Bench }
		public string SceneName=SceneName, ObjName=ObjName;
		public object Value=Value;
		public Vector2 LocalPosition=LocalPosition;
		public FOType Type=Type;
	}

	//Get the persistent variable value
	public static FoundObj? GetPersistData(GameObject Obj)
	{
		if(Obj==null)
			return null;

		//Test for PersistentBoolItem
		PersistentBoolItem PBI=Obj.GetComponent<PersistentBoolItem>();
		if(PBI!=null) {
			var Data=new Reflectors.RField<PersistentBoolItem, PersistentItemData<bool>>(PBI, "itemData").Get();
			return Data.ID is null or Misc.Empty ? null : new FoundObj(Data.SceneName, Data.ID, Data.Value, Obj.transform.position, FoundObj.FOType.PermBool); //{obj.scene.name}.{obj.name}
		}

		//Test for PersistentIntItem
		PersistentIntItem PII=Obj.GetComponent<PersistentIntItem>();
		if(PII!=null) {
			var Data=new Reflectors.RField<PersistentIntItem, PersistentItemData<int>>(PII, "itemData").Get();
			return Data.ID is null or Misc.Empty ? null : new FoundObj(Data.SceneName, Data.ID, Data.Value, Obj.transform.position, FoundObj.FOType.PermInt); //{obj.scene.name}.{obj.name}
		}

		//Test for RestBench
		RestBench RB=Obj.GetComponent<RestBench>();
		if(RB!=null)
			return new FoundObj(Obj.scene.name, Obj.name, "-", Obj.transform.position, FoundObj.FOType.Bench);

		//Test for FSM->QuestTargetPlayerDataBools
		foreach(PlayMakerFSM PmFsm in Obj.GetComponents<PlayMakerFSM>()) {
			if(PmFsm?.FsmVariables==null)
				return null;
			foreach(NamedVariable? NamedVar in new Reflectors.RProp<FsmVariables, NamedVariable[]>(PmFsm.FsmVariables, "allVariables") ?? new NamedVariable[] { }) {
				if(NamedVar is not FsmObject)
					continue;
				FsmObject FsmObj=(FsmObject)NamedVar;
				if(FsmObj.RawValue is not QuestTargetPlayerDataBools)
					continue;
				string VarName=new Reflectors.RField<QuestTargetPlayerDataBools, string>((QuestTargetPlayerDataBools)FsmObj.RawValue, "pdFieldTemplate").Get()+Obj.scene.name;
				bool VarVal=false;
				try {
					VarVal=new Reflectors.RField<PlayerData, bool>(PlayerData.instance, VarName).Get();
				} catch {
					LogError($"Warning: GetPersistData on {Obj.scene.name}.{Obj.name}: PlayerData.{VarName} variable does not exist");
				}
				return new FoundObj(Obj.scene.name, VarName, VarVal, Obj.transform.position, FoundObj.FOType.Hero);
			}
		}
		return null;
	}

	//Find an object in a scene by its name (whether it is hidden or not)
	public static GameObject? FindGameObjectInScene(Scene TheScene, string LookupName) =>
		TheScene.GetRootGameObjects()
		.Select(Parent => FindGameObjRecurse(LookupName, Parent.transform))
		.FirstOrDefault(static T => T!=null)
		?.gameObject;

	//Helper for the above function
	private static Transform? FindGameObjRecurse(string LookupName, Transform Parent) =>
		  Parent.name==LookupName ? Parent
		: Parent.GetEnumerator().AsEnumerable<Transform>()
			.Select(Child => FindGameObjRecurse(LookupName, Child))
			.FirstOrDefault(static T => T!=null);

	//Calls FindGameObjectInScene for the current scene
	public static GameObject? FindGameObjectInCurrentScene(string LookupName) =>
		FindGameObjectInScene(SceneManager.GetActiveScene(), LookupName);

	//Find all persistent objects in a scene
	public static List<FoundObj> FindPersistentObjectsInScene(Scene TheScene) =>
		TheScene.GetRootGameObjects()
		.Aggregate(new List<FoundObj>(), static (TheList, Parent) =>
			FindPersistentObjectsRecurse(TheList, Parent.transform)
		);

	//Helper for the above function
	private static List<FoundObj> FindPersistentObjectsRecurse(List<FoundObj> ObjList, Transform Parent)
	{
		try {
			FoundObj? NewVal=GetPersistData(Parent.gameObject);
			if(NewVal!=null)
				ObjList.Add(NewVal);
		} catch(Exception e) {
			LogError($"GetPersistData on {Parent.gameObject.scene.name}.{Parent.name}: {e.Message}");
		}

		foreach(Transform Child in Parent)
			_=FindPersistentObjectsRecurse(ObjList, Child);
		return ObjList;
	}

	//Get the list of the scene bundle files
	public static string[] AllSceneFiles =>
		[.. Directory.GetDirectories(Path.Combine(Application.streamingAssetsPath, "aa"), "Standalone*")
			.SelectMany(static Dir => Directory.EnumerateFiles(Path.Combine(Dir, "scenes_scenes_scenes"), "*.bundle"))
		];

	//Loads all the scenes and pulls out their persistent objects
	public static IEnumerator StartProcess()
	{
		//Make sure the save directory exists
		string SaveDir=Path.Combine([Misc.GetPluginPath, .. Config.PinTempDir]);
		if(!Directory.Exists(SaveDir)) {
			Log.Info($"Creating save directory: {SaveDir}");
			try {
				_=Directory.CreateDirectory(SaveDir);
			} catch(Exception e) {
				string ErrMsg=$"Save directory [{SaveDir}] creation failed. Cannot continue: {e.Message}";
				LogError(ErrMsg);
				_=new DialogWindow(ErrMsg, FontSize:28);
				yield break;
			}
		}

		//Only run one at a time and not if on the menu
		if(CurrentlyRunning) {
			_=new DialogWindow("Process is already running") { Priority=1500 };
			yield break;
		}
		if(SceneManager.GetActiveScene().name=="Menu_Title") {
			_=new DialogWindow("Cannot run on the title screen. Start your game (preferably on a new save).");
			yield break;
		}
		CurrentlyRunning=true;
		NCActivate.Self.Toggle(true); //Toggle noclip so player doesnt accidentally fall and force a scene change

		//Handle the progress bar
		int PB_TotalFiles=1, PB_CurrentFile=0, PB_FoundItems=0, PB_RunTime=0;
		string PB_FileName=Misc.Empty;
		PBWL?.Close();
		PBWL=new ProgressBarWithLogs();
		Action PB_Update=() => {
			PBWL.MessageText=$"[{PB_RunTime} sec] Found Items: {PB_FoundItems}; Currently processing: {PB_FileName}";
			PBWL.PercentText=$"{PB_CurrentFile}/{PB_TotalFiles}";
			PBWL.PercentAmount=PB_CurrentFile/(float)PB_TotalFiles;
		};

		//Watch for freeze. When it happens inform the user of the freeze
		Misc.Ref<DateTime> LastUpdate=new(new DateTime());
		Coroutine WatchForFreezeCo=Catcher.ExecCoroutine("WatchForFreeze", WatchForFreeze(LastUpdate));

		//Process each file
		DateTime StartTime=DateTime.Now;
		string[] AllScenes=AllSceneFiles;
		PB_TotalFiles=AllScenes.Length;
		foreach((int Index, string SceneFile) in AllScenes.Entries()) {
			//Skip if the file already exists
			string FileName=Path.GetFileName(SceneFile);
			string SavePath=Path.Combine(SaveDir, FileName+".obj");
			PB_CurrentFile=Index+1;
			if(File.Exists(SavePath)) {
				Log.Info($"Skipping (already processed): {FileName}");
				continue;
			}

			//Update log and progress bar
			Log.Info($"Starting to process scene {Index+1}/{AllScenes.Length} [{(DateTime.Now-StartTime).TotalSeconds} seconds]: {FileName}");
			PB_FileName=FileName;
			PB_RunTime=(int)(DateTime.Now-StartTime).TotalSeconds;
			PB_Update();

			//Check for bar closed
			if(PBWL.IsClosed)
				break;

			//Process the scene items
			List<FoundObj> RetObjects=[];
			yield return LoadAllPersistentObjectsInScene(SceneFile, RetObjects, true);
			PB_FoundItems+=RetObjects.Count();
			LastUpdate.Value=DateTime.Now;

			//Save the scene item
			try {
				File.WriteAllText(SavePath, FileOps.SerializeToJSON(RetObjects.ToArray()), System.Text.Encoding.UTF8);
			} catch(Exception e) {
				LogError($"Error saving to file [{SavePath}]: {e.Message}");
			}
		}

		//Output the found data
		string SuccessMessage=(PBWL.IsClosed ? "Stopped" : "Finished")+$" processing {PB_CurrentFile}/{AllScenes.Length} scenes at {(DateTime.Now-StartTime).TotalSeconds} seconds";
		Log.Info(SuccessMessage);
		if(PBWL.IsClosed) {
			CurrentlyRunning=false;
			_=new DialogWindow($"{MustClose}</color>\n\n{SuccessMessage}", FontSize:20, Height:480);
			yield break;
		}

		//Combine all the files into a final JSON
		PB_FileName=$"Combining files into {Config.PinsJson}";
		PB_Update();
		try {
			//If the file already exists, move it to a backup file
			string FinalFileName=Path.Combine(Misc.GetPluginPath, Config.PinsJson);
			if(File.Exists(FinalFileName)) {
				try { File.Delete(FinalFileName+".backup"); } catch { }
				File.Move(FinalFileName, FinalFileName+".backup");
			}

			//I didn’t make this part of the process handle errors or be very robust
			File.WriteAllText(FinalFileName, FileOps.SerializeToJSON(
				Directory.EnumerateFiles(SaveDir, "*.bundle.obj")
					.Select(static File => FileOps.DeserializeJson<List<FoundObj>>(FileOps.ReadFile(File)))
					.Aggregate(static (Acc, List) => { Acc.AddRange(List); return Acc; })
					?.ToArray() ?? throw new Exception("Data is null")
			));
			_=new DialogWindow($"{MustClose}</color>\n<size=35><color=green>Process complete</color></size>\n\n{SuccessMessage}", FontSize:20, Height:480) { Priority=1500 };
			try { Directory.Delete(SaveDir, true); } catch(Exception e) { LogError($"Error deleting temp files: {e.Message}"); }
		} catch(Exception e) {
			LogError($"Error combining files and writing final file: {e.Message}");
			_=new DialogWindow($"{MustClose}\n<size=35>Process failed on the final step.</size>\n{e.Message}</color>", FontSize:20, Height:480) { Priority=1500 };
		}

		CurrentlyRunning=false;

		//Since the session is foobared anyways, may as well leave these things as they are
		//PBWL.Close();
		//NCActivate.Self.Toggle(false);
	}

	//Watch for 15 second freeze every 5 seconds. When it happens inform the user of the freeze
	private static IEnumerator WatchForFreeze(Misc.Ref<DateTime> LastUpdate)
	{
		while(true) {
			yield return new WaitForSecondsRealtime(5f);
			if(CurrentlyRunning==false)
				break;
			if((DateTime.Now-LastUpdate.Value).TotalSeconds<15)
				continue;
			_=new DialogWindow($"{MustClose}\nThe processor has been on this scene for 15+ seconds. It will continue where it left off after you exit and restart the pin finding process.</color>\n\n<color=green><size=50%>This is normal. It generally happens to me around scene #400</size></color>", FontSize:30, Height:480) { Priority=1500 };
			break;
		}
	}
	private static IEnumerator LoadAllPersistentObjectsInScene(string SceneFile, List<FoundObj> RetObjects, bool IsFullProcessing=false)
	{
		//Skip the menu
		string FileName=Path.GetFileName(SceneFile);
		if(Array.Exists(
			Config.C.SkipScenes.V.Split(Misc.NewLine).Select(static s => s.Trim()).ToArray(),
			s => s==FileName
		)) {
			Log.Info($"Skipping (skip list): {FileName}");
			yield break;
		}

		//Load the asset bundle
		AssetBundleCreateRequest BundleLoadRequest=AssetBundle.LoadFromFileAsync(SceneFile);
		yield return BundleLoadRequest;
		AssetBundle Bundle=BundleLoadRequest.assetBundle;
		if(Bundle==null) {
			LogError($"Could not load bundle: {FileName}");
			yield break;
		}
		switch(Bundle.GetAllScenePaths().Length) {
			case 0:
				LogError($"No scenes in file: {FileName}");
				yield break;
			case 1:
				break;
			default:
				LogError($"Too many scenes in file (only processing 1st): {FileName}");
				break;
		}

		//Load the scene
		string ScenePath=Bundle.GetAllScenePaths()[0];
		yield return SceneManager.LoadSceneAsync(ScenePath);
		NCActivate.Self.Toggle(true); //Just in case, keep noclip going after scene loads
		Scene TheScene=SceneManager.GetSceneByPath(ScenePath);
		if(TheScene==null) {
			_=Bundle.UnloadAsync(true);
			yield break;
		}

		///Wait 1 second after loading has finished to give it time to process all events, then unload the bundle
		while(!TheScene.isLoaded)
			yield return new WaitForSecondsRealtime(0.1f);
		yield return new WaitForSecondsRealtime(1f);
		_=Bundle.UnloadAsync(true);

		//Find the persistent objects in the scene, transform them into map coordinates, and convert their data to a more readable form
		try {
			string[] SkipKeywordsList=[.. Config.C.SkipKeywords.V.Split(Misc.NewLine).Select(static s => s.Trim())];
			List<FoundObj> SceneItems=FindPersistentObjectsInScene(TheScene);
			GetScenePinData GSPD=new(TheScene.name);
			List<string> NewStrings=new(SceneItems.Count);
			RetObjects.Capacity=Mathf.Max(RetObjects.Count+SceneItems.Count, RetObjects.Capacity);
			foreach(FoundObj CurItem in SceneItems) {
				if(!KeepItem(CurItem, SkipKeywordsList))
					continue;
				Vector2 MapPos=CurItem.LocalPosition=GSPD.GetMapPositionFromLocalPosition(CurItem.LocalPosition);
				RetObjects.Add(CurItem);
				NewStrings.Add($"[{CurItem.Type}]{CurItem.SceneName}.{CurItem.ObjName}={CurItem.Value} [{MapPos.x}, {MapPos.y}]");
			}
			Log.Info("Scene items:"+(NewStrings.Count!=0 ? Misc.NewLine+string.Join(Misc.NewLine, NewStrings) : " NONE FOUND"));

			//Operations that only need to happen when we are full processing
			if(IsFullProcessing)
				PBWL.AddLogLines(NewStrings);
		} catch(Exception e) {
			LogError($"PLEASE REPORT THIS ERROR! We somehow errored out of processing during {SceneFile}: {e.Message}");
		}
	}

	//Filter out bad objects
	public static bool KeepItem(FoundObj CurItem, string[] SkipKeywordsList) => //Normally you’d want SkipKeywordsList to be Config.SkipKeywords
		!Array.Exists(SkipKeywordsList, s => CurItem.ObjName.Contains(s, StringComparison.OrdinalIgnoreCase));

	//Logs errors to BepInEx log and progress bar log
	private static void LogError(string Message)
	{
		Log.Error("ERROR: "+Message);
		PBWL.AddErrorLine(Message);
	}
}