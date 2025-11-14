using SilkDev.Textures;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PharloomAtlas;

public partial class SideBar
{
	//Keep track of all sections
	private readonly List<SideBarSection> SectionsList=[];
	private readonly Dictionary<string, SideBarSection> Sections=[];
	private SideBarSection CurrentSection;

	public abstract class SideBarSection
	{
		//Determine where to move in a group
		[Flags] public enum MoveToType { FirstRow=1<<0, LastRow=1<<1, NoCol=1<<2, LeftCol=1<<3, RightCol=1<<4 }

		//Base members
		public readonly string Name;
		public int Index { get; private set; } = 0;
		protected readonly SideBar SB;
		protected int SelectedItem;
		public Action<int>? BeforeDraw=null, AfterDraw=null;

		//Static members
		internal static Texture2D CTexSelect=Config.C.Color_SideBar_Highlight.V.MakeTexture();

		//Getters
		protected SideBarSection PrevSection=> SB.SectionsList[Index-1>=0 ? Index-1 : SB.SectionsList.Count-1];
		protected SideBarSection NextSection=> SB.SectionsList[(Index+1)%SB.SectionsList.Count];
		protected bool IsSectionSelected	=> this==SB.CurrentSection;

		//Constructor
		public SideBarSection(string Name, SideBar SB, int OverrideIndex=-1)
		{
			(this.Name, this.SB)=(Name, SB);

			//If a duplicate named section, throw an error
			if(SB.Sections.ContainsKey(Name))
				throw new ArgumentException($"Sidebar section name already used: {Name}");

			//Set to last if negative
			Index=OverrideIndex;
			if(Index<0 || Index>SB.SectionsList.Count)
				Index=SB.SectionsList.Count;

			//Nothing can proceed a CategoryGroupSection
			if(this is not CategoryGroupSection) {
				for(int i=0;i<Index;i++)
					if(SB.SectionsList[i] is CategoryGroupSection) {
						Index=i;
						break;
					}
			}

			//Add the section to the list
			if(Index==0)
				SB.CurrentSection=this;
			SB.SectionsList.Insert(Index, this);
			SB.Sections[Name]=this;

			//Fix the index of other items that come after this
			for(int i=Index+1; i<SB.SectionsList.Count; i++)
				SB.SectionsList[i].Index++;
		}

		//Overwritten functions
		public abstract void MoveHor(bool IsNeg);
		public abstract void MoveVer(bool IsNeg);
		public abstract void MoveTo(MoveToType M);
		public abstract void ExecSelected();
		protected abstract void ExecDraw(int ClientWidth);
		public virtual void CheckSelectedIndex() { }

		//Base functions
		protected void MovedTo(int SelectedItem) =>
			(SB.CurrentSection, this.SelectedItem)=(this, SelectedItem);
		internal void Draw(int ClientWidth)
		{
			BeforeDraw?.Invoke(ClientWidth);
			ExecDraw(ClientWidth);
			AfterDraw?.Invoke(ClientWidth);
		}

		protected static void HighlightLastItem() => //Draw a highlight texture over the currently highlighted item (which is the last item drawn)
			GUI.DrawTexture(GUILayoutUtility.GetLastRect(), CTexSelect);
	}
}