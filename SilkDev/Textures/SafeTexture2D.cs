//This class is a drop-in replacement for Texture2D used only during debug mode.
//During release, everything reverts back to using Texture2D (aliased as RTexture2D).
//Texture2Ds only need to be kept as SafeTexture2Ds for their pimary owner. When borrowing the texture elsewhere, you can revert it back to Texture2Ds for use.

#if !DEBUG
//This is the only functionality that could not be reproduced to work for the SafeTexture2D safely.
//Specifically, when doing a null conditional operator on the object.
using System.Runtime.CompilerServices;
using UnityEngine;

namespace SilkDev.Textures;

public static class SafeTexExtension {
	extension(Texture2D Source) {
		public Texture2D Tex { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => Source; }
	}
}
#else

using System;
using UnityEngine;

namespace SilkDev.Textures;

//Watch for leaked textures during Debug
public class SafeTexture2D(Texture2D Tex, bool NonDisposable=false, int CallerStackCount=1) :
	DisposalVerifier<Texture2D, SafeTexture2D>(Tex, NonDisposable, CallerStackCount+1),
	DisposalVerifier<Texture2D, SafeTexture2D>.IHandleLeak
{
	public void TDestroy() => Dispose();
	protected override void BaseDispose() => UnityEngine.Object.Destroy(Tex);
	public Texture2D Tex => Obj;
	public static implicit operator Texture2D(SafeTexture2D PT) => PT.Obj;
	public static implicit operator SafeTexture2D(Texture2D CObj) => new(CObj);

	protected static void HandleLeak(Texture2D Obj, string CallerID)
	{
		string ObjName=$"Leaked-Texture-{DateTime.Now:yyyy-MM-dd_HH_mm_ss_fff}-{FileOps.FixFileName(CallerID.Replace(':', ';'))}.png";
		Log.Error($"Writing leaked Texture to {ObjName}");
		using TypedDisposer<Texture2D>? ReadableTexture=Obj.isReadable ? null : Obj.ToReadable().Disposable;
		FileOps.WriteFile(FileOps.PathCombine(FileOps.GetPluginPath, ObjName), (ReadableTexture?.Target ?? Obj).EncodeToPNG());
		Obj.TDestroy(true);
	}

//---------------------------------------------Pass through functions and members---------------------------------------------
	public SafeTexture2D(int Width, int Height, TextureFormat TextureFormat, bool MipChain)
		: this(new Texture2D(Width, Height, TextureFormat, MipChain)) { }
	public static SafeTexture2D New(int Width=1, int Height=1) =>
		new(new Texture2D(Width, Height, TextureFormat.ARGB32, false));

	//Ignore naming style on wrapped fields
#pragma warning disable IDE1006 //Naming Styles
	public string			name		=> Tex.name;
	public int				width		=> Tex.width;
	public int				height		=> Tex.height;
	public bool				isReadable	=> Tex.isReadable;
	public TextureWrapMode	wrapMode	{ get => Tex.wrapMode	; set => Tex.wrapMode	=value; }
	public FilterMode		filterMode	{ get => Tex.filterMode	; set => Tex.filterMode	=value; }
#pragma warning restore IDE1006

	public Color[]	GetPixels		(									) => Tex.GetPixels();
	public void		SetPixels		(Color[] Pixels						) => Tex.SetPixels(Pixels);
	public void		ReadPixels		(Rect Source, int DestX, int DestY	) => Tex.ReadPixels(Source, DestX, DestY);
	public void		Apply			(									) => Tex.Apply();
	public byte[]	EncodeToPNG		(									) => Tex.EncodeToPNG();
	public byte[]	EncodeToJPG		(int Quality						) => Tex.EncodeToJPG(Quality);
	public bool		LoadImage		(byte[] Data						) => Tex.LoadImage(Data);
	public Unity.Collections.NativeArray<Color32> GetPixelData<T> (int MipLevel) where T : struct => Tex.GetPixelData<Color32>(MipLevel); //This is a hack that only supports Color32

	public SafeTexture2D ToReadable(Rect? TexCoords=null, Vector2? ResizeDimensions=null) => Tex.ToReadable(TexCoords:TexCoords, ResizeDimensions:ResizeDimensions);
	public Texture2D ReColor(Color C) => Tex.ReColor(C);

	public SafeTexture2D? NullSafe => Tex==null ? null : this;
}
#endif