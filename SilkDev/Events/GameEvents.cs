using HarmonyLib;
using System;

namespace SilkDev.Events;

//Subscribe to game events (Currently: Update, Game Loaded, Game Saved)
public static class GameEvents
{
	public static PrioritizedEvents<Action		> OnUpdate		=new(nameof(OnUpdate	)); //Runs on Plugin.Update()
	public static PrioritizedEvents<Action<int>	> OnGameLoaded	=new(nameof(OnGameLoaded)); //Runs when a new game is loaded
	public static PrioritizedEvents<Action<int>	> OnGameSaved	=new(nameof(OnGameSaved	)); //Runs when a game is saved

	private  static void Handle_GameLoaded	(int SaveSlot) => OnGameLoaded	.Run(F => F(SaveSlot));
	private  static void Handle_GameSaved	(int SaveSlot) => OnGameSaved	.Run(F => F(SaveSlot));
	internal static void Handle_Update		(			 ) => OnUpdate		.Run(				 );

	[HarmonyPatch(typeof(GameManager))]
	private static class GameManagerHooks
	{
		//Game loaded callback
		[HarmonyPostfix][HarmonyPatch("SetLoadedGameData",			[typeof(SaveGameData), typeof(int)])]
		private static void Postfix_SetLoadedGameData(int saveSlot) => Handle_GameLoaded(saveSlot);

		//Game saved callback
		[HarmonyPostfix][HarmonyPatch(nameof(GameManager.SaveGame),	[typeof(int), typeof(Action<bool>), typeof(bool), typeof(AutoSaveName)])]
		private static void Postfix_SaveGame		 (int saveSlot) => Handle_GameSaved(saveSlot);
	}
}