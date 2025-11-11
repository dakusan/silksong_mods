using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using UnityEngine;

namespace SilkDev;

public static class Extensions
{
	//Rect extensions
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static Rect SetX		(this Rect R, float X     ) { R.x		 =X     ; return R; }
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static Rect AddX		(this Rect R, float X     ) { R.x		+=X     ; return R; }
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static Rect SetY		(this Rect R, float Y     ) { R.y		 =Y     ; return R; }
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static Rect AddY		(this Rect R, float Y     ) { R.y		+=Y     ; return R; }
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static Rect SetWidth	(this Rect R, float Width ) { R.width	 =Width ; return R; }
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static Rect AddWidth	(this Rect R, float Width ) { R.width	+=Width ; return R; }
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static Rect SetHeight	(this Rect R, float Height) { R.height	 =Height; return R; }
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static Rect AddHeight	(this Rect R, float Height) { R.height	+=Height; return R; }
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static Rect Add		(this Rect R1, Rect R2)	=>	  new(R1.position+R2.position, R1.size+R2.size);
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static Rect Mul		(this Rect R1, float N)	=>	  new(R1.position*N, R1.size*N);
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static Rect Inverse	(this Rect R)			=>	  new(1/R.x, 1/R.y, 1/R.width, 1/R.height);
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static Rect CenterIn	(this Vector2 InnerSize, Vector2 OuterSize) => new((OuterSize-InnerSize)/2, InnerSize);

	//Textures
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static Rect ConvertTexCoords(this Rect R, Texture2D Tex) => new(R.position/Tex.Size(), R.size/Tex.Size()); //Convert 2D absolute sprite texture coordinates into scaled 0-1.0 floats
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static Vector2 Size(this Texture2D T) => new(T.width, T.height);
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static void TDestroy(this Texture2D T) => UnityEngine.Object.Destroy(T);
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Texture2D MakeTexture(this Color c) => new Texture2D(2, 2).ReColor(c); //Create a 2x2 pixel texture to create solid colors
	public static Texture2D ReColor(this Texture2D Tex, Color c) //Replace the pixels inside a texture with a color
	{
		for(int x=0; x<Tex.width; x++)
			for(int y=0; y<Tex.height; y++)
				Tex.SetPixel(x, y, c);
		Tex.Apply();
		return Tex;
	}

	//Copy an unreadable texture (Texture2D.isReadable) to a readable texture. Optional resize with second parameter.
	public static Texture2D ToReadable(this Texture2D Tex, Vector2? ResizeDimensions=null)
	{
		Vector2 NewSize=ResizeDimensions ?? Tex.Size();
		RenderTexture PrevRT=RenderTexture.active;
		using TypedDisposer<RenderTexture> RT=new(
			RenderTexture.GetTemporary((int)NewSize.x, (int)NewSize.y, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear),
			(Target) => {
				RenderTexture.active=PrevRT;
				RenderTexture.ReleaseTemporary(Target);
			}
		);

		Graphics.Blit(Tex, RT.Target);
		RenderTexture.active=RT.Target;
		using TypedDisposer<Texture2D> NewTex=new(
			new Texture2D((int)NewSize.x, (int)NewSize.y, TextureFormat.ARGB32, false),
			Target => Target.TDestroy()
		);
		NewTex.Target.ReadPixels(new Rect(Vector2.zero, NewSize), 0, 0);
		NewTex.Target.Apply();
		return NewTex.Detach();
	}

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
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0029:Use coalesce expression", Justification="UnityEngine.Object.GetCachedPtr thing")]
	public static T? NullSafe<T>(this T? UO) where T: UnityEngine.Object => UO==null ? null : UO;

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