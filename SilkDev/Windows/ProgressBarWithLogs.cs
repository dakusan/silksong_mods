using System;
using SilkDev.Textures;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SilkDev.Windows;

//A progress bar window with a message line and and 2 logs below it (an error and a normal)
//Any public variables can be updated at any time and the window will auto adjust on the next frame draw
public class ProgressBarWithLogs() : Window(nameof(ProgressBarWithLogs), null, 1200, 1)
{
	//Constants
	private readonly Texture2D DarkGreyTex=new Color(0.25f, 0.25f, 0.25f, 1f).MakeTexture();
	private readonly Texture2D GreyTex=Color.grey.MakeTexture();
	private readonly Texture2D BlackTex=Color.black.MakeTexture();
	private readonly Texture2D BlueTex=Color.blue.MakeTexture();
	private readonly GUIStyle PercentTextStyle=new(GUI.skin.label) { fontSize=DefaultPercentFontSize, alignment=TextAnchor.MiddleCenter };
	private readonly GUIStyle MessageTextStyle=new(GUI.skin.label) { fontSize=15, alignment=TextAnchor.MiddleLeft, richText=true };
	private const int Margin=5, DefaultBarHeight=120, MinBarHeight=20, DefaultPercentFontSize=80;

	//Members
	private readonly Queue<string> LogLines=new(), ErrorLines=new();
	public  string PercentText=Misc.Empty, MessageText=Misc.Empty;
	public  int NumLogLines		{ get; set => KeepBarHeight(() => { field=Mathf.Max(value, 1); while(LogLines  .Count>field) _=LogLines  .Dequeue(); }); } = 5;
	public  int NumErrorLines	{ get; set => KeepBarHeight(() => { field=Mathf.Max(value, 1); while(ErrorLines.Count>field) _=ErrorLines.Dequeue(); }); } = 5;
	public  float PercentAmount { get; set => field=Mathf.Clamp(value, 0, 1); }
	private float LineHeight;
	public int BarHeight
	{
		get => (int)WindowRect.height-HeightWithoutBar;
		set => WindowRect.height=value+HeightWithoutBar;
	}
	public int HeightWithoutBar => Margin*4+(int)((NumLogLines+ErrorLines.Count+1)*LineHeight/1.2f);
	public Action? OnClose=null;

	protected override void OnInit()
	{
		(Visible, Priority, BarHeight, Resizer!.MinSize.x)=(true, 1000, DefaultBarHeight, 300);
		RecalculateLineHeight();
		WindowRect.height=DefaultBarHeight+HeightWithoutBar;
		WindowRect=WindowRect.size.CenterIn(Misc.ScreenSize);
		Resizer!.CheckWindowRect(ref WindowRect);
	}

	protected override void DoLayout(int ID, Event Ev)
	{
		//Determine the window dimensions
		int Width=(int)WindowRect.width;
		Resizer!.MinSize.y=MinBarHeight+HeightWithoutBar;
		int Height=(int)WindowRect.height;
		Resizer!.Default_MoveHandleHeight=Margin+BarHeight;

		//Determine inner dimensions
		Rect PBRect=new(Margin, Margin, Width-Margin*2, BarHeight);
		Rect MessageTextRect=PBRect.AddY(PBRect.height).SetHeight(Height-BarHeight-Margin*2);

		//Draw the window, border, and progress bar
		GUI.DrawTexture(new Rect(0, 0, Width, Height), DarkGreyTex); //Border
		GUI.DrawTexture(PBRect, BlackTex); //Window
		GUI.DrawTexture(PBRect.SetWidth(Mathf.Clamp(PercentAmount, 0, 1)*PBRect.width), BlueTex); //Progress bar background
		GUI.DrawTexture(MessageTextRect, GreyTex); //Progress bar progress

		//Draw the text
		string NewMessageText=string.Join(Misc.Empty, [
			$"<color=green>{Misc.SanitizeRichString(MessageText)}</color>{Misc.NewLine}",
			ErrorLines.Count==0 ? Misc.Empty : "<color=red>"+Misc.SanitizeRichString(string.Join(Misc.NewLine, ErrorLines))+$"</color>{Misc.NewLine}",
			Misc.SanitizeRichString(string.Join(Misc.NewLine, LogLines)),
		]);
		MessageTextRect.x+=Margin;
		MessageTextRect.width-=Margin*2;
		PercentTextStyle.fontSize=Mathf.CeilToInt(DefaultPercentFontSize-(DefaultBarHeight-BarHeight)/1.5f);
		CalculateFontSize(PercentText, PercentTextStyle, PBRect.width);
		GUI.Label(PBRect, PercentText, PercentTextStyle);
		GUI.Label(MessageTextRect, NewMessageText, MessageTextStyle);

		//Show the close button
		if(GUI.Button(new Rect(WindowRect.width-CloseButtonSize-CloseButtonPadding, CloseButtonPadding, CloseButtonSize, CloseButtonSize), "X"))
			Catcher.Run(() => $"“{Title}”.{nameof(CloseButton)}", CloseButton);
	}

	//Calculate the maximum font size usable (if the initial font size is too big) where (Style)Message will fit within MaxWidth
	public static void CalculateFontSize(string Message, GUIStyle Style, float MaxWidth, int TestStep=10)
	{
		//If the size already fits, nothing to do
		GUIContent GUIMessage=new(Message);
		bool DoesSizeFit() => Style.CalcSize(GUIMessage).x<=MaxWidth;
		if(DoesSizeFit())
			return;

		//Get a size where it does fit using TestStep increments
		while((Style.fontSize-=TestStep)>1 && !DoesSizeFit());
		int Lo=Mathf.Max(Style.fontSize, 1), Hi=Lo+TestStep-1;

		//Run the binary search
		while(Lo<Hi) {
			int Mid=Hi-(Hi-Lo)/2;
			Style.fontSize=Mid;
			if(DoesSizeFit())
				Lo=Mid;
			else
				Hi=Mid-1;
		}
		Style.fontSize=Lo;
	}

	public void AddLogLines(IEnumerable<string> Lines)
	{
		foreach(string Line in Lines.TakeLast(NumLogLines)) {
			while(LogLines.Count>=NumLogLines)
				_=LogLines.Dequeue();
			LogLines.Enqueue(Line);
		}
	}

	public void AddErrorLine(string Line) => KeepBarHeight(() =>
	{
		while(ErrorLines.Count>=NumErrorLines)
			_=ErrorLines.Dequeue();
		ErrorLines.Enqueue(Line);
	});

	//Make sure the bar height stays the same after the window height changes
	private void KeepBarHeight(Action A)
	{
		int StartBarHeight=BarHeight;
		A();
		BarHeight=StartBarHeight;
	}

	//Call this if you change the MessageTextStyle
	public void RecalculateLineHeight() =>
		LineHeight=MessageTextStyle!.CalcHeight(new GUIContent("ABCDefg"), float.MaxValue);

	public override void Close()
	{
		OnClose?.Invoke();
		base.Close();
		foreach(Texture2D DelTex in new Texture2D[] { DarkGreyTex, GreyTex, BlackTex, BlueTex })
			DelTex.TDestroy();
	}
}