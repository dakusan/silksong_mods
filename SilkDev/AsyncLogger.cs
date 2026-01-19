using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using Task = System.Threading.Tasks.Task;

namespace SilkDev;

public interface ILoggerLog : IDisposable { public void Log(string LogLine); }

/*
Thread-safe, asynchronous file logger.
- Log() enqueues messages and returns immediately; a background task drains the queue and flushes to LogFilePath.
- If DuplicateWindowSeconds>0, repeated identical log lines (non-adjacent) within that window are de-duplicated:
	- The first duplicate is written with an annotation indicating time since first occurrence and the remaining pause interval.
	- Further duplicates are suppressed while paused and counted.
	- When the pause ends, a summary line is written indicating the actual pause duration and how many duplicates were suppressed.
	- If no natural post-pause log arrives, the summary is forced after AfterWindowForceSeconds.
- Optional FilterFunc can transform log lines or suppress them by returning null.
*/
public class AsyncLogger : ILoggerLog
{
	//Base members
	private readonly ConcurrentQueue<string> Queue=new();
	private readonly SemaphoreSlim ActionReady=new(0, 1); //Using this as a boolean flag instead of a counter
	private readonly CancellationToken CancellationToken;
	private readonly CancellationTokenSource CancellationSource=new();
	private readonly Task MyTask;
	private readonly Func<string, string?>? FilterFunc;
	private readonly object LockWriter=new();
	private StreamWriter? Writer;
	public string? LogFilePath { get; set { field=value; SignalActionReady(); } }

	//Write a log line. Does nothing if Writer is null
	public void Log(string LogLine)
	{
		lock(LockWriter) {
			if(Writer==null)
				return;
		}

		if(FilterFunc!=null && (LogLine=FilterFunc(LogLine)!)==null)
			return;

		lock(LockDuplicate)
			if(IsDuplicate(LogLine))
				return;
		Queue.Enqueue(LogLine);
		SignalActionReady();
	}

	//Handle running the process asynchronously
	public AsyncLogger(string? LogFilePath, Func<string, string?>? FilterFunc=null)
	{
		this.FilterFunc=FilterFunc;
		CancellationToken=CancellationSource.Token;
		MyTask=Task.Run(RunAsync);
		DuplicateTask=Task.Run(DuplicateMonitorAsync);
		this.LogFilePath=LogFilePath;
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
		if(CurrentLogFile!=LogFilePath)
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
				CurrentLogFile=LogFilePath=null;
			}
			SilkDev.Log.Error($"Writing to async log failed:{AdditionalLogMessage}");
		}
	}

	//Open a new log file
	private string? CurrentLogFile=null;
	private void UpdateWriter()
	{
		//Close out the previous file and return if no new file is requested
		try { Writer?.Close(); } catch(Exception) { }
		Writer=null;
		if((CurrentLogFile=LogFilePath)==null)
			return;

		//Attempt to open the new file
		try {
			Writer=File.AppendText(CurrentLogFile);
		} catch(Exception e) {
			SilkDev.Log.Error($"Could not open file for logging: {e.Message}");
			Writer=null;
			CurrentLogFile=LogFilePath=null;
		}
	}

	//Safely stop the process
	public void Dispose()
	{
		CancellationSource.Cancel();
		SignalActionReady(); //Bypass possible deadlock
		MyTask.Wait();
		DuplicateTask.Wait();

		lock(LockWriter) {
			Writer?.Close();
			Writer=null;
			CurrentLogFile=LogFilePath=null;
		}
	}

	//-------------------------------------------------------Deduplication supression-------------------------------------------------------
	private readonly ConcurrentDictionary<string, DuplicateState> DuplicateStates=[];
	private readonly Task DuplicateTask;
	private readonly object LockDuplicate=new();
	public double DuplicateWindowSeconds=0; //Set to >0 to enable this functionality
	public double AfterWindowForceSeconds=1;

	private sealed class DuplicateState
	{
		public DateTime StartTime, PauseStart, PauseUntil;
		public int DuplicateCount;
		public bool IsPaused;
	}

	//Handle duplicate messages
	private bool IsDuplicate(string LogLine)
	{
		#if DEBUG
			System.Diagnostics.Debug.Assert(Monitor.IsEntered(LockDuplicate), "LockDuplicate must be held before entering this function");
		#endif

		//Ignore functionality if DuplicateWindowSeconds<=0
		if(DuplicateWindowSeconds<=0)
			return false;

		//First occurrence (output)
		DateTime CurTime=DateTime.UtcNow;
		if(!DuplicateStates.TryGetValue(LogLine, out DuplicateState State)) {
			DuplicateStates[LogLine]=new DuplicateState { StartTime=CurTime };
			return false;
		}

		//Still paused (suppress)
		if(State.IsPaused && CurTime<State.PauseUntil) {
			State.DuplicateCount++;
			return true;
		}

		//Pause window exceeded (output and reset)
		if(State.IsPaused && CurTime>=State.PauseUntil)
		{
			LogSupressedLine(LogLine, CurTime, State, true);
			State.StartTime=CurTime;
			State.IsPaused=false;
			State.DuplicateCount=0;
			return true;
		}

		//Duplicate window passed (reset)
		double SinceFirstSeconds=(CurTime-State.StartTime).TotalSeconds;
		if(SinceFirstSeconds>DuplicateWindowSeconds) {
			State.StartTime=CurTime;
			return false;
		}

		//Handle first duplicate (output and start)
		double PauseIntervalSeconds=DuplicateWindowSeconds-SinceFirstSeconds;
		LogDupLine(LogLine, $"Duplicate message received after {SinceFirstSeconds:0.00} seconds and paused for {PauseIntervalSeconds:0.00} seconds");
		State.IsPaused=true;
		State.PauseStart=CurTime;
		State.PauseUntil=CurTime.AddSeconds(PauseIntervalSeconds);
		State.DuplicateCount=0;
		return true;
	}
	private void LogDupLine(string LogLine, string Message)
	{
		Queue.Enqueue(LogLine+DevStrings.NewLine+"***"+Message+"***");
		SignalActionReady();
	}
	private void LogSupressedLine(string LogLine, DateTime CurTime, DuplicateState State, bool PlusOne) =>
		LogDupLine(LogLine, $"This message was paused for {(CurTime-State.PauseStart).TotalSeconds:0.00} seconds and was repeated {State.DuplicateCount}{(PlusOne ? "+1" : null)} times");

	//Call HandleDuplicateTimeouts every second
	private async Task DuplicateMonitorAsync()
	{
		while(!CancellationToken.IsCancellationRequested)
			try {
				await Task.Delay(1000, CancellationToken);
				lock(LockDuplicate)
					HandleDuplicateTimeouts();
			} catch(OperationCanceledException) {
				return;
			} catch(Exception e) {
				SilkDev.Log.Error($"Logger duplicate supression monitor error: {e.Message}");
			}
	}

	//Watch for duplicate messages that have passed their PauseUntil and clear unique log messages after the DuplicateWindowSeconds has passed
	private void HandleDuplicateTimeouts()
	{
		#if DEBUG
			System.Diagnostics.Debug.Assert(Monitor.IsEntered(LockDuplicate), "LockDuplicate must be held before entering this function");
		#endif

		DateTime CurTime=DateTime.UtcNow;
		System.Collections.Generic.List<string>? RemoveKeys=null;
		foreach((string LogLine, DuplicateState State) in DuplicateStates)
		{
			//Remove lines that were not duplicated within their DuplicateWindowSeconds
			if(!State.IsPaused) {
				if(CurTime>State.StartTime.AddSeconds(DuplicateWindowSeconds))
					if(RemoveKeys==null)
						RemoveKeys=[LogLine];
					else
						RemoveKeys.Add(LogLine);
				continue;
			}

			//Force the log line if it didn’t arrive naturally within PauseUntil+AfterWindowForceSeconds
			if(CurTime<State.PauseUntil.AddSeconds(AfterWindowForceSeconds))
				continue;
			LogSupressedLine(LogLine, CurTime, State, false);
			State.StartTime=CurTime;
			State.IsPaused=false;
			State.DuplicateCount=0;
		}

		//Remove log lines whose DuplicateWindowSeconds has passed
		if(RemoveKeys!=null)
			foreach(string LogLine in RemoveKeys)
				_=DuplicateStates.TryRemove(LogLine, out _);
	}
}