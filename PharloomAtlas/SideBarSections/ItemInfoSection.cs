using SilkDev;
using SilkDev.DevInput.Mouse;
using SilkDev.Textures;
using SilkDev.Windows;
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
	private class ItemInfoSection : SideBarSection
	{
		private const string CachedImagesDir="CachedImages";
		private const float SaveImageWidth=480;
		private const int InfoSectionHorPadding=20;
		private float ClientWidth;
		private Texture2D FakeTex=null!;
		private readonly GUIStyle ItemInfoBoxStyle=new(GUI.skin.label) { fontSize=16, normal={textColor=Color.white}, richText=true };
		private readonly Dictionary<string, Texture2D?> LoadedImages=[];
		protected override void ExecDraw(int ClientWidth)
		{
			//Make sure FakeTex has been created
			FakeTex??=new Texture2D(1, 1);

			//Add a margin
			GUILayout.BeginHorizontal();
			GUILayout.Space(InfoSectionHorPadding);
			GUILayout.BeginVertical(GUILayout.Width(SB.Width-InfoSectionHorPadding*2));

			//Basic title
			ItemInfoBoxStyle.alignment=TextAnchor.MiddleCenter;
			GUILayout.Label($"<b><size=17>{TSan("Select an icon on the map to see its data here")}</size></b>", ItemInfoBoxStyle);
			ItemInfoBoxStyle.alignment=TextAnchor.UpperLeft;

			//Add hover item information
			if(MapControl.Self.HoverItem is Item HI)
				GUILayout.Label(MakeItemInfoLine("Currently Over", $"{HI.Title} [#{HI.ID}]"), ItemInfoBoxStyle);

			//Render the selected item
			this.ClientWidth=ClientWidth-InfoSectionHorPadding*2;
			if(LastSelectedItem!=MapControl.Self.SelectedItem)
				UpdateClickableLink(MapControl.Self.SelectedItem);
			if(MapControl.Self.SelectedItem!=null)
				RenderSelectedItem(MapControl.Self.SelectedItem);

			//End the margin
			GUILayout.EndVertical();
			GUILayout.EndHorizontal();
			GUILayout.Space(10);
		}

		//Handle the clickable label
		private ClickableLabel? CLabel;
		private Item? LastSelectedItem;
		public ItemInfoSection(string Name, SideBar SB) : base(Name, SB)
		{
			Config.C.Color_Link.SettingChanged += (_, _) => CLabel?.LinkColor=Config.C.Color_Link;
			Config.C.Color_LinkHover.SettingChanged += (_, _) => CLabel?.HoverColor=Config.C.Color_LinkHover;
		}
		private void UpdateClickableLink(Item? CurrentSelectedItem)
		{
			LastSelectedItem=CurrentSelectedItem;
			CLabel=CurrentSelectedItem==null ? null : new ClickableLabel() {
				LinkColor=Config.C.Color_Link,
				HoverColor=Config.C.Color_LinkHover
			};
			SelectedItem=-1; //Selection state is unknown until the next frame when we know if the ClickableLabel has any links
			if(!IsSectionSelected)
				return;
			if(CLabel==null)
				NextSection.MoveTo(MoveToType.FirstRow|MoveToType.NoCol);
			else
				OnNextFrame(() => Misc.IFF(
					CLabel.ActiveLinks.Length==(SelectedItem=0),
					() => NextSection.MoveTo(MoveToType.FirstRow|MoveToType.NoCol)
				), false);
		}

		//Render the currently selected item
		private void RenderSelectedItem(Item CurSelectedItem)
		{
			static void DrawInfoSectionLine(Rect TitleRect) =>
				GUI.DrawTexture(
					TitleRect.AddY(TitleRect.height).SetHeight(1),
					Texture2D.whiteTexture, ScaleMode.StretchToFill
				);

			//Draw the header and horizontal border line
			GUILayout.Space(5);
			ItemInfoBoxStyle.alignment=TextAnchor.MiddleCenter;
			GUILayout.Label($"<b><size=+2>{TSan("Selected Item Info")}</size></b>", ItemInfoBoxStyle);
			ItemInfoBoxStyle.alignment=TextAnchor.UpperLeft;
			DrawInfoSectionLine(GUILayoutUtility.GetLastRect());

			//Get list of text and images
			List<Texture2D> Images=[];
			List<string> Lines=[
				MakeItemInfoLine("Title", CurSelectedItem.Title),
				MakeItemInfoLine("Category", MapControl.Self.DS.Categories[CurSelectedItem.CategoryID].Title),
				CurSelectedItem.Description,
				CurSelectedItem.IgnPageName==null ? Misc.Empty : MakeItemInfoLine("IGN Page", "https://www.ign.com/wikis/hollow-knight-silksong/"+CurSelectedItem.IgnPageName),
			];
			if(Config.C.ShowSideBarPictures)
				CurSelectedItem.ImageURLs?.ForEach(ImageURL => {
					(string? FailureString, Texture2D? Image)=RenderImage(ImageURL);
					if(FailureString!=null)
						Lines.Add(TSan(FailureString));
					else if(Image!=null)
						Images.Add(Image!);
				});

			//Render text, images, and a horizontal border line
			ClickableLabel.Link? L=CLabel!.GUILabelLayout(
				string.Join(Misc.NewLine, Lines.Where(static I => I!=Misc.Empty)),
				ItemInfoBoxStyle,
				IsSectionSelected && SelectedItem!=-1 ? [CLabel.ActiveLinks[SelectedItem]] : []
			);
			if(L!=null && Event.current.type==EventType.MouseDown && Button.CurrentButton==Button.Enum.Left)
				MapControl.Self.DS.LinkSelected(L.Attributes.Get("ItemID") ?? "1");
			Images.ForEach(Tex => {
				float RealClientWidth=ClientWidth+InfoSectionHorPadding;
				float DrawWidth=Math.Min(RealClientWidth, SaveImageWidth);
				float WidthRatio=DrawWidth/Tex.width;
				GUI.DrawTexture(
					GUILayoutUtility.GetRect(Tex.width*WidthRatio, Tex.height*WidthRatio)
						.SetWidth(DrawWidth)
						.AddX((RealClientWidth-DrawWidth)/2),
					Tex
				);
			});
			DrawInfoSectionLine(GUILayoutUtility.GetLastRect());
		}

		//Return the image to render or a text string explaining its state
		private (string?, Texture2D?) RenderImage(string ImageURL)
		{
			//If URL starts with an exclamation mark then prepend the const URL path
			if(ImageURL.Length>0 && ImageURL[0]=='!')
				ImageURL="https://media.mapgenie.io/storage/media/"+ImageURL[1..];

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
			float WidthRatio=SaveImageWidth/NewTex.Target.width;
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
				Misc.IFF(Tex!=FakeTex, () => Tex!.NullSafe?.TDestroy())
			);
			LoadedImages.Clear();
		}

		//Make sure the cache path has been created
		private static readonly string? CachePath=null;
		static ItemInfoSection()
		{
			string MyPath=FileOps.PathCombine(FileOps.GetPluginPath, CachedImagesDir);
			try {
				if(!FileOps.DirectoryExists(MyPath))
					_=FileOps.CreateDirectory(MyPath);
			} catch(Exception e) {
				Log.Error($"Could not create image cache directory: {e.Message}");
				return;
			}
			CachePath=MyPath;
		}

		//Translations
		private static string MakeItemInfoLine(string Title, string Info) =>
			$"<size=-1>{Tr.T(Title, "ItemFields", true)}</size>: <b>{Misc.SanitizeRichString(Info)}</b>";
		private static string TSan(string Message) => Tr.T(Message, nameof(ItemInfoSection), true);
		private static readonly Translations Tr=Config.C.Tr;

		//SideBarSection actions
		public override void MoveHor(bool IsNeg)
		{
			SelectedItem+=(IsNeg ? -1 : 1); //Select next/previous button
			if(SelectedItem>=(CLabel?.ActiveLinks.Length ?? 0)) //Last button moves to next title
				NextSection.MoveTo(MoveToType.FirstRow|MoveToType.NoCol);
			else if(SelectedItem<0) //First button moves to last group last item in column 1
				PrevSection.MoveTo(MoveToType.LastRow|MoveToType.NoCol);
		}
		public override void MoveVer(bool IsNeg) =>
			(IsNeg ? PrevSection : NextSection).MoveTo(IsNeg ? MoveToType.LastRow : MoveToType.FirstRow);
		public override void MoveTo(MoveToType M) => OnNextFrame(() =>
		{
			int NumLinks=CLabel?.ActiveLinks.Length ?? 0;
			if(NumLinks==0)
				(M.HasFlag(MoveToType.LastRow) ? PrevSection : NextSection).MoveTo(M); //Pass through section if empty
			else
				MovedTo(M.HasFlag(MoveToType.LastRow) ? NumLinks-1 : 0);
		});
		public override void ExecSelected() =>
			MapControl.Self.DS.LinkSelected(CLabel?.ActiveLinks.ElementAtOrDefault(SelectedItem)?.Attributes.Get("ItemID") ?? "1");
	}
}