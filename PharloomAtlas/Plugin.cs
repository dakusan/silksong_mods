using BepInEx;
using SilkDev;
using SilkDev.Windows;
using System;
using Task = System.Threading.Tasks.Task;

namespace PharloomAtlas;

internal static class PluginInfo
{
	public const string PLUGIN_GUID="com.dakusan.pharloomatlas";
	public const string PLUGIN_NAME="Pharloom Atlas";
	public const string PLUGIN_VERSION="1.0.0";
}

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
[BepInDependency("com.dakusan.silkdev", BepInDependency.DependencyFlags.HardDependency)]
internal class Plugin : BaseUnityPlugin
{
	internal static new BepInEx.Logging.ManualLogSource? Logger;

	//Unity passthrough functions
	private void Awake() => Init();

	//Initialize the plugin
	private void Init()
	{
		//Add loggers
		#if DEBUG
			Log.AddLogger(
				"PAtlas-Debug",
				new AsyncLogger(FileOps.PathCombine(FileOps.GetPluginPath, "PAtlas-Debug.log")) { DuplicateWindowSeconds=10 }
			);
		#endif
		Log.AddLogger("PAtlas-Error", ErrorLog.Log);

		new HarmonyLib.Harmony(PluginInfo.PLUGIN_GUID).PatchAll();
		Catcher.Run($"{PluginInfo.PLUGIN_NAME} Config Init", () => new Config(Config));
		ErrorLog.Init();
		Catcher.Run($"{PluginInfo.PLUGIN_NAME} Init", () => {
			_=new MonitorSaveValues();
			_=new MoreMarkers();
		});
		Window.OnNextFrame(() => {
			SaveValuesWindow.Init();
			SearchWindow.Init();
			(Logger=base.Logger).LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded");
		});
	}

	//Error log
	private static readonly CErrorLog ErrorLog=new();
	internal class CErrorLog
	{
		internal const string				PAtlasErrorLogName="PAtlas-Error.log";
		private  const string				ErrorLogWebAddress="https://www.castledragmire.com/silksong/SubmitErrorLog.php", LogCutStr="...CUT...";
		private  const int					MaxLogSize=8*1024*1024;
		internal readonly AsyncLogger		Log=new(null, FilterErrorLog) { DuplicateWindowSeconds=10 };
		private  bool						IsSending=false;
		private  readonly object			LockIsSending=new();
		private  static Config				C => PharloomAtlas.Config.C;

		private class PopupWithCancel(string Message, Action OnClose) : PopupMessage(Message) {
			protected override void OnClosing()
			{
				OnClose();
				base.OnClosing();
			}
		}

		internal void Init()
		{
			void SetLogFilename() => Log.LogFilePath=(C.UseErrorLog ? FileOps.PathCombine(FileOps.GetPluginPath, PAtlasErrorLogName) : null);
			C.UseErrorLogSend.SettingChanged += (_, _) => Task.Run(SendErrorLog);
			C.UseErrorLog	 .SettingChanged += (_, _) => SetLogFilename();
			SetLogFilename();
		}

		private static string? FilterErrorLog(string LogLine) =>
			LogLine.StartsWith("[Error]") ? LogLine : null;

		private async Task SendErrorLog()
		{
			//Handle config entry toggle and only allow 1 send at a time
			if(!C.UseErrorLogSend)
				return;
			Window.OnNextFrame(static () => C.UseErrorLogSend.V=false);
			lock(LockIsSending) {
				if(IsSending)
					return;
				IsSending=true;
			}
			bool AlreadyUnlocked=false;
			using TypedDisposer<int> FreeIsSending=new(0, _ => { //Make sure lock is released when function is exiting
				lock(LockIsSending)
					if(!AlreadyUnlocked)
						IsSending=false;
			});

			//Handle popup message
			const string BaseText="Sending Error Log\n\n";
			HTTPPost CurrentSend=null!;
			PopupWithCancel StatusPopup=new(BaseText+"Initializing", () => CurrentSend?.Cancel());
			void SetPopupMessage(string Message) => StatusPopup!.Message=BaseText+Message;
			void ProgressCallback(long Sent, long Total, bool TotalIsEstimate)
			{
				lock(LockIsSending) Misc.IFF(
					IsSending,
					() => SetPopupMessage($"Progress: {(double)Sent/Total*100:0.00}% [{Sent:N0}/{(TotalIsEstimate ? "~" : "")}{Total:N0}]")
				);
			}

			//Get the log contents
			//TODO: This whole process could be done better with streams and spans so extra data is never processed.
			//TODO: We may also want to take the end of the log instead of the beginning on overruns.
			string LogContents, ErrMsg;
			try {
				if((LogContents=DevStrings.UTF8Cut(FileOps.ReadFileBytes(FileOps.PathCombine(FileOps.GetPluginPath, PAtlasErrorLogName), true), MaxLogSize, LogCutStr)).Length<=0)
					throw new("Log is empty");
			} catch(Exception e) {
				Catcher.OutputException(ErrMsg="Error reading log file", e);
				SetPopupMessage($"{ErrMsg}: {e.Message}");
				return;
			}

			//Execute the web request
			string FinalMessage;
			bool WasSuccess=false;
			try {
				CurrentSend=new HTTPPost(ErrorLogWebAddress, ProgressCallback);
				string? ReturnVal=await CurrentSend.Start(new() {
					{ "ErrorLog", LogContents }, //This must be first so partial sends are not counted as full sends by php
					{ "Username", DevStrings.SteamUsername },
				}).ConfigureAwait(false);
				FinalMessage=
					  ReturnVal==null ? "Send was cancelled"
					: (WasSuccess=(ReturnVal=="SUCCESS")) ? "Send was successful"
					: "Error: "+ReturnVal;
				SilkDev.Log.Info("ErrorLog send result: "+FinalMessage);
			} catch(Exception e) {
				Catcher.OutputException(ErrMsg="Error sending log file", e);
				FinalMessage=$"{ErrMsg}: {e.Message}";
			}

			CurrentSend?.Dispose();
			lock(LockIsSending) {
				AlreadyUnlocked=true;
				IsSending=false;
				SetPopupMessage($"<color={(WasSuccess ? "green" : "red")}>{FinalMessage}</color>");
			}
		}
	}
}