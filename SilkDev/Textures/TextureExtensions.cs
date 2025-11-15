using System.Runtime.CompilerServices;
using UnityEngine;

namespace SilkDev.Textures;

public static class TextureExtensions
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static Rect ConvertTexCoords(this Rect R, Texture2D Tex) => new(R.position/Tex.Size(), R.size/Tex.Size()); //Convert 2D absolute sprite texture coordinates into scaled 0-1.0 floats
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static Vector2 Size(this Texture2D T) => new(T.width, T.height);
	[MethodImpl(MethodImplOptions.AggressiveInlining)] public static void TDestroy(this Texture2D T) => Object.Destroy(T);
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

	//Copy an unreadable texture (Texture2D.isReadable) to a readable texture. TexCoords when set will specify the coordinates to extract. ResizeDimensions when set will specify the final texture size.
	public static Texture2D ToReadable(this Texture2D Tex, Rect? TexCoords=null, Vector2? ResizeDimensions=null)
	{
		//Create a render texture that we will use for blitting/resizing
		Vector2 NewSize=ResizeDimensions ?? TexCoords?.size ?? Tex.Size();
		RenderTexture PrevRT=RenderTexture.active;
		using TypedDisposer<RenderTexture> RT=new(
			RenderTexture.GetTemporary((int)NewSize.x, (int)NewSize.y, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear),
			(Target) => {
				RenderTexture.active=PrevRT;
				RenderTexture.ReleaseTemporary(Target);
			}
		);

		//If specific texture coordinates were requested then do a CopyTexture
		Rect TC=TexCoords ?? Rect.zero;
		using TypedDisposer<Texture2D>? RT2=
			  TexCoords==null ? null
			: new(new Texture2D((int)TC.size.x, (int)TC.size.y, TextureFormat.ARGB32, false), static T => T.TDestroy());
		if(RT2!=null) {
			using TypedDisposer<Texture2D>? TextToCopyFrom=
				  !UnityEngine.Experimental.Rendering.GraphicsFormatUtility.IsCompressedFormat(Tex.format) ? null
				: new(Tex.ToReadable(), static T => T.TDestroy()); //If compressed then we are forced to create a copy of the whole texture first before extracting what we need
			Graphics.CopyTexture(TextToCopyFrom?.Target ?? Tex, 0, 0, (int)TC.x, (int)TC.y, (int)TC.width, (int)TC.height, RT2.Target, 0, 0, 0, 0);
			Tex=RT2.Target;
		}

		//Copy (and resize) the texture
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
}