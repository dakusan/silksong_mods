using System;
using System.Collections.Generic;
using UnityEngine;

namespace PharloomAtlas;

public partial class SideBar
{
	public class ButtonsRowSection : SideBarSection
	{
		private static readonly GUIStyle TitleStyle=new(GUI.skin.label) { wordWrap=false, richText=false };
		private static readonly SilkDev.Translations Tr=Config.C.Tr;
		public readonly List<Button> Buttons=[];
		private readonly string Title;
		public class Button
		{
			public string Title;
			public Action Exec;
			public readonly int Index;
			public readonly ButtonsRowSection Parent;
			public string TransTitle => Tr.T(Title, "SideBarButtons", true);
			public Button(string Title, Action Exec, ButtonsRowSection Parent)
			{
				(this.Title, this.Exec, this.Parent, Index)=(Title, Exec, Parent, Parent.Buttons.Count);
				Parent.Buttons.Add(this);
			}
			public void Execute() => Exec();
			internal void Draw()
			{
				if(GUILayout.Button(TransTitle))
					Exec.Invoke();
				if(Parent.IsSectionSelected && Parent.SelectedItem==Index)
					HighlightLastItem();
			}
		}

		public readonly struct CreateButton(string Title, Action Exec)
		{
			public readonly string Title=Title;
			public readonly Action Exec=Exec;
		}

		public ButtonsRowSection(string Name, string Title, SideBar SB, CreateButton[] Buttons, int OverrideIndex=-1) : base(Name, SB, OverrideIndex)
		{
			this.Title=Title;
			foreach(CreateButton Button in Buttons)
				_=new Button(Button.Title, Button.Exec, this);
		}

		public override void MoveHor(bool IsNeg)
		{
			SelectedItem+=(IsNeg ? -1 : 1); //Select next/previous button
			if(SelectedItem>=Buttons.Count) //Last button moves to next title
				NextSection.MoveTo(MoveToType.FirstRow|MoveToType.NoCol);
			else if(SelectedItem<0) //First button moves to last group last item in column 1
				PrevSection.MoveTo(MoveToType.LastRow|MoveToType.NoCol);
		}

		public override void MoveVer(bool IsNeg)
		{
			(IsNeg ? PrevSection : NextSection).MoveTo(
				(IsNeg ? MoveToType.LastRow : MoveToType.FirstRow)|
				(SelectedItem!=Buttons.Count-1 ? MoveToType.LeftCol : MoveToType.RightCol)
			);
		}

		public override void MoveTo(MoveToType M) {
			if(Buttons.Count!=0)
				MovedTo(M.HasFlag(MoveToType.RightCol) || M==(MoveToType.NoCol|MoveToType.LastRow) ? Buttons.Count-1 : 0);
			else
				(M.HasFlag(MoveToType.LastRow) ? PrevSection : NextSection).MoveTo(M); //Pass through section if empty
		}

		protected override void ExecDraw(int ClientWidth)
		{
			if(Buttons.Count==0)
				return;

			GUIContent RowTitle=new($"{Tr.T(Title, "SideBarButtons")}:");
			GUILayout.BeginHorizontal(GUILayout.Width(ClientWidth));
			GUILayout.Label(RowTitle, TitleStyle, GUILayout.ExpandWidth(false));
			float LineUsedWidth=TitleStyle.CalcSize(RowTitle).x;
			foreach(Button B in (Button[])[.. Buttons]) {
				GUILayout.FlexibleSpace();

				//Move to a new row on overflow
				float NewButtonWidth=GUI.skin.button.CalcSize(new GUIContent(B.TransTitle)).x;
				if(LineUsedWidth+NewButtonWidth>ClientWidth) {
					GUILayout.EndHorizontal();
					GUILayout.BeginHorizontal(GUILayout.Width(ClientWidth));
					GUILayout.FlexibleSpace();
					LineUsedWidth=0;
				}

				B.Draw();
				LineUsedWidth+=NewButtonWidth;
			}
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();
		}

		public override void ExecSelected() =>
			Buttons[SelectedItem].Execute();

		public override void CheckSelectedIndex() =>
			MovedTo(Mathf.Min(SelectedItem, Buttons.Count-1));
	}
}