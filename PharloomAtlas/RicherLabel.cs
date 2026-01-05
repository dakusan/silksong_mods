using SilkDev;
using SilkDev.Textures;
using SilkDev.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Button = SilkDev.DevInput.Mouse.Button;

#if DEBUG
	using SafeTexture2D = SilkDev.Textures.SafeTexture2D;
#else
	using SafeTexture2D = UnityEngine.Texture2D;
#endif
using RTexture2D = UnityEngine.Texture2D;

namespace PharloomAtlas;

public class RicherLabel() : LinkedLabel, IDisposable
{
	//Process found/started links
	protected override void ParseComplete()
	{
		foreach(Link L in ActiveLinks)
			if(
				   int.TryParse(L.Attributes.Get("ItemID") ?? Misc.Empty, out int LID)
				&& MapControl.Self.DS.Items.TryGetValue(LID, out Item I)
				&& (I.IsFound || I.IsStarted)
			)
				L.StrikeColor=(I.IsFound ? Color.white : Color.gray);

		base.ParseComplete();
	}

	//Extract important links
	private const string ImportantAttr="Important";
	protected override IEnumerable<Link> RequiredRects(IEnumerable<Link> Links)
	{
		ImportantLinks.ForEach(static IL => IL.Dispose());
		ImportantLinks.Clear();
		return base.RequiredRects(ActiveLinks.Where(static L => L.Attributes.ContainsKey(ImportantAttr)).Union(Links));
	}

	//Process important links
	protected override void RectsGenerated(Link L, RTexture2D Tex)
	{
		if(L.Attributes.ContainsKey(ImportantAttr))
			ImportantLinks.Add(new ImportantLink(L, Tex));
		base.RectsGenerated(L, Tex);
	}

	public override void Dispose()
	{
		base.Dispose();
		ImportantLinks.ForEach(static IL => IL.Dispose());
		ImportantLinks.Clear();
	}

	public void Draw(string LabelText, GUIStyle Style, int SelectedItem)
	{
		//Render the label and important links
		Link? L=GUILabelLayout(
			LabelText, Style,
			SelectedItem!=-1 ? [ActiveLinks[SelectedItem]] : []
		);
		ImportantLinks.ForEach(IL => IL.Render(Pos));

		//Handle link click
		if(Event.current.type==EventType.MouseDown && Button.CurrentButton==Button.Enum.Left)
			LinkClicked(L);
	}

	public void LinkClicked(Link? L)
	{
		//Ignore important links
		if(L?.Attributes.ContainsKey("Important")!=false)
			return;

		MapControl.Self.DS.LinkSelected(L.Attributes.Get("ItemID") ?? "1");
	}

	//Handle “Important” links
	private readonly List<ImportantLink> ImportantLinks=[];
	private class ImportantLink : IDisposable
	{
		//Initialize the shader
		private const string BundleFile="PharloomAtlas.bundle", ShaderFile="tk2d_OverlayBlend.shader";
		private static readonly Color Orange=new(1, 165f/255, 0, 1);
		private static readonly Material? Material;
		private const float BaseSpeed=0.25f;
		static ImportantLink()
		{
			//Create a material from the new shader
			try {
				using TypedDisposer<AssetBundle> Bundle=new(
					AssetBundle.LoadFromStream(FileOps.LoadLocalFileOrResource(BundleFile)),
					static Target => Target.Unload(false)
				);
				Material=new Material(Bundle.Target.LoadAsset<Shader>(ShaderFile));
			} catch(Exception e) {
				Log.Info($"Could not load shader: {e.Message}");
				return;
			}

			//Create and set the rainbow texture
			const int NumPixels=256;
			RTexture2D RainbowTexture=RTexture2D.New(NumPixels, 1);
			Color[] Pixels=new Color[NumPixels];
			for(int i=0; i<NumPixels; i++) {
				Color NewColor=Color.HSVToRGB((float)i/NumPixels, 1f, 1f);
				NewColor.a=1;
				Pixels[i]=NewColor;
			}
			RainbowTexture.SetPixels(Pixels);
			RainbowTexture.Apply();
			RainbowTexture.wrapMode=TextureWrapMode.Repeat;
			Material.SetTexture("_OverlayTex", RainbowTexture);
		}

		//Render information
		private const float TimeStateSeed=2.2941f;
		private readonly Link L;
		private readonly SafeTexture2D RenderFrom, RenderEnd;
		private readonly RenderTexture RenderTo;
		private readonly int OffsetDrawX, OffsetDrawY, RenderSizeX, RenderSizeY;
		private float DrawTimeState() => DateTime.Now.Ticks%10000000000L/10000000f+(10f/BaseSpeed*L.StringStartPos*TimeStateSeed); //Make different labels start at different places
		public ImportantLink(Link L, RTexture2D FullRender)
		{
			//If material loading failed or there are no rects, we will not be creating the texture
			this.L=L;
			if(Material==null || !L.Rects.Any()) {
				Log.Info("Creating Important Link Failed: "+(Material==null ? "No material" : "No rects"));
				RenderFrom=RenderEnd=null!;
				RenderTo=null!;
				return;
			}

			//Do not render the text since we will be rendering it ourself
			L.NormalColor=L.HoverColor=new Color(0, 0, 0, 0);

			//Calculate dimensions and create render textures
			OffsetDrawX=L.Rects.Min(static R => (int) R.x);
			OffsetDrawY=L.Rects.Min(static R => (int) R.y);
			RenderSizeX=L.Rects.Max(static R => (int)(R.x+R.width ))-OffsetDrawX;
			RenderSizeY=L.Rects.Max(static R => (int)(R.y+R.height))-OffsetDrawY;
			RenderFrom=SafeTexture2D.New(RenderSizeX, RenderSizeY);
			RenderEnd =SafeTexture2D.New(RenderSizeX, RenderSizeY);
			RenderTo=new RenderTexture(RenderSizeX, RenderSizeY, 0, RenderTextureFormat.ARGB32);
			Graphics.CopyTexture(
				FullRender, 0, 0, OffsetDrawX, FullRender.height-OffsetDrawY-RenderSizeY, RenderSizeX, RenderSizeY,
				RenderFrom, 0, 0, 0, 0
			);
		}

		//Render the important label
		private string? LastOutputException=null;
		public void Render(Vector2 OffsetPos)
		{
			//Render through the shader
			RenderTexture PrevRT=RenderTexture.active;
			if(Material!=null && RenderFrom!=null)
				try {
					//Set shader variables for the material render
					float TexScale=RenderSizeX/255f;
					Material.SetTextureScale("_OverlayTex", new Vector2(TexScale, 1));
					Material.SetTextureOffset("_OverlayTex", new Vector2(Mathf.Repeat(DrawTimeState()*BaseSpeed, 1f), 0));
					Material.SetVector("_LineOffsetScale", new Vector2(0.15f*TexScale, 1));

					//Render from RenderFrom+Material->RenderTo->RenderEnd
					RenderTexture.active=RenderTo;
					Graphics.Blit(RenderFrom, Material);
					RenderEnd.ReadPixels(new Rect(0, 0, RenderSizeX, RenderSizeY), 0, 0);
					RenderEnd.Apply();
					RenderTexture.active=PrevRT;

					//Draw to the screen
					GUI.DrawTexture(new Rect(OffsetDrawX+OffsetPos.x, OffsetDrawY+OffsetPos.y, RenderSizeX, RenderSizeY), RenderEnd);

					//Handle previous exceptions
					if(LastOutputException!=null) //If there was an exception then we need to reset the link colors
						L.NormalColor=L.HoverColor=new Color(0, 0, 0, 0);
					LastOutputException=null; //Erase previous exception strings
					return;
				} catch(Exception e) {
					RenderTexture.active=PrevRT;

					//Only output adjacent duplicate exceptions once
					string NewOutputException=Catcher.GetOutputException("RicherLabel Important Shader", e);
					if(NewOutputException!=LastOutputException)
						Log.Error(NewOutputException);
					LastOutputException=NewOutputException;
				}

			//Handle render when the shader fails
			L.NormalColor=L.HoverColor=Mathf.FloorToInt(Time.realtimeSinceStartup)%2==1 ? Orange : Color.red;
		}

		public void Dispose()
		{
			RenderFrom?.TDestroy();
			RenderTo?.Release();
			RenderEnd?.TDestroy();
		}
	}
}