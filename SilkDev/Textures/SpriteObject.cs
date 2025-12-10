using UnityEngine;

namespace SilkDev.Textures;

//Base for sprite extraction
public abstract class SpriteObject(string Name, string ParentTree, GameObject GO)
{
	//Base information
	public readonly string Name=Name, ParentTree=ParentTree;
	public GameObject GO=GO;
	public Rect ScreenPos => GO==null ? Rect.zero : ExtractSpritesWindow.WorldBoundsToScreenRect(Bounds);

	//Overridden functions
	public abstract bool IsSafe			{ get; }
	public abstract Bounds Bounds		{ get; }
	public abstract Texture2D Texture	{ get; }
	public abstract Rect TextureRect	{ get; }
	public abstract float? PPU			{ get; }

	//Shared functions
	public static Rect TextureRectFromUVs(Vector2[] UVs, Vector2 TextureSize)
	{
		//Find the axis-aligned UV bounds in the atlas (handles pack-time flipping/rotation automatically)
		float MinU=float.MaxValue, MinV=float.MaxValue;
		float MaxU=float.MinValue, MaxV=float.MinValue;
		foreach(Vector2 UV in UVs) {
			MinU=Mathf.Min(MinU, UV.x);
			MaxU=Mathf.Max(MaxU, UV.x);
			MinV=Mathf.Min(MinV, UV.y);
			MaxV=Mathf.Max(MaxV, UV.y);
		}
		return new Rect(MinU, MinV, MaxU-MinU, MaxV-MinV).Mul(TextureSize);
	}

	//Renders a SpriteObject (with full transparency) into a Texture2D
	private const int CaptureLayer=31;
	public Texture2D CaptureToTexture(float? PixelsPerUnit=null, int MaxSize=2048)
	{
		//Init
		if(!IsSafe)
			throw new System.ArgumentException(Internal.Config.C.Tr.T("Could not get sprite from Renderer", "Errors"));
		float MyPPU=PixelsPerUnit ?? PPU ?? 100f;

		//Set a unique temporary layer
		int OriginalLayer=GO.layer; //Remember original layer
		GO.layer=CaptureLayer;

		//Create camera that only renders that layer
		Bounds B=Bounds;
		GameObject CamGO=new("SpriteRendererCaptureCam");
		var Cam=CamGO.AddComponent<Camera>();
		Cam.orthographic		=true;
		Cam.clearFlags			=CameraClearFlags.Color;
		Cam.backgroundColor		=new Color(0, 0, 0, 0);
		Cam.cullingMask			=1<<CaptureLayer;
		Cam.orthographicSize	=B.extents.y;
		Cam.transform.position	=B.center-Vector3.forward*10f;

		//Resize if necessary
		int Width =Mathf.CeilToInt(B.size.x*MyPPU);
		int Height=Mathf.CeilToInt(B.size.y*MyPPU);
		float AspectRatio=Width/(float)Height;
		if(Width>MaxSize) {
			Width=MaxSize;
			Height=Mathf.RoundToInt(Width/AspectRatio);
		}
		if(Height>MaxSize) {
			Height=MaxSize;
			Width=Mathf.RoundToInt(Height*AspectRatio);
		}

		//Capture
		RenderTexture RT=new(Width, Height, 24, RenderTextureFormat.ARGB32);
		Cam.targetTexture=RT;
		Cam.Render();
		RenderTexture.active=RT;
		Texture2D Tex=new(Width, Height, TextureFormat.ARGB32, false);
		Tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
		Tex.Apply();

		//Cleanup
		GO.layer=OriginalLayer;
		Cam.targetTexture=null;
		RenderTexture.active=null;
		Object.DestroyImmediate(RT);
		Object.DestroyImmediate(CamGO);

		return Tex;
	}
}

//The different types of sprites we can extract
public class SpriteObject_SpriteRenderer(string Name, string ParentTree, SpriteRenderer SR) : SpriteObject(Name, ParentTree, SR.gameObject)
{
	public SpriteRenderer SR			=  SR;
	public override bool IsSafe			=> SR.NullSafe?.sprite!=null;
	public override Bounds Bounds		=> SR.bounds;
	public override Texture2D Texture	=> SR.sprite.texture;
	public override Rect TextureRect	=> SR.sprite.textureRect;
	public override float? PPU			=> SR.sprite.pixelsPerUnit;
}

public class SpriteObject_tk2dBaseSprite(string Name, string ParentTree, tk2dBaseSprite TKS) : SpriteObject(Name, ParentTree, TKS.gameObject)
{
	public tk2dBaseSprite TKS			=  TKS;
	public override bool IsSafe			=> TKS.NullSafe?.CurrentSprite!=null && TKS.GetComponent<Renderer>()!=null && TKS.Collection!=null;
	public override Bounds Bounds		=> TKS.GetComponent<Renderer>().bounds;
	public override Texture2D Texture	=> (Texture2D)TKS.CurrentSprite.material.mainTexture;
	public override Rect TextureRect	=> TextureRectFromUVs(TKS.CurrentSprite.uvs, Texture.Size());
	public override float? PPU			=> 1f/TKS.CurrentSprite.texelSize.x;
}

//NOTE: I haven’t really been able to capture a worthwhile MeshFilter that wasn’t just a blank texture, so not sure how well this works
public class SpriteObject_MeshFilter(string Name, string ParentTree, MeshFilter MF) : SpriteObject(Name, ParentTree, MF.gameObject)
{
	public MeshFilter MF				=  MF;
	public override bool IsSafe			=> MF.NullSafe?.sharedMesh.NullSafe!=null && MF.GetComponent<Renderer>()?.sharedMaterial!=null;
	public override Bounds Bounds		=> MF.sharedMesh.bounds;
	public override Texture2D Texture	=> (Texture2D)MF.GetComponent<Renderer>().sharedMaterial.mainTexture;
	public override Rect TextureRect	=> TextureRectFromUVs(MF.sharedMesh.uv, Texture.Size());
	public override float? PPU			{ get {
		Vector2 PPU2=TextureRect.size/Bounds.size;
		return Mathf.Approximately(PPU2.x, PPU2.y) ? PPU2.x : null;
	} }
}