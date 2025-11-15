using BepInEx.Logging;

namespace SilkDev;

//Logging
public static class Log
{
	public enum DebugLogLevelEnum { None=0, Info, Warning, Error }
	private static ManualLogSource? Logger;
	internal static void InitBeforeConfig(ManualLogSource Logger) =>
		(Log.Logger, LLevel)=(Logger, LogLevel.Warning);
	internal static void InitAfterConfig()
	{
		LLevel=GetNewLogLevel;
		Internal.Config.C.DebugLogLevel.SettingChanged += (_, _) => LLevel=GetNewLogLevel;
	}

	private static LogLevel LLevel=LogLevel.Info;
	public static void Debug	(string Message	) => Logger!.LogDebug	(		Message);
	public static void Info		(string Message	) => Logger!.Log		(LLevel,Message);
	public static void Info		(object Obj		) => Logger!.Log		(LLevel,FileOps.SerializeToJSON(Obj));
	public static void Message	(string Message	) => Logger!.LogMessage	(		Message);
	public static void Warning	(string Message	) => Logger!.LogWarning	(		Message);
	public static void Error	(string Message	) => Logger!.LogError	(		Message);
	public static void Fatal	(string Message	) => Logger!.LogFatal	(		Message);
	private static LogLevel GetNewLogLevel =>
		Internal.Config.C.DebugLogLevel.V switch {
			DebugLogLevelEnum.None		=> LogLevel.None,
			DebugLogLevelEnum.Info		=> LogLevel.Info,
			DebugLogLevelEnum.Warning	=> LogLevel.Warning,
			DebugLogLevelEnum.Error		=> LogLevel.Error,
			_							=> LogLevel.Info,
		};
}