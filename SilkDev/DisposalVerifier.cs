/*
Purpose: Make sure objects are properly disposed of during DEBUG.
Use this class as a drop-in replacement for the base type <T> used only during debug mode.
During release, everything should revert back to using the base type.
DisposalVerifiers only need to be kept as DisposalVerifiers for their primary owner. When borrowing the object elsewhere, you can revert it back to the base type for use.

See SafeTexture2D as an example of creating a drop in replacement for its Texture2D base.

---------------------------------------------------------------------------------
Using “class SafeTexture2D : DisposalVerifier<Texture2D>” as an example:
---------------------------------------------------------------------------------

Put this inside each file that uses it.
---------------------------------------------------------------------------------
#if DEBUG
	using SafeTexture2D = SilkDev.Textures.SafeTexture2D;
#else
	using SafeTexture2D = UnityEngine.Texture2D;
#endif

//This renames the base type so the analysis doesn’t throw a warning about not using “SafeTexture2D” instead of “Texture2D”.
//It also helps for searching/lookup purposes.
using RTexture2D = UnityEngine.Texture2D;
---------------------------------------------------------------------------------
---------------------------------------------------------------------------------
Also create extensions on the base class and add to it:
	* A “Disposable” member to make it a TypedDisposer
	* A disposal function (like Destroy()) that will be in both the base class (via extension) and DisposalVerifier derived class.
		* You can use Dispose() but that may interfeer with the base class if it is an IDisposable.
	* A property to access the inner Obj member that just returns itself (an alias is fine as long as they both have it)
---------------------------------------------------------------------------------
public static class TextureExtensions {
	extension(Texture2D T) {
		public TypedDisposer<Texture2D> Disposable => new(T, static TD => TD.Destroy(true));
		public Texture2D Tex { get => T; }
	}
	public static void Destroy(this Texture2D T, bool IsSafeDestroy=false)
	{
		Object.Destroy(T);
		#if DEBUG
			if(!IsSafeDestroy)
				Log.Error($"Texture destroyed in unsafe manner:\n{new System.Diagnostics.StackTrace()}");
		#endif
	}
}
---------------------------------------------------------------------------------
---------------------------------------------------------------------------------
Your class should also have the following:
---------------------------------------------------------------------------------
[Required]    public static void HandleLeak(T Obj, string CallerID);
[Recommended] public static implicit operator T(ParentT PT) => PT.Obj;
[Recommended] public static implicit operator ParentT(T CObj) => new(CObj);
---------------------------------------------------------------------------------
*/

using SilkDev.Events;
using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace SilkDev;

//Make sure objects are properly disposed of during DEBUG
public abstract class DisposalVerifier<T, ParentT> : IDisposable
	where ParentT :
		DisposalVerifier<T, ParentT>,
		DisposalVerifier<T, ParentT>.IHandleLeak
{
	//Initialize
	protected static int UniqueID=0;
	protected DisposalVerifier(T Obj, bool NonDisposable=false, int AddCallerStackCount=2)
	{
		//Initialize data and debug log
		(this.Obj, IsDisposed)=(Obj, NonDisposable);
		ID=System.Threading.Interlocked.Increment(ref UniqueID);
		var Frame=new System.Diagnostics.StackTrace(true).GetFrame(AddCallerStackCount+1);
		StaticDisposer.ObjData D=new(
			Frame.GetMethod()?.DeclaringType?.Name								?? "?Class",
			Frame.GetMethod()?.Name												?? "?Method",
			Frame.GetFileName()?[(Frame.GetFileName().LastIndexOf('\\')+1)..]	?? "?Filename",
			Frame.GetFileLineNumber(), DateTime.Now, ID, Obj
		);
		CallerID=$"{D.ClassName}.{D.FuncName}:{D.LineNumber}-{D.ID}{(NonDisposable ? " [NO DISPOSE]" : null)}";
		Log.Debug($"Created {typeof(T).Name}: {CallerID}");

		if(NonDisposable) //If not disposing, cancel GC
			GC.SuppressFinalize(this); //Suppress the finalizer so as to not promote this object to Gen 2 GC collection
		else //Store object and creation data in StaticDisposer since its reference data may already be gone during finalizing
			SD.Add(D);
	}

	//Handle disposing
	public readonly T Obj;
	public readonly int ID;
	public readonly string CallerID;
	public bool IsDisposed { get; private set; }
	protected abstract void BaseDispose();
	public void Dispose()
	{
		if(IsDisposed)
			return;
		IsDisposed=true;
		BaseDispose();
		SD.Remove(ID);
		GC.SuppressFinalize(this); //Suppress the finalizer so as to not promote this object to Gen 2 GC collection
		Log.Debug($"Destroyed {typeof(T).Name}: {CallerID}");
	}

	//Pass through types that will be handled via TypedDisposer during !DEBUG
	public ParentT Disposable => (ParentT)this;
	public ParentT Target => (ParentT)this;

	//Quick conversion to and from the base - Must be in base class
	//public static implicit operator T(ParentT PT) => PT.Obj;
	//public static implicit operator ParentT(T CObj) => new(CObj);

	//During GC, watch for objects that weren’t disposed
	~DisposalVerifier()
	{
		if(IsDisposed) //This shouldn’t be possible since finalizer is turned off during disposal
			Log.Error($"{typeof(T).Name} disposed but GC ran anyways: {CallerID}");
		else
			SD.Leaked(ID);
	}

	//An interface to handle a leak. This is required and will be loaded via reflection during runtime.
	public interface IHandleLeak
	{
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Empty marker interface due to runtime version")]
		protected static void HandleLeak(T Obj, string CallerID) { }
	}

	//During OnUpdate handle leaks
	private static readonly StaticDisposer SD=new();
	private class StaticDisposer
	{
		//Store info about the object until leaked
		public record struct ObjData(string ClassName, string FuncName, string FileName, int LineNumber, DateTime CreationTime, int ID, T Obj) {
			public override readonly string ToString() => $"{ClassName}.{FuncName}@{FileName}:{LineNumber}~{CreationTime:HH.mm.ss.fff}-{ID}";
		}
		private readonly ConcurrentDictionary<int, ObjData> LiveObjects=[];
		private readonly ConcurrentStack<ObjData> LeakedObjects=[];
		public void Add(ObjData D) => LiveObjects[D.ID]=D;
		public void Remove(int ID) => LiveObjects.TryRemove(ID, out _);
		public void Leaked(int ID) => Misc.IFF(LiveObjects.TryRemove(ID, out ObjData OData), () => LeakedObjects.Push(OData));

		//Initialize and handle leaks
		public StaticDisposer()
		{
			GameEvents.OnUpdate += OnUpdate;
			HandleLeak=GetHandleLeak();
		}
		public void OnUpdate() {
			while(LeakedObjects.TryPop(out ObjData ObjInfo))
				HandleLeak(ObjInfo.Obj, ObjInfo.ToString());
		}

		//Get the leak handler
		private readonly Action<T, string> HandleLeak;
		private Action<T, string> GetHandleLeak()
		{
			MethodInfo Method=typeof(ParentT).GetMethod("HandleLeak", BindingFlags.NonPublic|BindingFlags.Static) ??
				throw new InvalidOperationException($"No static HandleLeak method found in {typeof(ParentT).Name}");
			ParameterInfo[] Parameters=Method.GetParameters();
			return
				   Parameters.Length!=2
				|| Parameters[0].ParameterType!=typeof(T)
				|| Parameters[1].ParameterType!=typeof(string)
				|| Method.ReturnType!=typeof(void)
					? throw new InvalidOperationException($"Invalid HandleLeak signature in {typeof(ParentT).Name}. Expected: static void HandleLeak({typeof(T).Name}, string)")
					: (Action<T, string>)Delegate.CreateDelegate(typeof(Action<T, string>), Method);
		}
	}
}