using SilkDev;
using SilkDev.Textures;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace PharloomAtlas;

public partial class SideBar
{
	//Draw the information for the hovered and last selected items
	private readonly ItemInfoSection IIS;
	private class ItemInfoSection(SideBar SB)
	{
		private const string CachedImagesDir="CachedImages";
		private readonly SideBar SB=SB;
		private float ClientWidth;
		private Texture2D FakeTex=null!;
		private readonly GUIStyle ItemInfoBoxStyle=new(GUI.skin.label) { fontSize=16, normal={textColor=Color.white}, richText=true };
		private readonly Dictionary<string, Texture2D?> LoadedImages=[];
		internal void Draw(int ClientWidth)
		{
			//Make sure FakeTex has been created
			FakeTex??=new Texture2D(1, 1);

			//Add a margin
			const int InfoSectionHorPadding=20;
			GUILayout.BeginHorizontal();
			GUILayout.Space(InfoSectionHorPadding);
			GUILayout.BeginVertical(GUILayout.Width(SB.Width-InfoSectionHorPadding*2));

			//Basic title
			ItemInfoBoxStyle.alignment=TextAnchor.MiddleCenter;
			GUILayout.Label("<b><size=17>Select an icon on the map to see its data here</size></b>", ItemInfoBoxStyle);
			ItemInfoBoxStyle.alignment=TextAnchor.UpperLeft;

			//Add hover item information
			if(MapControl.Self.HoverItem is Item HI)
				GUILayout.Label(MakeItemInfoLine("Currently Over", $"{HI.Title} [#{HI.ID}]"), ItemInfoBoxStyle);

			//Render the selected item
			this.ClientWidth=ClientWidth-InfoSectionHorPadding*2;
			if(MapControl.Self.SelectedItem!=null)
				RenderSelectedItem(MapControl.Self.SelectedItem);

			//End the margin
			GUILayout.EndVertical();
			GUILayout.EndHorizontal();
			GUILayout.Space(10);
		}

		//Render the currently selected item
		private void RenderSelectedItem(Item SelectedItem)
		{
			static void DrawInfoSectionLine(Rect TitleRect) =>
				GUI.DrawTexture(
					TitleRect.AddY(TitleRect.height).SetHeight(1),
					Texture2D.whiteTexture, ScaleMode.StretchToFill
				);

			//Draw the header and horizontal border line
			GUILayout.Space(5);
			ItemInfoBoxStyle.alignment=TextAnchor.MiddleCenter;
			GUILayout.Label("<b><size=+2>Selected Item Info</size></b>", ItemInfoBoxStyle);
			ItemInfoBoxStyle.alignment=TextAnchor.UpperLeft;
			DrawInfoSectionLine(GUILayoutUtility.GetLastRect());

			//Get list of text and images
			List<Texture2D> Images=[];
			List<string> Lines=[
				MakeItemInfoLine("Title", SelectedItem.Title),
				MakeItemInfoLine("Category", MapControl.Self.DS.Categories[SelectedItem.CategoryID].Title),
				SelectedItem.Description==null ? Misc.Empty : MakeItemInfoLine("Description", SelectedItem.Description),
				SelectedItem.IgnPageName==null ? Misc.Empty : MakeItemInfoLine("IGN Page", "https://www.ign.com/wikis/hollow-knight-silksong/"+SelectedItem.IgnPageName),
			];
			if(Config.C.ShowSideBarPictures)
				SelectedItem.ImageURLs?.ForEach(ImageURL => {
					(string? FailureString, Texture2D? Image)=RenderImage(ImageURL);
					if(FailureString!=null)
						Lines.Add(FailureString);
					else if(Image!=null)
						Images.Add(Image!);
				});

			//Render text, images, and a horizontal border line
			GUILayout.Label(string.Join(Misc.NewLine, Lines.Where(static I => I!=Misc.Empty)), ItemInfoBoxStyle);
			Images.ForEach(Tex => {
				float WidthRatio=ClientWidth/Tex.width;
				GUI.DrawTexture(GUILayoutUtility.GetRect(Tex.width*WidthRatio, Tex.height*WidthRatio), Tex);
			});
			DrawInfoSectionLine(GUILayoutUtility.GetLastRect());
		}

		//Return the image to render or a text string explaining its state
		private (string?, Texture2D?) RenderImage(string ImageURL)
		{
			//See if the picture is already loaded
			string ImageFileName=FileOps.GetFileName(ImageURL);
			if(LoadedImages.TryGetValue(ImageFileName, out Texture2D? Image))
				return
				  Image==null ? ("[Downloading image]", null)
				: Image==FakeTex ? ("[Image download failed]", null)
				: (null, Image);

			//If the cache path failed, stop here
			if(CachePath==null)
				return ("Could not download image: Cache path failed", null);

			//If the picture does not exist locally, try to download it
			string FilePath=FileOps.PathCombine(CachePath, ImageFileName);
			if(!FileOps.FileExists(FilePath)) {
				LoadedImages[ImageFileName]=null;
				_=Catcher.ExecCoroutine("DownloadFileAsync", DownloadFileAsync(ImageURL, FilePath));
				return ("[Downloading image]", null);
			}

			//Get the picture locally
			LoadedImages[ImageFileName]=null; //Mark as acquiring
			try {
				Texture2D NewTexture=new(1, 1);
				if(!NewTexture.LoadImage(FileOps.ReadFileBytes(FilePath)))
					throw new Exception("Cached load failed");
				LoadedImages[ImageFileName]=NewTexture;
				return (null, NewTexture);
			} catch(Exception e) {
				LoadedImages[ImageFileName]=FakeTex;
				return ($"Image load failed: {e.Message}", null);
			}
		}

		private IEnumerator DownloadFileAsync(string Url, string SavePath)
		{
			//Execute the download
			using UnityWebRequest Request=UnityWebRequest.Get(Url);
			yield return Request.SendWebRequest();
			if(Request.result!=UnityWebRequest.Result.Success) {
				Log.Error($"Download failed for {FileOps.GetFileName(Url)}: {Request.error}");
				LoadedImages[FileOps.GetFileName(SavePath)]=FakeTex;
				yield break;
			}

			//Process the download
			byte[] FileBytes=Request.downloadHandler.data;
			OnNextFrame(() => {
				string? RetResult=ConvertAndSaveImage(FileBytes, SavePath);
				if(RetResult!=null) {
					Log.Error(RetResult);
					LoadedImages[FileOps.GetFileName(SavePath)]=FakeTex;
				}
			});
		}

		private string? ConvertAndSaveImage(byte[] FileBytes, string SavePath)
		{
			//Load the image into a temporary texture
			string ImageFileName=FileOps.GetFileName(SavePath);
			using TypedDisposer<Texture2D> NewTex=new(
				new(2, 2, TextureFormat.ARGB32, false),
				Target => Target.TDestroy()
			);
			if(!NewTex.Target.LoadImage(FileBytes))
				return $"Load image for {ImageFileName} failed";

			//Resize the image and get its jpeg bytes
			float WidthRatio=ClientWidth/NewTex.Target.width;
			int StartWidth=NewTex.Target.width, StartHeight=NewTex.Target.height;
			Vector2 NewSize=new Vector2(StartWidth, StartHeight)*WidthRatio;
			byte[] OutBytes;
			try {
				using TypedDisposer<Texture2D> ResizedTex=new(
					NewTex.Target.ToReadable(ResizeDimensions: NewSize),
					Target => Target.TDestroy()
				);
				OutBytes=ResizedTex.Target.EncodeToJPG(80);
			} catch(Exception e) {
				return $"Error resizing {ImageFileName}: {e.Message}";
			}

			//Write the file and clear its downloading state
			Log.Info($"Saved [{StartWidth}x{StartHeight}]→[{(int)NewSize.x}x{(int)NewSize.y}]: {ImageFileName}");
			try {
				FileOps.WriteFile(SavePath, OutBytes);
				_=LoadedImages.Remove(ImageFileName);
			} catch(Exception e) {
				Log.Error($"Failed writing file. Did you delete the {CachedImagesDir} folder? :-p :: {e.Message}");
				LoadedImages[ImageFileName]=FakeTex;
			}
			return null;
		}

		//When moving to a new item, release the images from the old item
		internal void ReleaseTextures()
		{
			LoadedImages.Values.ForEach(Tex =>
				Misc.IFF(Tex!=FakeTex, () => Tex.NullSafe()?.TDestroy())
			);
			LoadedImages.Clear();
		}

		//Make sure the cache path has been created
		private static readonly string? CachePath=null;
		static ItemInfoSection()
		{
			string MyPath=FileOps.PathCombine(Misc.GetPluginPath, CachedImagesDir);
			try {
				if(!FileOps.DirectoryExists(MyPath))
					_=FileOps.CreateDirectory(MyPath);
			} catch(Exception e) {
				Log.Error($"Could not create image cache directory: {e.Message}");
				return;
			}
			CachePath=MyPath;
		}

		private static string MakeItemInfoLine(string Title, string Info) =>
			$"<size=-1>{Title}</size>: <b>{Misc.SanitizeRichString(Info)}</b>";
	}
}