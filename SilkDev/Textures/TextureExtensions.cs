using System.Runtime.CompilerServices;
using UnityEngine;

namespace SilkDev.Textures;

public static class TextureExtensions
{
	//Convert 2D texture absolute pixel coordinates into scaled 0-1.0 floats
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Rect ConvertTexCoords(this Rect R, Texture2D Tex) => new(R.position/Tex.Size, R.size/Tex.Size);

	//Static and property overrides
	extension(Texture2D T) {
		//Get texture size as a Vector2
		public Vector2 Size { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new(T.width, T.height); }

		//I decided to override this so I wouldn’t have to keep redefining the TextureFormat and mips chain
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Texture2D New(int Width=1, int Height=1) => new(Width, Height, TextureFormat.ARGB32, false);

		//Get a disposable texture
		public TypedDisposer<Texture2D> Disposable => new(T, static TD => TD.TDestroy(true));
	}

	//Destroy the texture. Checks for improper use in debug mode
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void TDestroy(this Texture2D T, bool IsSafeDestroy=false)
	{
		Object.Destroy(T);
		#if DEBUG
			if(!IsSafeDestroy)
				Log.Error($"Texture destroyed in unsafe manner:\n{new System.Diagnostics.StackTrace()}");
		#endif
	}

	//Make pure color textures and recolor them
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Texture2D MakeTexture(this Color c) => Texture2D.New().ReColor(c); //Create a 2x2 pixel texture to create solid colors
	public static Texture2D ReColor(this Texture2D Tex, Color c) //Replace the pixels inside a texture with a color
	{
		for(int x=0; x<Tex.width; x++)
			for(int y=0; y<Tex.height; y++)
				Tex.SetPixel(x, y, c);
		Tex.Apply();
		return Tex;
	}

	//Draw a colored rectangle
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void DrawRect(
		this Color C, Rect R, Texture? Image=null, ScaleMode ScaleMode=ScaleMode.StretchToFill,
		bool AlphaBlend=true, float ImageAspect=0, float BorderWidth=0, float BorderRadius=0
	) => GUI.DrawTexture(R, Image ?? Texture2D.whiteTexture, ScaleMode, AlphaBlend, ImageAspect, C, BorderWidth, BorderRadius);

	//Copy an unreadable texture (Texture2D.isReadable) to a readable texture. TexCoords when set will specify the coordinates to extract. ResizeDimensions when set will specify the final texture size.
	public static Texture2D ToReadable(this Texture2D Tex, Rect? TexCoords=null, Vector2? ResizeDimensions=null)
	{
		//Create a render texture that we will use for blitting/resizing
		Vector2 NewSize=ResizeDimensions ?? TexCoords?.size ?? Tex.Size;
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
		using TypedDisposer<Texture2D>? RT2=(TexCoords==null ? null : Texture2D.New((int)TC.size.x, (int)TC.size.y).Disposable);
		if(RT2!=null) {
			using TypedDisposer<Texture2D>? TextToCopyFrom=
				  !UnityEngine.Experimental.Rendering.GraphicsFormatUtility.IsCompressedFormat(Tex.format) ? null
				: Tex.ToReadable().Disposable; //If compressed then we are forced to create a copy of the whole texture first before extracting what we need //TODO: We could theoretically do compressed textures via multiples of 4 receivers for copies
			Graphics.CopyTexture(TextToCopyFrom?.Target ?? Tex, 0, 0, (int)TC.x, (int)TC.y, (int)TC.width, (int)TC.height, RT2.Target, 0, 0, 0, 0);
			Tex=RT2.Target;
		}

		//Copy (and resize) the texture
		Graphics.Blit(Tex, RT.Target);
		RenderTexture.active=RT.Target;
		using TypedDisposer<Texture2D> NewTex=Texture2D.New((int)NewSize.x, (int)NewSize.y).Disposable;
		NewTex.Target.ReadPixels(new Rect(Vector2.zero, NewSize), 0, 0);
		NewTex.Target.Apply();
		return NewTex.Detach();
	}

	//Convert a color to RRGGBBAA
	private static readonly System.Collections.Generic.Dictionary<Color, string> HexCache=[];
	extension(Color C) { public string Hex =>
		HexCache.TryGetValue(C, out string Val) ? Val :
		HexCache[C]=string.Format("{0:X2}{1:X2}{2:X2}{3:X2}",
			(int)(Mathf.Clamp01(C.r)*255),
			(int)(Mathf.Clamp01(C.g)*255),
			(int)(Mathf.Clamp01(C.b)*255),
			(int)(Mathf.Clamp01(C.a)*255)
		);
	};
}