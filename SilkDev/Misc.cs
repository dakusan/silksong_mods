using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using UnityEngine;

namespace SilkDev;

public static class Misc
{
	//Implement singletons
	//Singletons will always be set before anything tries to use it
	public static void InitSingleton<T>(T Self, ref T ObjProperty) =>
		ObjProperty=(ObjProperty==null ? Self : throw new InvalidOperationException($"Can only instance class ‘{typeof(T).Name}’ once"));

	//Save to clipboard
	public static void SaveToClipboard(string Value) =>
		GUIUtility.systemCopyBuffer=Value;

	//Short circuiting and return type passthrough functions for cleaner code
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void IFF(bool Cond, Action CallOnTrue) {
		if(Cond)
			CallOnTrue();
	}
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static RetType PassThru<Unused, RetType>(Unused _, RetType Return) => Return;
	public static RetType PassThru<RetType>(Action A, RetType Return) {
		A();
		return Return;
	}

	//Open Unity Explorer inspection on game object (if plugin is loaded)
	private static Action<GameObject>? Call_UnityExplorer_Inspect=null;
	public static void UnityExplorer_Inspect(GameObject GO)
	{
		//If we already created the function call, nothing else to do
		if(Call_UnityExplorer_Inspect!=null) {
			Call_UnityExplorer_Inspect(GO);
			return;
		}

		//Get the method to open a GameObject in the inspector
		MethodInfo? MI=
			Hooks.DynamicHook.FindType("UnityExplorer.InspectorManager")
			?.GetMethods(BindingFlags.Public | BindingFlags.Static)
			.FirstOrDefault(static m => m.Name=="Inspect" && m.GetParameters().Length==2 && m.GetParameters()[0].ParameterType==typeof(object));
		if(MI==null) {
			Call_UnityExplorer_Inspect=static _ => { };
			Log.Error("Could not find unity explorer");
			return;
		}

		//Create a method to make unity explorer show up
		PropertyInfo? ShowMenuPI=Hooks.DynamicHook.FindType("UnityExplorer.UI.UIManager")?.GetProperty("ShowMenu");
		Action ShowMenuAction=(ShowMenuPI==null ? static () => { } : () => ShowMenuPI.SetValue(null, true));

		//Create and run the delegate for the full action
		Call_UnityExplorer_Inspect=RunGO => { _=MI.Invoke(null, [RunGO, null!]); ShowMenuAction(); };
		Call_UnityExplorer_Inspect(GO);
	}

	//Fit GUI text onto a fixed width line
	public static void RenderFixedWidthLine(string Text, GUIStyle TextStyle, Action<GUIContent> Render, float KnownWidth=-1)
	{
		//Get the width
		float Width=KnownWidth;
		if(Width<=0) {
			Vector2 GetSize=GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(0), GUILayout.ExpandWidth(true)).size;
			GUILayout.Space(GetSize.y*-1);
			Width=GetSize.x;
		}

		//Calculate the new font size so it will fit
		GUIContent GText=new(Text);
		int StartFontSize=TextStyle.fontSize;
		float RenderedLineWidth=TextStyle.CalcSize(GText).x;
		bool ResizeNeeded=RenderedLineWidth>Width && Width>5;
		if(ResizeNeeded)
			TextStyle.fontSize=(int)(StartFontSize*Width/RenderedLineWidth);

		//Render and restore the font size
		Render(GText);
		if(ResizeNeeded)
			TextStyle.fontSize=StartFontSize;
	}

	//Simple reference class
	public class Ref<T>(T Value) {
		public T Value { get; set; } = Value;
	}

	//Atomic int operations for thread safety
	public sealed class AtomicInt(int InitialValue=0)
	{
		private int ValueInternal = InitialValue;
		public int Value {
			get								=> Volatile.Read(ref ValueInternal);
			set								=> Volatile.Write(ref ValueInternal, value);
		}
		public int IncrementVal()			=> Interlocked.Increment(ref ValueInternal);
		public int DecrementVal()			=> Interlocked.Decrement(ref ValueInternal);
		public int AddVal(int Delta)		=> Interlocked.Add(ref ValueInternal, Delta);
		public int Exchange(int NewValue)	=> Interlocked.Exchange(ref ValueInternal, NewValue);
		public bool CompareExchange(int NewValue, int Comparand)
		 									=> Interlocked.CompareExchange(ref ValueInternal, NewValue, Comparand) == Comparand;

		public void Increment()				=> IncrementVal();
		public void Decrement()				=> DecrementVal();
		public void Add(int Delta)			=> AddVal(Delta);
	}

	public const char NewLine='\n';
	public const string Empty="";
}