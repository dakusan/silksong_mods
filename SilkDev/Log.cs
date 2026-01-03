using BepInEx.Logging;
using System;
using System.IO;
using System.Threading;
using System.Linq;
using Task = System.Threading.Tasks.Task;

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
	private static void ILog(object[] Objs, bool TimeStamp, LogLevel LLevel)
	{
		//Output to the normal log
		string OutStr;
		Logger!.Log(
			LLevel,
			OutStr=string.Join(Misc.NewLine, Objs.Select((Obj, Index) =>
				(!TimeStamp			? Misc.Empty : DateTime.Now.ToString("HH:mm:ss.fff: ")	)+
				(Objs.Length<=1		? Misc.Empty : $"{Index+1}. "							)+
				(Obj is string Str	? Str		 : JSON.JsonUtils.Serialize(Obj)			)
			))
		);

		ALog.Log($"[{LLevel}] {OutStr}");
	}

	//Extra logging
	private static readonly AsyncLogger ALog=new();
	public static string? AdditionalLogFile { get => ALog.AdditionalLogFile; set { ALog.AdditionalLogFile=value; } }
}

//An additional logger
public class AsyncLogger : IDisposable
{
	//Base members
	private readonly System.Collections.Concurrent.ConcurrentQueue<string> Queue=new();
	private readonly SemaphoreSlim ActionReady=new(0, 1); //Using this as a boolean flag instead of a counter
	private readonly CancellationToken CancellationToken;
	private readonly CancellationTokenSource CancellationSource=new();
	private readonly Task MyTask;
	public string? AdditionalLogFile { get; set { field=value; SignalActionReady(); } }
	private StreamWriter? Writer;
	private readonly object LockWriter=new();

	//Write a log line. Does nothing if Writer is null
	public void Log(string LogLine)
	{
		lock(LockWriter) {
			if(Writer==null)
				return;
		}
		Queue.Enqueue(LogLine);
		SignalActionReady();
	}

	//Handle running the process asynchronously
	public AsyncLogger()
	{
		CancellationToken=CancellationSource.Token;
		MyTask=Task.Run(RunAsync);
	}
	private async Task RunAsync() {
		while(!CancellationToken.IsCancellationRequested)
			try {
				await Process();
			} catch(Exception e) {
				SilkDev.Log.Error($"Extra logger error: {e.Message}");
			}
	}

	//Signal the process that an update has been made
	private void SignalActionReady() {
		try { _=ActionReady.Release(); }
		catch(SemaphoreFullException) { } //Will throw on >1, which is fine
	}

	//The main processor
	private async Task Process()
	{
		//Wait for a signal
		await ActionReady.WaitAsync(CancellationToken);

		//If a new log file has been requested, update the writer
		if(CurrentLogFile!=AdditionalLogFile)
			lock(LockWriter)
				UpdateWriter();

		//Process all currently queued items and flush when complete
		Exception Ex;
		try {
			while(Queue.TryDequeue(out string logLine))
				await (Writer?.WriteLineAsync(logLine) ?? Task.CompletedTask);
			await (Writer?.FlushAsync() ?? Task.CompletedTask);
			return;
		} catch(Exception e) {
			Ex=e;
		}

		//Output the failure to the normal log and try reopening the log file
		lock(LockWriter) {
			string AdditionalLogMessage;
			try { Writer!.Close(); } catch(Exception) { }
			try {
				Writer=File.AppendText(CurrentLogFile!);
				AdditionalLogMessage=$" Log file reopened: {Ex.Message}";
			} catch(Exception e) {
				AdditionalLogMessage=$"\nOriginal error: {Ex.Message}\nReopen error: {e.Message}";
				Writer=null;
				CurrentLogFile=AdditionalLogFile=null;
			}
			SilkDev.Log.Error($"Writing to additional log failed:{AdditionalLogMessage}");
		}
	}

	//Open a new log file
	private string? CurrentLogFile=null;
	private void UpdateWriter()
	{
		//Close out the previous file and return if no new file is requested
		try { Writer?.Close(); } catch(Exception) { }
		Writer=null;
		if((CurrentLogFile=AdditionalLogFile)==null)
			return;

		//Attempt to open the new file
		try {
			Writer=File.AppendText(CurrentLogFile);
		} catch(Exception e) {
			SilkDev.Log.Error($"Could not open file for logging: {e.Message}");
			Writer=null;
			CurrentLogFile=AdditionalLogFile=null;
		}
	}

	//Safely stop the process
	public void Dispose()
	{
		CancellationSource.Cancel();
		SignalActionReady(); //Bypass possible deadlock
		MyTask.Wait();
		lock(LockWriter) {
			Writer?.Close();
			Writer=null;
			CurrentLogFile=AdditionalLogFile=null;
		}
	}
}