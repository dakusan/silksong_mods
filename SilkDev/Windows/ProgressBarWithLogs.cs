using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SilkDev.Windows;

//A progress bar window with a message line and and 2 logs below it (an error and a normal)
//Any public variables can be updated at any time and the window will auto adjust on the next frame draw
public class ProgressBarWithLogs : Window
{
	//All functions confirm these are set before using them
	private readonly Texture2D DarkGreyTex=new Color(0.25f, 0.25f, 0.25f, 1f).MakeTexture();
	private readonly Texture2D GreyTex=Color.grey.MakeTexture();
	private readonly Texture2D BlackTex=Color.black.MakeTexture();
	private readonly Texture2D BlueTex=Color.blue.MakeTexture();
	private readonly GUIStyle PercentTextStyle=new(GUI.skin.label) { fontSize=80, alignment=TextAnchor.MiddleCenter };
	private readonly GUIStyle MessageTextStyle=new(GUI.skin.label) { fontSize=15, alignment=TextAnchor.MiddleLeft, richText=true };

	private readonly Queue<string> LogLines=new(), ErrorLines=new();
	public  string PercentText=Misc.Empty, MessageText=Misc.Empty;
	public  int NumLogLines=5, NumErrorLines=5, Width=1200, BarHeight=120;
	public  float PercentAmount; //0-1
	private float LineHeight;

	public ProgressBarWithLogs() : base(nameof(ProgressBarWithLogs), true, 1000) =>
		RecalculateLineHeight();

	protected override void DoLayout(int ID, Event Ev)
	{
		//Determine the window dimensions
		const int Margin=5;
		int Height=BarHeight+Margin*4+(int)((NumLogLines+ErrorLines.Count+1)*LineHeight/1.2f);
		WindowRect=new Vector2(Width, Height).CenterIn(Misc.ScreenSize);

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
		GUI.Label(PBRect, PercentText, PercentTextStyle);
		GUI.Label(MessageTextRect, NewMessageText, MessageTextStyle);
	}

	public void AddLogLines(IEnumerable<string> Lines)
	{
		foreach(string Line in Lines.TakeLast(NumLogLines)) {
			while(LogLines.Count>=NumLogLines)
				_=LogLines.Dequeue();
			LogLines.Enqueue(Line);
		}
	}

	public void AddErrorLine(string Line)
	{
		while(ErrorLines.Count>=NumErrorLines)
			_=ErrorLines.Dequeue();
		ErrorLines.Enqueue(Line);
	}

	//Call this if you change the MessageTextStyle
	public void RecalculateLineHeight() =>
		LineHeight=MessageTextStyle!.CalcHeight(new GUIContent("ABCDefg"), float.MaxValue);

	public override void Close()
	{
		base.Close();
		foreach(Texture2D DelTex in new Texture2D[] { DarkGreyTex, GreyTex, BlackTex, BlueTex })
			DelTex.TDestroy();
	}
}