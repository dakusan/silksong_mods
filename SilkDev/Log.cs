using BepInEx.Logging;
using System.Linq;

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
		Internal.Config.C.DebugLogLevel.SettingChanged += static (_, _) => LLevel=GetNewLogLevel;
	}

	private static LogLevel LLevel=LogLevel.Info;
	public static void Info		(params object[] Obj) => ILog(Obj, false, LLevel			);
	public static void InfoT	(params object[] Obj) => ILog(Obj, true , LLevel			);
	public static void Debug	(params object[] Obj) => ILog(Obj, false, LogLevel.Debug	);
	public static void DebugT	(params object[] Obj) => ILog(Obj, true , LogLevel.Debug	);
	public static void Message	(params object[] Obj) => ILog(Obj, false, LogLevel.Message	);
	public static void MessageT	(params object[] Obj) => ILog(Obj, true , LogLevel.Message	);
	public static void Warning	(params object[] Obj) => ILog(Obj, false, LogLevel.Warning	);
	public static void WarningT	(params object[] Obj) => ILog(Obj, true , LogLevel.Warning	);
	public static void Error	(params object[] Obj) => ILog(Obj, false, LogLevel.Error	);
	public static void ErrorT	(params object[] Obj) => ILog(Obj, true , LogLevel.Error	);
	public static void Fatal	(params object[] Obj) => ILog(Obj, false, LogLevel.Fatal	);
	public static void FatalT	(params object[] Obj) => ILog(Obj, true , LogLevel.Fatal	);
	private static LogLevel GetNewLogLevel =>
		Internal.Config.C.DebugLogLevel.V switch {
			DebugLogLevelEnum.None		=> LogLevel.None,
			DebugLogLevelEnum.Info		=> LogLevel.Info,
			DebugLogLevelEnum.Warning	=> LogLevel.Warning,
			DebugLogLevelEnum.Error		=> LogLevel.Error,
			_							=> LogLevel.Info,
		};

	//If more than 1 object: Objects are separated by newlines and have “$Index. ” prepended
	//If TimeStamp is true, each object line has time format “HH:mm:ss.fff: ” prepended before the optional index
	//If individual object is a string, output as is. Otherwise, run through JSON.JsonUtils.Serialize()
	private static void ILog(object[] Objs, bool TimeStamp, LogLevel LLevel) =>
		Logger!.Log(
			LLevel,
			string.Join(Misc.NewLine, Objs.Select((Obj, Index) =>
				(!TimeStamp			? Misc.Empty : System.DateTime.Now.ToString("HH:mm:ss.fff: ")	)+
				(Objs.Length<=1		? Misc.Empty : $"{Index+1}. "									)+
				(Obj is string Str	? Str		 : JSON.JsonUtils.Serialize(Obj)					)
			))
		);
}