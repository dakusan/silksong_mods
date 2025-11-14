using SilkDev;
using UnityEngine;

namespace SilkDev.Textures;

public static class SpriteRendererCapture
{
	private const int CaptureLayer=31;

	//Renders a SpriteRenderer (with full transparency) into a Texture2D
	public static Texture2D CaptureToTexture(this SpriteRenderer Renderer, float? PixelsPerUnit=null, int MaxSize=2048)
	{
		Sprite Sprite=Renderer.NullSafe()?.sprite.NullSafe() ?? throw new System.ArgumentException("Could not get sprite from Renderer");
		float PPU=PixelsPerUnit ?? Sprite.pixelsPerUnit;

		//Set a unique temporary layer
		int OriginalLayer=Renderer.gameObject.layer; // Remember original layer
		Renderer.gameObject.layer=CaptureLayer;

		//Create camera that only renders that layer
		Bounds B=Renderer.bounds;
		GameObject CamGO=new("SpriteRendererCaptureCam");
		var Cam=CamGO.AddComponent<Camera>();
		Cam.orthographic		=true;
		Cam.clearFlags			=CameraClearFlags.Color;
		Cam.backgroundColor		=new Color(0, 0, 0, 0);
		Cam.cullingMask			=1<<CaptureLayer;
		Cam.orthographicSize	=B.extents.y;
		Cam.transform.position	=B.center-Vector3.forward*10f;

		//Resize if necessary
		int Width =Mathf.CeilToInt(B.size.x*PPU);
		int Height=Mathf.CeilToInt(B.size.y*PPU);
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
		Renderer.gameObject.layer=OriginalLayer;
		Cam.targetTexture=null;
		RenderTexture.active=null;
		Object.DestroyImmediate(RT);
		Object.DestroyImmediate(CamGO);

		return Tex;
	}
}