using SilkDev;
using SilkDev.DevInput.Mouse;
using SilkDev.Textures;
using UnityEngine;

namespace PharloomAtlas;

public partial class SideBar
{
	private class CategoryGroupSection(CategoryGroup CG, SideBar SB) : SideBarSection($"Category Group {CG.Title}", SB)
	{
		//Styling stuff
		private const int IconSize=16, ItemLabelPadding=4, ColumnPadding=10;
		private static readonly Texture2D CTexIconHover=new Color(0, 0, 0, .5f).MakeTexture(), CTexStrike=Color.white.MakeTexture();
		private static readonly GUIStyle TitleTextStyle=new(GUI.skin.label) { fontSize=18, wordWrap=false };
		private static readonly GUIStyle LabelTextStyle=new(GUI.skin.label) { fontSize=14, wordWrap=false };
		private static readonly Color NormalTitleTextColor=GUI.skin.label.normal.textColor;
		private static readonly Color NormalLabelTextColor=new(0.5f, 0.5f, 0.5f, 1);

		//Constructors
		public readonly CategoryGroup CG=CG;
		static CategoryGroupSection() =>
			new GUIStyle[] { TitleTextStyle, LabelTextStyle }.ForEach(static GS =>
				GS.padding=GS.margin=new RectOffset(0, 0, 0, 0)
			);

		//Getter properties
		protected int ItemsInLeftCol=> (CG.Count+1)/2;
		protected bool HasOddCols	=> ItemsInLeftCol*2!=CG.Count;

		public override void MoveHor(bool IsNeg)
		{
			//Title currently selected
			if(SelectedItem==-1) {
				if(!IsNeg)
					SelectedItem++;
				else
					PrevSection.MoveTo(MoveToType.LastRow|MoveToType.NoCol);
				return;
			}

			//Category currently selected
			int NumInFirstCol=(CG.Count+1)/2;
			bool IsInFirstCol=SelectedItem<NumInFirstCol;
			SelectedItem+=(NumInFirstCol-(IsNeg==IsInFirstCol ? 1 : 0))*(IsInFirstCol ? 1 : -1); //Select next/previous item horizontally
			bool InSameColumn=(IsInFirstCol==(SelectedItem<NumInFirstCol));
			if(InSameColumn && IsNeg) //First item moves to title of category
				SelectedItem=-1;
			else if(InSameColumn || SelectedItem>=CG.Count) //Moved past last item
				NextSection.MoveTo(MoveToType.FirstRow|MoveToType.NoCol);
		}

		public override void MoveVer(bool IsNeg)
		{
			//Title currently selected
			if(SelectedItem==-1) {
				if(!IsNeg)
					SelectedItem++;
				else
					PrevSection.MoveTo(MoveToType.LastRow|MoveToType.LeftCol);
				return;
			}

			//Category currently selected
			int NumInFirstCol=(CG.Count+1)/2;
			bool IsInFirstCol=SelectedItem<NumInFirstCol;
			SelectedItem+=(IsNeg ? -1 : 1); //Move to the next/previous item in the current column
			bool InSameColumn=(IsInFirstCol==(SelectedItem<NumInFirstCol));
			if(SelectedItem<0) //Negative on first item in first column moves to the group title
				SelectedItem=-1;
			else if(!InSameColumn && IsNeg) //Negative on first item in second column
				PrevSection.MoveTo(MoveToType.LastRow|MoveToType.RightCol);
			else if(SelectedItem>=CG.Count) //Positive on last item in second column
				NextSection.MoveTo(MoveToType.FirstRow|MoveToType.RightCol);
			else if(!InSameColumn) //Positive on last item in first column
				NextSection.MoveTo(MoveToType.FirstRow|MoveToType.LeftCol);
		}

		public override void MoveTo(MoveToType M) => MovedTo(
			  M.HasFlag(MoveToType.FirstRow) //Checking for first row
			? (M.HasFlag(MoveToType.RightCol) ? ItemsInLeftCol : -1) //First row
			: M.HasFlag(MoveToType.LeftCol) || (M.HasFlag(MoveToType.NoCol) && HasOddCols) //Second row (checking for left column)
				? ItemsInLeftCol-1 //Left column
				: CG.Count-1 //right column
		);

		protected override void ExecDraw(int ClientWidth)
		{
			//Category section label
			int ColumnWidth=(ClientWidth-ColumnPadding)/2;
			GUILayout.BeginHorizontal();
			GUILayout.Space(AreaMargin);
			Rect LabelRect=GUILayoutUtility.GetRect(0, 0);
			LabelRect.size=TitleTextStyle.CalcSize(new GUIContent(CG.Title));
			bool IsMouseOver=SB.CheckHasMouse && LabelRect.Contains(Event.current.mousePosition);
			TitleTextStyle.normal.textColor=(IsMouseOver ? Color.blue : NormalTitleTextColor);
			GUILayout.Label(CG.Title, TitleTextStyle, GUILayout.Width(ClientWidth));
			if(IsSectionSelected && SelectedItem==-1)
				HighlightLastItem();
			GUILayout.EndHorizontal();
			if(IsMouseOver && Event.current.type==EventType.MouseDown && Button.CurrentButton==Button.Enum.Left)
				SB.DS.CycleGroupCategoryState(CG);

			//Category items
			int i=0, Half=(int)Mathf.Ceil(CG.Count/2f);
			GUILayout.BeginHorizontal();
			foreach(Category CategoryInfo in CG.AsOrdered) {
				//Start columns
				if(i==0 || i==Half) {
					if(i!=0) {
						GUILayout.EndVertical();
						GUILayout.Space(ColumnPadding);
					}
					GUILayout.BeginVertical(GUILayout.Width(ColumnWidth));
				}
				DrawCategory(CategoryInfo, ColumnWidth, IsSectionSelected && i==SelectedItem);
				i++;
			}
			if(CG.Count>0)
				GUILayout.EndVertical();
			GUILayout.EndHorizontal();
		}

		//Draw the category item
		private void DrawCategory(Category CategoryInfo, int ColumnWidth, bool IsSelected)
		{
			//Depending on if the mouse is over this element we will be changing colors and checking for mouse events
			Rect AreaRect=GUILayoutUtility.GetRect(0, 0);
			bool IsMouseOver=SB.CheckHasMouse && new Rect(AreaRect.x, AreaRect.y+2, ColumnWidth, IconSize).Contains(Event.current.mousePosition);
			if(IsMouseOver && Event.current.type==EventType.MouseDown && Button.CurrentButton==Button.Enum.Left)
				SB.DS.SetCategoryState(CategoryInfo, DataStorage.GetNextToggleState(CategoryInfo.ToggleState));
			LabelTextStyle.normal.textColor=
				CategoryInfo.CurrentCount>=CategoryInfo.TotalCount ?
					  (IsMouseOver ? new Color(46/255f, 111/255f, 64/255f) : Color.green) //Forest green
					: (IsMouseOver ? Color.white : NormalLabelTextColor);
			LabelTextStyle.fontStyle=(CategoryInfo.ToggleState==CategoryToggleState.All ? FontStyle.Bold : FontStyle.Normal);

			//Draw the category
			Sprite MySprite=CategoryInfo.Sprite;
			string CountStr=$"{CategoryInfo.CurrentCount}/{CategoryInfo.TotalCount}";
			float CountStrWidth=LabelTextStyle.CalcSize(new GUIContent(CountStr)).x;
			GUILayout.BeginHorizontal();
			GUILayout.Space(AreaMargin);
			GUI.DrawTextureWithTexCoords(
				GUILayoutUtility.GetRect(IconSize, IconSize), MySprite.texture,
				(	  Config.C.IconSet.Value!="Icons-Circles.png" ? MySprite.textureRect
					: new Rect(MySprite.textureRect.x+13, MySprite.textureRect.y+13, 40f, 40f) //Zoom in on circles textures
				).ConvertTexCoords(MySprite.texture)
			);
			if(IsMouseOver)
				GUI.DrawTexture(GUILayoutUtility.GetLastRect(), CTexIconHover);
			GUILayout.Space(ItemLabelPadding);
			GUILayout.Label(CategoryInfo.Title, LabelTextStyle, GUILayout.Width(ColumnWidth-IconSize-CountStrWidth-ItemLabelPadding*2));
			GUILayout.Space(ItemLabelPadding);
			LabelTextStyle.alignment=TextAnchor.MiddleRight;
			GUILayout.Label(CountStr, LabelTextStyle, GUILayout.Width(CountStrWidth));
			LabelTextStyle.alignment=TextAnchor.MiddleLeft;
			GUILayout.EndHorizontal();
			if(IsSelected)
				HighlightLastItem();

			//Draw the strikethrough line depending on its toggle state
			if(CategoryInfo.ToggleState==CategoryToggleState.None) {
				Rect CategoryLineRect=GUILayoutUtility.GetLastRect();
				GUI.DrawTexture(CategoryLineRect.AddY(CategoryLineRect.height/2).AddX(1).SetHeight(1), CTexStrike);
			}

			GUILayout.Space(ItemLabelPadding);
		}

		public override void ExecSelected()
		{
			Category CurCat;
			if(SelectedItem==-1)
				SB.DS.CycleGroupCategoryState(CG);
			else
				SB.DS.SetCategoryState(
					CurCat=CG.AsOrdered[SelectedItem],
					DataStorage.GetNextToggleState(CurCat.ToggleState)
				);
		}
	}
}