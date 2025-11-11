using System;
using UnityEngine;

namespace SilkDev.Windows;

//A window that contains a message and optionally ok/cancel buttons
public class DialogWindow : Window
{
	public string Message;
	public GUIStyle LabelStyle=null!;
	public Action<bool>? ConfirmationDialogCallback; //If this is set then OK/Cancel buttons are added to the dialog

	public DialogWindow(string Message, int Width=800, int Height=400, int FontSize=60) : base("Alert", null, Width, Height)
	{
		(this.Message, Visible)=(Message, true);
		DevInput.Mouse.Visibility.ForceEvent += ForceCursor;
		OnNextFrame(() =>
			LabelStyle=new GUIStyle(GUI.skin.label) { alignment=TextAnchor.MiddleCenter, fontSize=FontSize, wordWrap=true, richText=true }
		);
	}

	protected override void DoLayout(int ID, Event Ev)
	{
		GUILayout.BeginVertical();
		GUILayout.FlexibleSpace();
		GUILayout.Label(Message, LabelStyle);
		if(ConfirmationDialogCallback!=null) {
			GUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			if(GUILayout.Button("OK", GUILayout.Width(100)))
				DialogResult(true);
			if(GUILayout.Button("Cancel", GUILayout.Width(100)))
				DialogResult(false);
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();
		}
		GUILayout.FlexibleSpace();
		GUILayout.EndVertical();
	}

	protected override void CloseButton() => DialogResult(false);
	public void DialogResult(bool AcceptedConfirmation)
	{
		ConfirmationDialogCallback?.Invoke(AcceptedConfirmation);
		Close();
	}
	public override void Close()
	{
		DevInput.Mouse.Visibility.ForceEvent -= ForceCursor;
		base.Close();
	}
	private static bool ForceCursor() => true;
}