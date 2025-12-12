using SilkDev;
using SilkDev.Textures;
using System;
using System.Text.RegularExpressions;
using UnityEngine;
using static SilkDev.DevInput.Joystick;

namespace PharloomAtlas;

public partial class SideBar
{
	//Displays a help message
	private class HelpWindow : SilkDev.Windows.PopupMessage
	{
		private static int NumOpen=0;
		private static Texture2D? ControllerLayout;
		private static string FullMessage=Misc.Empty;
		private const string ControllerFileName="KeyMappings.png";
		private const string HelpTextFile="Help.txt";
		private const string HelpTextTranslateKey="HelpText";
		private Vector2 ScrollPosition=Vector2.zero;
		public static bool HasAnyOpen => NumOpen>0;
		public GUIStyle ScrollStyle=new(GUI.skin.scrollView) { normal={background=Texture2D.grayTexture}, margin=new RectOffset(2, 2, 0, 0) };
		public GUIStyle LabelStyle=new(GUI.skin.label) { fontSize=20, alignment=TextAnchor.UpperLeft, wordWrap=true, richText=true };
		public GUIStyle CenterRichText=new(GUI.skin.label) { alignment=TextAnchor.MiddleCenter, richText=true };

		private const int TitleFontSize=45, SubTitleFontSize=30;

		private static readonly System.Collections.Generic.List<(Regex, string)> Tags=[];
		private static void UpdateTags() { Tags.Clear(); Tags.AddRange([
			(new Regex(@"<title>(.*)</title>"				), $"<size=@{TitleFontSize}><u>$1</u></size>"),
			(new Regex(@"<subtitle>(.*)</subtitle>"			), $"<color=#999999><size={SubTitleFontSize}><u>$1</u></size></color>"),
			(new Regex(@"^([*\t])", RegexOptions.Multiline	), "  $1"),
			(new Regex(@"\t"								), "    "),
			(new Regex(@"(\*.*?)<configs>(.*?)</configs>"	), $"<size=15><color=grey>$1 {TSan("Config options")}: $2</color></size>"),
			(new Regex(@"<config>(.*?)</config>"			), $"<size=15><color=grey>({TSan("Config option")}: “<i>$1</i>”)</color></size>"),
			(new Regex(@"<conf>(.*?)</conf>"				), "“<i>$1</i>”"),
		]); }
		static HelpWindow()
		{
			UpdateTags();
			Config.C.Tr.LanguageChanged += UpdateTags;
		}

		public HelpWindow() : base(FullMessage)
		{
			//Do not setup when multiple windows open
			NumOpen++;
			if(NumOpen!=1)
				return;

			//Build the message
			try {
				Message=TrT(HelpTextTranslateKey);
				if(Message==HelpTextTranslateKey)
					Message=FileOps.LoadEmbeddedResource(HelpTextFile).ReadAllAndCloseS();
			} catch(Exception e) {
				Message=$"<color=red><size=30>{TSan("Could not load help file: {0}", e.Message)}</size></color>";
			}
			foreach((Regex Replacer, string ReplaceWith) in Tags)
				Message=Replacer.Replace(Message, ReplaceWith);
			Message=Message.Replace("<ABYSS>", !PlayerData.instance.visitedAbyss ? "@#$%@" : TSan("Abyss"));

			//Load the texture
			ControllerLayout=new Texture2D(2, 2, TextureFormat.ARGB32, false);
			try {
				if(!ControllerLayout.LoadImage(FileOps.LoadEmbeddedResource(ControllerFileName).ReadAllAndCloseB()))
					throw new Exception("Unity failed loading the image");
			} catch(Exception e) {
				ControllerLayout.TDestroy();
				ControllerLayout=null;
				string ErrMsg="Couldn’t load controller layout image: {0}";
				Log.Error(string.Format(ErrMsg, e.Message));
				Message=$"{Misc.NewLine}<color=red><size=30>{TSan(ErrMsg, e.Message)}</size></color>{Misc.NewLine}{Misc.NewLine}{Message}";
			}

			//Store the full message for duplicate windows
			FullMessage=Message;
		}

		protected override void DrawContents(Vector2 AreaSize)
		{
			//Draw the controller (if we have it)
			if(ControllerLayout!=null) {
				const int ImageBottomSpacing=5;
				GUILayout.BeginHorizontal();
				GUILayout.FlexibleSpace();
				GUILayout.Label(ControllerLayout, GUILayout.Width(ControllerLayout.width), GUILayout.Height(ControllerLayout.height));
				GUILayout.FlexibleSpace();
				GUILayout.EndHorizontal();
				GUILayout.Space(ImageBottomSpacing);
			}

			//Draw the press any key text
			GUILayout.Label(PressAnyKeyString, CenterRichText);

			//Draw the message
			ScrollPosition=GUILayout.BeginScrollView(ScrollPosition, ScrollStyle, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
			GUILayout.Label($"<color=#FF6C9C><size=20>{TSan("Scroll this window with Sidebar Scroll Up/Down")}</size></color>", CenterRichText);
			GUILayout.Label(Message, LabelStyle);
			GUILayout.EndScrollView();

			GUILayout.Space(2);
		}

		//Scroll up/down the help info
		private const float MoreOftenMultiplier=4;
		private readonly SilkDev.DevInput.InputRepeatDelay<int> WinScrollCheck=new(.075f/MoreOftenMultiplier,
			(Config.C		.Shortcut_SB_ScrollUp	, -1),
			(Config.C		.Shortcut_SB_ScrollDown	,  1),
			(false,Direction.Left					, -1),
			(false,Direction.Right					,  1)
		);
		protected override void OnUpdate() =>
			ScrollPosition.y=WinScrollCheck.IsReadyValueVType is int ScrollAmount ? //If ready to scroll
				  Mathf.Max(ScrollPosition.y+ScrollAmount*50/MoreOftenMultiplier, 0) //Do the scrolling
				: ScrollPosition.y;

		//Destroy the texture when there is only 1 copy of the window left
		protected override void OnClosed()
		{
			base.OnClosed();
			if(--NumOpen>0)
				return;
			ControllerLayout?.TDestroy();
			ControllerLayout=null;
		}

		//Do not close from scroll buttons
		protected override bool BlockClose =>
			   Config.C.Shortcut_SB_ScrollUp.IsPressed()
			|| Config.C.Shortcut_SB_ScrollDown.IsPressed()
			|| ActiveDevice.RightStick.HasChanged;

		//Translation
		private static string TSan(string Message, params object[] FormatList) => Config.C.Tr.TDef(Message, nameof(HelpWindow), Message, true , FormatList);
		private static string TrT (string Message, params object[] FormatList) => Config.C.Tr.TDef(Message, nameof(HelpWindow), Message, false, FormatList);
	}
}