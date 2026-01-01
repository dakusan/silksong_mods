using SilkDev.Internal;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace SilkDev;

//Used to call functions wrapped within a try/catch that will output a stack trace when caught (if config is on)
public static class Catcher
{
	private static bool OutputStack;
	static Catcher()
	{
		OutputStack=Config.C.ForceStackTrace;
		Config.C.ForceStackTrace.SettingChanged += static (_, _) => OutputStack=Config.C.ForceStackTrace;

		//Anything caught by unity will have already
		Application.logMessageReceived += static (condition, stackTrace, type) => Misc.IFF(
			OutputStack && type==LogType.Exception,
			() => Log.Error($"EXCEPTION: {condition}\nStack trace:\n{stackTrace}")
		);
	}

	//Runs a singlecast action and outputs stack trace (if turned on in config) for exceptions.
	public static void Run(string ActionName, Action A)
	{
		try {
			A();
		} catch(Exception e) {
			OutputException(ActionName, A, GetRelevantException(e));
		}
	}

	//Returns the first exception in the chain that is not a TargetInvocationException or TypeInitializationException. Stops on the last exception too.
	public static Exception GetRelevantException(Exception e)
	{
		while(e?.InnerException!=null && e is TargetInvocationException or TypeInitializationException)
			e=e.InnerException;
		return e!;
	}

	//Runs a list of singlecast action and outputs stack trace (if turned on in config) for exceptions. CallWrapper is required for any Action with parameters.
	public static void RunList<T>(string ActionName, IEnumerable<T> ActionList, Action<T>? CallWrapper=null) where T : Delegate
	{
		if(ActionList==null)
			throw new ArgumentNullException(nameof(ActionList));
		if(CallWrapper!=null)
			ActionList.ForEach(D => Run(ActionName, () => CallWrapper(D)));
		else if(ActionList is IEnumerable<Action> AL)
			AL.ForEach(D => Run(ActionName, D));
		else
			throw new ArgumentException("CallWrapper is required for Actions that take parameters");
	}

	//Runs a MulticastDelegate chain and outputs stack trace (if turned on in config) for exceptions. CallWrapper is required for any Action with parameters.
	public static void RunList<T>(string ActionName, T ActionMulticastDelegate, Action<T>? CallWrapper=null) where T : Delegate =>
		RunList(ActionName, ActionMulticastDelegate?.GetInvocationList().AsEnumerable().Cast<T>()!, CallWrapper);

	//Execute a coroutine
	public static Coroutine ExecCoroutine(string ActionType, IEnumerator TheFunc) =>
		Plugin.Self.StartCoroutine(WrapCoroutine(ActionType, TheFunc));
	private static IEnumerator WrapCoroutine(string ActionType, IEnumerator Coroutine)
	{
		while(true) {
			object Current;
			try {
				if(!Coroutine.MoveNext())
					yield break;
				Current=Coroutine.Current;
			} catch(Exception e) {
				OutputException($"{ActionType} coroutine failure", e);
				yield break;
			}

			yield return Current;
		}
	}

	//Output the stack trace for an exception you catch yourself
	public static void OutputException(string Name, Delegate D, Exception e) =>
		OutputException($"{Name} failure for {D.Target?.GetType().Name ?? "STATIC"}.{D.Method.Name} [{e.GetType().Name}]", e);

	//Output the stack trace for an exception you catch yourself
	public static void OutputException(string Message, Exception e) =>
		Log.Error(GetOutputException(Message, e));
	public static string GetOutputException(string Message, Exception e) =>
		$"{Message}: {e.Message}"+
		(!OutputStack ? Misc.Empty : Misc.NewLine+e.StackTrace.ToString());

	//Outputs stack trace (if turned on in config) for exceptions
	public static void Run(Func<string> ActionName, Action A) => Run(ActionName(), A);
}