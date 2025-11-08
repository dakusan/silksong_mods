using SilkDev;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using static SilkDev.DevInput.Joystick;

namespace PharloomAtlas;

public partial class SideBar
{
	//Displays a help message
	private class HelpWindow : PopupMessage
	{
		private static int NumOpen=0;
		private static Texture2D? ControllerLayout;
		private static string FullMessage=Misc.Empty;
		private static DateTime LastScrollKeyTime=DateTime.MinValue;
		private const string ControllerFileName="KeyMappings.png";
		private const string HelpTextFile="Help.txt";
		private Vector2 ScrollPosition=Vector2.zero;
		public static bool HasAnyOpen => NumOpen>0;
		public GUIStyle ScrollStyle=new(GUI.skin.scrollView) { normal={background=Texture2D.grayTexture}, margin=new RectOffset(2, 2, 0, 0) };
		public GUIStyle LabelStyle=new(GUI.skin.label) { fontSize=20, alignment=TextAnchor.UpperLeft, wordWrap=true, richText=true };
		public GUIStyle CenterRichText=new(GUI.skin.label) { alignment=TextAnchor.MiddleCenter, richText=true };

		private const int TitleFontSize=45, SubTitleFontSize=30;
		private static readonly List<(Regex, string)> Tags=[
			(new Regex(@"<title>(.*)</title>"				), $"<size=@{TitleFontSize}><u>$1</u></size>"),
			(new Regex(@"<subtitle>(.*)</subtitle>"			), $"<color=#999999><size={SubTitleFontSize}><u>$1</u></size></color>"),
			(new Regex(@"^([*\t])", RegexOptions.Multiline	), "  $1"),
			(new Regex(@"\t"								), "    "),
			(new Regex(@"(\*.*?)<configs>(.*?)</configs>"	), "<size=15><color=grey>$1 Config options: $2</color></size>"),
			(new Regex(@"<config>(.*?)</config>"			), "<size=15><color=grey>(Config option: “<i>$1</i>”)</color></size>"),
			(new Regex(@"<conf>(.*?)</conf>"				), "“<i>$1</i>”"),
		];

		public HelpWindow() : base(FullMessage)
		{
			//Do not setup when multiple windows open
			NumOpen++;
			if(NumOpen!=1)
				return;

			//Build the message
			try {
				Message=FileOps.LoadEmbeddedResource(HelpTextFile).ReadAllAndCloseS();
			} catch(Exception e) {
				Message=$"<color=red><size=30>Could not load help file: {e.Message}</size></color>";
			}
			foreach((Regex Replacer, string ReplaceWith) in Tags)
				Message=Replacer.Replace(Message, ReplaceWith);
			if(PlayerData.instance.visitedAbyss)
				Message=Regex.Replace(Message, @"@[#-&]+@", "Abyss");

			//Load the texture
			ControllerLayout=new Texture2D(2, 2, TextureFormat.ARGB32, false);
			try {
				if(!ControllerLayout.LoadImage(FileOps.LoadEmbeddedResource(ControllerFileName).ReadAllAndCloseB()))
					throw new Exception("Unity failed loading the image");
			} catch(Exception e) {
				ControllerLayout.TDestroy();
				ControllerLayout=null;
				Log.Error($"Couldn’t load controller layout image: {e.Message}");
				Message=$"\n<color=red><size=30>Couldn’t load controller layout image: {e.Message}</size></color>\n\n{Message}";
			}

			//Store the full message for duplicate windows
			FullMessage=Message;
		}

		protected override void DrawContents()
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
			GUILayout.Label("<color=#FF6C9C><size=20>Scroll this window with Sidebar Scroll Up/Down</size></color>", CenterRichText);
			GUILayout.Label(Message, LabelStyle);
			GUILayout.EndScrollView();

			GUILayout.Space(2);
		}

		protected override void OnUpdate()
		{
			//Scroll up/down the help info
			Direction JD;
			float ScrollDir=
			  Config.C.Shortcut_SB_ScrollUp.IsPressed() ? -1
			: Config.C.Shortcut_SB_ScrollDown.IsPressed() ? 1
			: (JD=GetOrdinalDirectionAndMagnitude(false, 20, .4f, out float Magnitude)) is Direction.Left or Direction.Right
				? Magnitude*(JD==Direction.Left ? -1 : 1)
			: 0;
			if(ScrollDir==0)
				return;

			//Do not allow keypresses too quickly
			const float MoreOftenMultiplier=4;
			const float KeyPressDelay=.075f/MoreOftenMultiplier;
			if((DateTime.Now-LastScrollKeyTime).TotalSeconds<KeyPressDelay)
				return;
			LastScrollKeyTime=DateTime.Now;

			//Do the scrolling
			ScrollPosition.y=Mathf.Max(ScrollPosition.y+ScrollDir*50/MoreOftenMultiplier, 0);
		}

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
		protected override bool BlockAnyKeyClose =>
			   Input.GetKey(Config.C.Shortcut_SB_ScrollUp.Value.MainKey)
			|| Input.GetKey(Config.C.Shortcut_SB_ScrollDown.Value.MainKey)
			|| ActiveDevice.RightStick.HasChanged;
	}
}