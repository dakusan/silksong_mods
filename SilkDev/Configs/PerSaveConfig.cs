using BepInEx.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace SilkDev.Configs;

public class PerSaveConfig
{
	private static readonly Dispatcher Dispatch=new();
	private const string ConfigDirectoryName="Configs";
	private DateTime LastSaveTime=DateTime.MinValue;
	private bool NeedsSave => HasUnsavedChanges && (DateTime.Now-LastSaveTime).TotalSeconds>SaveDelayInSeconds;

	public const string BackupExtension=".bak";
	public readonly ConfigFile CF;
	public readonly string BaseFileName, DLLPath;
	public float SaveDelayInSeconds=60;
	public bool HasUnsavedChanges { get; private set; } = false;
	public int NumBackupsToKeep=0;
	public Action<ConfigEntryBase>? ConfigChangedOnLoad;

	public PerSaveConfig(ConfigFile CF, string DLLPath)
	{
		(this.CF, this.DLLPath)=(CF, DLLPath);
		CF.SaveOnConfigSet=false;
		CF.SettingChanged += (_, _) => HasUnsavedChanges=true;
		BaseFileName=FileOps.GetFileName(CF.ConfigFilePath);
		Dispatch.Add(this);
	}

	//Change the config file to the appropriate path and reload
	private void GameLoaded()
	{
		//Make sure the config directory exists
		string DirName=FileOps.PathCombine(DLLPath, ConfigDirectoryName);
		try {
			if(!FileOps.DirectoryExists(DirName))
				_=FileOps.CreateDirectory(DirName);
		} catch(Exception e) {
			Log.Error($"Could not create config directory: {e.Message}");
			return;
		}

		//Set the config file to the new filename and save it if it doesn’t already exist
		string NewFilePath=ConfigFileName;
		new Reflectors.RField<ConfigFile, string>(CF, $"<{nameof(CF.ConfigFilePath)}>k__BackingField").Set(NewFilePath);
		if(!FileOps.FileExists(NewFilePath)) {
			SaveToFile();
			return;
		}

		//Make backups
		for(int i=NumBackupsToKeep;i>0;i--) {
			string OldFileName=NewFilePath+(i==1 ? "" : BackupExtension+(i-1));
			//File does not exist to copy/move
			if(!FileOps.FileExists(OldFileName))
				continue;

			//Make a copy of the current config
			string NewFileName=$"{NewFilePath}{BackupExtension}{i}";
			if(i==1) {
				Catcher.Run("Backup file copy", () => FileOps.FileCopy(OldFileName, NewFileName));
				continue;
			}

			//Cycle old backups
			if(i==NumBackupsToKeep && FileOps.FileExists(NewFileName)) //Delete the final backup first
				FileOps.FileDelete(NewFileName);
			Catcher.Run("Backup file move", () => FileOps.FileMove(OldFileName, NewFileName));
		}

		//Store currently loaded configs and reload new values
		Dictionary<string, object> PrevValues=[];
		foreach((ConfigDefinition Name, ConfigEntryBase ConfigEntry) in CF)
			PrevValues[$"{Name.Section}.{Name.Key}"]=ConfigEntry.BoxedValue;
		CF.Reload();

		//TODO: Need to handle OrderedConfig (Not much incentive now that I only need to add numbers to sections and not their settings)
		//If it already exists then we need to Determine which configs have changed and call their events
		string ConfigName;
		foreach((ConfigDefinition Name, ConfigEntryBase ConfigEntry) in CF) {
			//Config hasn’t changed
			if(PrevValues.Get(ConfigName=$"{Name.Section}.{Name.Key}")?.Equals(ConfigEntry.BoxedValue) ?? ConfigEntry.BoxedValue==null)
				continue;

			//Run ConfigChangedOnLoad and config.SettingChanged event
			if(ConfigChangedOnLoad!=null)
				Catcher.Run($"{ConfigName}.Changed.LoadCallback", () => ConfigChangedOnLoad(ConfigEntry));
			Catcher.Run($"{ConfigName}.Changed", () =>
				((EventHandler?)ConfigEntry.GetType().GetField("SettingChanged", BindingFlags.NonPublic|BindingFlags.Instance)?.GetValue(ConfigEntry))
				?.Invoke(ConfigEntry, new SettingChangedEventArgs(ConfigEntry))
			);
		}
	}

	//Write to the save file and reset flags
	public void SaveToFile()
	{
		CF.Save();
		LastSaveTime=DateTime.Now;
		HasUnsavedChanges=false;
	}

	public string ConfigFileName => FileOps.PathCombine(DLLPath, ConfigDirectoryName, $"{Dispatch.SaveSlotPrefix}.{BaseFileName}");

	private class Dispatcher() : Window(nameof(PerSaveConfig), false, -2000, true)
	{
		private readonly List<PerSaveConfig> ConfigsList=[];
		private string? Username; //Unfortunately, trying to get this on first plugin use fails because steam library has not been loaded yet
		private int CurSaveSlot=-1;
		public string SaveSlotPrefix => $"{Username}-{CurSaveSlot}";

		//Adding PerSaveConfigs
		protected override void DoLayout(int ID, Event Ev) { } //Not used
		public void Add(PerSaveConfig Config) => ConfigsList.Add(Config);

		//Inform all configs that a save was loaded
		protected override void OnGameLoaded(int SaveSlot)
		{
			if(Username==null && (Username=Misc.SteamUsername)==Misc.UsernameErrorString)
				Username="NoUsername";
			CurSaveSlot=SaveSlot;
			foreach(PerSaveConfig Config in ConfigsList)
				Config.GameLoaded();
		}

		protected override void OnGameSaved(int SaveSlot) => //Save any configs with outstanding changes
			ConfigsList.Where(static C => C.HasUnsavedChanges).ForEach(static C => C.SaveToFile());
		protected override void OnUpdate() => //Save any configs that need saving
			ConfigsList.Where(static C => C.NeedsSave).ForEach(static C => C.SaveToFile());
	}
}