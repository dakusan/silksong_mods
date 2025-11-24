using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using UnityEngine;
using fl = float;

namespace SilkDev;

public static class Extensions
{
	//Rect extensions
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static Rect SetX		(this Rect R, float X     )	{ R.x		 =X		; return R; }
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static Rect AddX		(this Rect R, float X     )	{ R.x		+=X		; return R; }
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static Rect SetY		(this Rect R, float Y     )	{ R.y		 =Y		; return R; }
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static Rect AddY		(this Rect R, float Y     )	{ R.y		+=Y		; return R; }
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static Rect SetWidth	(this Rect R, float Width )	{ R.width	 =Width	; return R; }
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static Rect AddWidth	(this Rect R, float Width )	{ R.width	+=Width	; return R; }
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static Rect SetHeight	(this Rect R, float Height)	{ R.height	 =Height; return R; }
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static Rect AddHeight	(this Rect R, float Height)	{ R.height	+=Height; return R; }
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static Rect SetPos	(this Rect R, Vector2 Pos )	{ R.position =Pos	; return R; }
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static Rect AddPos	(this Rect R, Vector2 Pos )	{ R.position+=Pos	; return R; }
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static Rect SetSize	(this Rect R, Vector2 Size)	{ R.size	 =Size	; return R; }
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static Rect AddSize	(this Rect R, Vector2 Size)	{ R.size	+=Size	; return R; }
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static Rect SetX		(this Rect R, Func<float,	float  > MFunc)	{ R.x		=MFunc(R.x			); return R; }
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static Rect SetY		(this Rect R, Func<float,	float  > MFunc)	{ R.y		=MFunc(R.y			); return R; }
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static Rect SetWidth	(this Rect R, Func<float,	float  > MFunc)	{ R.width	=MFunc(R.width		); return R; }
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static Rect SetHeight	(this Rect R, Func<float,	float  > MFunc)	{ R.height	=MFunc(R.height		); return R; }
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static Rect SetPos	(this Rect R, Func<Vector2,	Vector2> MFunc)	{ R.position=MFunc(R.position	); return R; }
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static Rect SetSize	(this Rect R, Func<Vector2,	Vector2> MFunc)	{ R.size	=MFunc(R.size		); return R; }
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static Rect Add		(this Rect R1, Rect R2	) => new(R1.position+R2.position, R1.size+R2.size);
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static Rect Mul		(this Rect R1, float N	) => new(R1.position*N, R1.size*N);
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static Rect Mul		(this Rect R1, Vector2 V) => new(R1.position*V, R1.size*V);
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static Rect Grow		(this Rect R,fl X,fl Y	) => R.Add(new(-X, -Y, X*2, Y*2)); //Give negative numbers to shrink
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static Rect Inverse	(this Rect R			) => new(1/R.x, 1/R.y, 1/R.width, 1/R.height);
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static Rect CenterIn	(this Vector2 InnerSize, Vector2 OuterSize) => new((OuterSize-InnerSize)/2, InnerSize);

	//Delegate extensions
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T Toggle<T>(this T Del, T Handler, bool Enable) where T : Delegate =>
		Enable ? (T)Delegate.Combine(Del, Handler) : (T)Delegate.Remove(Del, Handler);

	//Generics extensions
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void ForEach<T>(this IEnumerable<T> IEnum, Action<T> Action) {
		foreach(T Item in IEnum)
			Action(Item);
	}
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static IEnumerable<(int Index, T Value)> Entries<T>(this IEnumerable<T> Source)
	{
		int i=0;
		foreach(T Item in Source)
			yield return (i++, Item);
	}
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static IEnumerable<T> AsEnumerable<T>(this IEnumerator enumerator) {
		while(enumerator.MoveNext())
			yield return (T)enumerator.Current;
	}
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static IEnumerable<T> AsEnumerable<T>(this IEnumerator<T> enumerator) {
		while(enumerator.MoveNext())
			yield return enumerator.Current;
	}
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static TValue? Get<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> Dict, TKey Key) =>
		Dict.GetValueOrDefault(Key);

	//Unity stuff
	//If calling a unity-nulled UnityEngine.Object with a null conditional operator, it still throws a null exception. This fixes it.
	extension<T>(T Source) where T: UnityEngine.Object
	{
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0029:Use coalesce expression", Justification="UnityEngine.Object.GetCachedPtr thing")]
		public T? NullSafe { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => Source==null ? null : Source; }
	}
	extension(Screen) { public static Vector2 Size => new(Screen.width, Screen.height); }

	//Turn a Task into a Coroutine IEnumerator
	public static IEnumerator AsCoroutine(this Task Task, Misc.Ref<Exception?> Err)
	{
		while(!Task.IsCompleted)
			yield return null;
		if(Task.IsFaulted)
			Err.Value=Task.Exception!;
	}

	//Streams
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static string ReadAllAndCloseS(this Stream Stream)
	{
		using StreamReader Reader=new(Stream);
		return Reader.ReadToEnd();
	}
	public static byte[] ReadAllAndCloseB(this Stream Stream)
	{
		using Stream Reader=Stream;
		if(!Reader.CanSeek)
			throw new NotSupportedException("Stream must be seekable");
		long Length=Reader.Length;
		byte[] Data=new byte[Length];
		int Offset=0;
		while(Offset<Length) {
			int ReadAmount=Reader.Read(Data, Offset, (int)(Length-Offset));
			if(ReadAmount==0)
				throw new IOException("Incomplete read");
			Offset+=ReadAmount;
		}
		return Data;
	}
}