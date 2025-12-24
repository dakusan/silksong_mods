using SilkDev;
using SilkDev.Configs;
using SilkDev.Textures;
using System;
using System.Text.RegularExpressions;
using UnityEngine;
using static SilkDev.DevInput.Joystick;

namespace PharloomAtlas;

public partial class SideBar
{
	//Displays a help message
	internal class HelpWindow : SilkDev.Windows.PopupMessage
	{
		private static int NumOpen=0;
		private static Texture2D? ControllerLayout;
		private static string FullMessage=Misc.Empty;
		private const string ControllerFileName="KeyMappings.png";
		private const string HelpTextFile="Help.txt";
		private const string HelpTextTranslateKey="HelpText";
		private static readonly string SettingTranslationSection=TranslatedConfig.SettingTranslationSections.Names.TranslationName();
		private Vector2 ScrollPosition=Vector2.zero;
		public static bool HasAnyOpen => NumOpen>0;
		public GUIStyle ScrollStyle=new(GUI.skin.scrollView) { normal={background=Texture2D.grayTexture}, margin=new RectOffset(2, 2, 0, 0) };
		public GUIStyle LabelStyle=new(GUI.skin.label) { fontSize=20, alignment=TextAnchor.UpperLeft, wordWrap=true, richText=true };
		public GUIStyle CenterRichText=new(GUI.skin.label) { alignment=TextAnchor.MiddleCenter, richText=true };

		private const int TitleFontSize=45, SubTitleFontSize=30;

		private static string GetQuotedConf => $"{Tr!.T("“", RichSanitize:true)}<i>$1</i>{Tr.T("”", RichSanitize:true)}";
		private static readonly (Regex, Func<(bool, string)>)[] Tags=[
			(new Regex(@"<title>(.*)</title>"				), () => (false, $"<size=@{TitleFontSize}><u>$1</u></size>")),
			(new Regex(@"<subtitle>(.*)</subtitle>"			), () => (false, $"<color=#999999><size={SubTitleFontSize}><u>$1</u></size></color>")),
			(new Regex(@"^([*\t])", RegexOptions.Multiline	), () => (false, "  $1")),
			(new Regex(@"\t"								), () => (false, "    ")),
			(new Regex(@"(\*.*?)<configs>(.*?)</configs>"	), () => (false, $"<size=15><color=grey>$1 {TSan("Config options")}: $2</color></size>")),
			(new Regex(@"<config>(.*?)</config>"			), () => (true , $"<size=15><color=grey>({TSan("Config option")}: {GetQuotedConf})</color></size>")),
			(new Regex(@"<conf>(.*?)</conf>"				), () => (true , GetQuotedConf)),
		];

		public HelpWindow() : base(FullMessage)
		{
			//Do not setup when multiple windows open
			NumOpen++;
			if(NumOpen!=1)
				return;

			//Load the help text
			try {
				Message=TrT(HelpTextTranslateKey);
				if(Message==HelpTextTranslateKey)
					Message=FileOps.LoadEmbeddedResource(HelpTextFile).ReadAllAndCloseS();
			} catch(Exception e) {
				Message=$"<color=red><size=30>{TSan("Could not load help file: {0}", e.Message)}</size></color>";
			}

			//Transform html tags
			bool IsDefaultLang=(Tr.Language==Tr.DefaultLang);
			static string GetSettingNameTranslation(string SettingName) =>
				  (Tr.T(SettingName, SettingTranslationSection) is string TransStr) && TransStr!=SettingName ? TransStr //Normal translation worked
				: Tr.TDef("Sidebar "+char.ToLower(SettingName[0])+SettingName[1..], SettingTranslationSection, SettingName); //See if it’s a sidebar translation (which were named slightly differently)
			static string GetUpdatedMessage((bool NeedsTranslate, string ReplaceWith) v, string Message, Regex Replacer, bool IsDefaultLang) =>
				  !v.NeedsTranslate || IsDefaultLang ? Replacer.Replace(Message, v.ReplaceWith) //Handle normal replaces
				  //These replacements need translation per replacement. Fortunately, the translation generator left all the config names unchanged, except in Tamil, which, oh well.
				: Replacer.Replace(Message, Match => v.ReplaceWith.Replace("$1", GetSettingNameTranslation(Match.Groups[1].Value)));
			foreach((Regex Replacer, Func<(bool, string)> RepFunc) in Tags)
				Message=GetUpdatedMessage(RepFunc(), Message, Replacer, IsDefaultLang);
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
			GUILayout.Label($"<color=#FF6C9C><size=20>{TSan("Scroll this window with selected-icon-stack next/previous")}</size></color>", CenterRichText);
			GUILayout.Label(Message, LabelStyle);
			GUILayout.EndScrollView();

			GUILayout.Space(2);
		}

		//Scroll up/down the help info
		private const float MoreOftenMultiplier=4;
		private readonly SilkDev.DevInput.InputRepeatDelay<int> WinScrollCheck=new(.075f/MoreOftenMultiplier,
			(Config.C		.Shortcut_SelStack_Next , -1),
			(Config.C		.Shortcut_SelStack_Prev ,  1),
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
			   Config.C.Shortcut_SelStack_Next.IsPressed()
			|| Config.C.Shortcut_SelStack_Prev.IsPressed()
			|| ActiveDevice.RightStick.HasChanged;

		//Translation
		private static string TSan(string Message, params object[] FormatList) => Tr.TDef(Message, nameof(HelpWindow), Message, true , FormatList);
		private static string TrT (string Message, params object[] FormatList) => Tr.TDef(Message, nameof(HelpWindow), Message, false, FormatList);
		private static readonly Translations Tr=Config.C.Tr;
	}
}