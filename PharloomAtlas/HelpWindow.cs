using SilkDev;
using SilkDev.Configs;
using SilkDev.Textures;
using System;
using System.Text.RegularExpressions;
using UnityEngine;
using static SilkDev.DevInput.Joystick;

#if DEBUG
	using SafeTexture2D = SilkDev.Textures.SafeTexture2D;
#else
	using SafeTexture2D = UnityEngine.Texture2D;
#endif
using RTexture2D = UnityEngine.Texture2D;

namespace PharloomAtlas;

public partial class SideBar
{
	//Displays a help message
	internal class HelpWindow : SilkDev.Windows.PopupMessage
	{
		private static int NumOpen=0;
		private static SafeTexture2D? ControllerLayout;
		private static string FullMessage=Misc.Empty;
		private const string ControllerFileName="KeyMappings.png";
		private const string HelpTextFile="Help.txt";
		private const string HelpTextTranslateKey="HelpText";
		private static readonly string SettingTranslationSection=TranslatedConfig.SettingTranslationSections.Names.TranslationName();
		private Vector2 ScrollPosition=Vector2.zero;
		public static bool HasAnyOpen => NumOpen>0;
		public GUIStyle ScrollStyle=new(GUI.skin.scrollView) { normal={background=RTexture2D.grayTexture}, margin=new RectOffset(2, 2, 0, 0) };
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
			ControllerLayout=SafeTexture2D.New();
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

		/*!=double height, ~=double width.
		The following uses translation strings to populate Assets/Embed/KeyMappings.png. All strings support rich text.
		They may begin with an optional prefix that controls layout and rendering behavior. The prefix consists of ordered flags, all optional unless noted.
		Prefix Order (must be preserved): [Alignment][!FontSize][!LineHeight][!MarginTop]
		“#” denotes a 1–2 digit number. See ja.tr.json as an example.
			1) [“Button_” only]			Number 1-9	: String alignment.	1=Upper left; 5=Middle center (default); 9=Lower right
			2)							!#			: Font Size.		If omitted, inherits the previous line’s font size. First line=GUI.skin.label.fontSize.
			3) [Required if 2) exists]	!#			: Line height.		If given, uses special rendering (see below). Otherwise, the string renders normally.
			4)							!#			: Margin top.		Additional top margin in pixels. Default=0
		Special rendering (if FontSize+LineHeight is specified):
			The string is always rendered from the top and ignores the style’s vertical alignment.
			Line breaks are calculated manually and each line is rendered individually with +LineHeight applied.
		*/
		private static readonly string?[] NumPadButtons=[
			null, "/" , "*", "-",
			"7"	, "8" , "9", "!+",
			"4"	, "5" , "6", null,
			"1"	, "2" , "3", "!Enter",
			"~0", null, ".", null,
		];
		private static readonly Regex GetFontHeights=new(@"^!(\d\d?)!(\d\d?)(?:!(\d\d?))?"), GetSizeBlock=new(@"^<size\s*=\s*\d+>.*?</size>");

		protected override void DrawContents(Vector2 AreaSize)
		{
			//Draw the controller (if we have it)
			if(ControllerLayout!=null) {
				//Center the controller
				const int ImageBottomSpacing=5;
				GUILayout.BeginHorizontal();
				GUILayout.FlexibleSpace();
				GUILayout.Label(ControllerLayout, GUILayout.Width(ControllerLayout.width), GUILayout.Height(ControllerLayout.height));
				Vector2 StartPos=GUILayoutUtility.GetLastRect().position;
				GUILayout.FlexibleSpace();
				GUILayout.EndHorizontal();
				GUILayout.Space(ImageBottomSpacing);

				//Draw the numpad labels
				const int NPButtonX=611, NPButtonWidth =70, NPButtonPaddingX=11;
				const int NPButtonY=3  , NPButtonHeight=79, NPButtonPaddingY=1 ;
				int StartX=(int)StartPos.x+NPButtonX, StartY=(int)StartPos.y+NPButtonY, PrevFontSize=CenterRichText.fontSize;
				Color PrevTextColor=CenterRichText.normal.textColor;
				CenterRichText.normal.textColor=Color.black;
				foreach((int Index, string? NPName) in NumPadButtons.Entries) {
					if(NPName==null)
						continue;

					//Get the translation string
					string TString=Tr.TDef("Button_"+(NPName[0] is '~' or '!' ? NPName[1..] : NPName), nameof(HelpWindow), Misc.Empty);
					if(TString==Misc.Empty)
						continue;

					//Change the alignment if requested for the translation string
					bool HasAlignNum=(TString[0] is >='1' and <='9');
					CenterRichText.alignment=HasAlignNum ? TextAnchor.UpperLeft+(TString[0]-'1') : TextAnchor.MiddleCenter;
					if(HasAlignNum)
						TString=TString[1..];

					//Determine the rect to draw the label in
					Rect DrawRect=new(
						StartX+Index%4*(NPButtonWidth +NPButtonPaddingX),
						StartY+Index/4*(NPButtonHeight+NPButtonPaddingY),
						NPButtonWidth *(NPName[0]=='~' ? 2 : 1),
						NPButtonHeight*(NPName[0]=='!' ? 2 : 1)
					);

					DrawHelpString(DrawRect, TString, CenterRichText);
				}

				//Draw the other 3 labels
				void DrawLabel(int x, int y, int Width, int Height, TextAnchor A, Color c, string Key)
				{
					CenterRichText.alignment=A;
					CenterRichText.normal.textColor=c;
					DrawHelpString(new Rect(StartPos.x+x, StartPos.y+y, Width, Height), Tr.TDef(Key, nameof(HelpWindow), ""), CenterRichText);
				}
				DrawLabel(480, 0  , 125, 89, TextAnchor.UpperRight , Color.cyan , "NumPadReqs"		);
				DrawLabel(152, 0  , 287, 82, TextAnchor.UpperCenter, Color.white, "WhenButtonsWork"	);
				DrawLabel(104, 360, 378, 41, TextAnchor.UpperLeft  , Color.white, "AsteriskDefines"	);

				//Restore the GUIStyle
				CenterRichText.alignment=TextAnchor.MiddleCenter;
				CenterRichText.normal.textColor=PrevTextColor;
				CenterRichText.fontSize=PrevFontSize;
			}

			//Draw the press any key text
			GUILayout.Label(PressAnyKeyString, CenterRichText);

			//Draw the message
			ScrollPosition=GUILayout.BeginScrollView(ScrollPosition, ScrollStyle, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
			GUILayout.Label($"<color=#FF6C9C><size=20>{TSan("Scroll this window with next/previous selected item buttons")}</size></color>", CenterRichText);
			GUILayout.Label(Message, LabelStyle);
			GUILayout.EndScrollView();

			GUILayout.Space(2);
		}

		public void DrawHelpString(Rect Rect, string Str, GUIStyle Style)
		{
			//If no regex match, just draw the line normally
			Match FontSizeMatch=GetFontHeights.Match(Str);
			if(!FontSizeMatch.Success) {
				GUI.Label(Rect, Str, Style);
				return;
			}

			//Make sure the string isn’t empty after the regex
			Str=Str[FontSizeMatch.Length..];
			if(string.IsNullOrEmpty(Str))
				return;

			//Get the custom spacing values
			int FontSize=int.Parse(FontSizeMatch.Groups[1].Value);
			int LineHeight=int.Parse(FontSizeMatch.Groups[2].Value);
			int MarginTop=FontSizeMatch.Groups[3].Value==Misc.Empty ? 0 : int.Parse(FontSizeMatch.Groups[3].Value);

			//Get the lines by adding 1 character (or size block) at a time until a line overflows
			System.Collections.Generic.List<string> Lines=[];
			string CurrentLine=Misc.Empty, CurBlock, TestStr=Misc.Empty;
			Match SizeBlock;
			bool Overflow=false;
			Style.fontSize=FontSize;
			for(int i=0; i<Str.Length; i+=CurBlock.Length) {
				CurBlock=(Str[i]=='<' && (SizeBlock=GetSizeBlock.Match(Str[i..])).Success ? SizeBlock.Value : Str[i].ToString());
				if(CurBlock=="\n" || (Overflow=Style.CalcSize(new GUIContent(TestStr=CurrentLine+CurBlock)).x>Rect.width && CurrentLine.Length>0))
					Lines.Add(CurrentLine);
				CurrentLine=(CurBlock=="\n" ? Misc.Empty : Overflow ? CurBlock : TestStr);
			}
			if(CurrentLine.Length>0)
				Lines.Add(CurrentLine);

			//Render the lines
			float y=Rect.y+MarginTop;
			foreach(string Line in Lines) {
				GUI.Label(new Rect(Rect.x, y, Rect.width, FontSize*2), Line, Style);
				y+=LineHeight;
			}
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