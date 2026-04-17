import $ from 'jquery';
import { FriendClass, StatStr, WillBeSet } from '../Util/SharedClasses';
import { Window } from '../Util/WindowManager';
import { Share } from '../Share';
import type DataStorage from '../DataStorage';
import { Category, CategoryGroup, CategoryToggleState } from '../CategoriesAndItems';

export default class CategoryGroupsWindow extends Window
{
	private static _Self:CategoryGroupsWindow=WillBeSet;
	public static get Self() { return this._Self ??= new CategoryGroupsWindow(); }

	public readonly $CatTable=$('<div class=CategoryGroups>');
	public readonly Rows:ReadonlyMap<number, CategoryRow>=new Map();

	constructor()
	{
		super({Title:'-', Type:'CategoryGroups', MinWidth:234, Width:707, Height:598, SaveID:'CategoryGroups'});
		this.$Content.attr('id', 'CategoryGroupsWindow');

		function CreateSBTransButton(TranslationKey:string, ClickFunc:() => void)
		{
			const El=$(document.createElement('button')).on('click', ClickFunc);
			Share.Tr.CreateTranslationElement(El[0], TranslationKey, 'SideBarButtons');
			return El;
		}
		$('<div class=CategoryGroupButtons>').appendTo(this.$Content).append(
			CreateSBTransButton("Show All"			, Share.DS.SetAllCategoriesStates			.bind(Share.DS, CategoryToggleState.All			)),
			CreateSBTransButton("Show Incomplete"	, Share.DS.SetAllCategoriesStates			.bind(Share.DS, CategoryToggleState.Incomplete	)),
			CreateSBTransButton("Hide all"			, Share.DS.SetAllCategoriesStates			.bind(Share.DS, CategoryToggleState.None		)),
			CreateSBTransButton("Needed for 100%"	, Share.DS.SetCategoriesStatesFor100Percent	.bind(Share.DS									)),
			CreateSBTransButton("Show Unlinked"		, this.UpdateShowLinked						.bind(this, 									)).attr('ID', 'ShowUnlinkedButton').attr('state', 'off'),
		);

		for(const CatGrp of Share.DS.CategoryGroups)
			this.InitGroup(CatGrp);
		this.$CatTable.appendTo(this.$Content);

		Share.Tr.UpdateDOMSubElements(this.$Content[0]);
		this.LanguageChanged();
	}
	private InitGroup(CG:CategoryGroup)
	{
		//Category section label
		const $CatGroup=$('<div class=Group>').appendTo(this.$CatTable);
		const SectionTitle=$('<button class=\'TranslationEl Title\' data-translation-section=Categories>').text(CG.Title).attr('data-translation-key', CG.Title).appendTo($CatGroup);
		SectionTitle.on('click', Share.DS.CycleGroupCategoryState.bind(Share.DS, CG));

		//Category items
		for(const Cat of CG.AsOrdered)
			(this.Rows as Map<number, CategoryRow>).set(Cat.ID,
				CategoryRow_Friend.Init($CatGroup, Cat)
			);
	}

	public override LanguageChanged()
	{
		Share.Tr.OnLanguageLoadedOnce(() => this.Title=Share.Tr.T("Category States", 'SettingNames'));
	}

	public override OnClosing()
	{
		for(const Row of this.Rows.values())
			(Row as CategoryRow_Friend).Unload();
		CategoryGroupsWindow._Self=null!;
		return false;
	}

	private UpdateShowLinked()
	{
		const Btn=$('#ShowUnlinkedButton');
		const TurnOn=Btn.attr('state')==='off';
		Share.Tr.UpdateDOMElement(Btn.attr({
			'data-translation-key': TurnOn ? "Hide Unlinked" : "Show Unlinked",
			state:TurnOn ? 'on' : 'off'
		})[0]);
		Share.MC.ShowLinkedStatus=TurnOn;
	}
}

class CategoryRow
{
	public readonly $Cat	:JQuery=$(document.createElement('span')).addClass('Category'			);
	public readonly $Counts	:JQuery=$(document.createElement('span')).addClass('Counts'				);
	public readonly $Icon	:JQuery=$(document.createElement('span')).addClass('ItemIcon'			);
	public readonly $Name	:JQuery=$(document.createElement('span')).addClass('Name TranslationEl'	).attr('data-translation-section', 'Categories');

	protected static Init($ParentEl:JQuery, CategoryInfo:Category) { return new CategoryRow($ParentEl, CategoryInfo); }
	protected constructor(
		$ParentEl:JQuery,
		public readonly CategoryInfo:Category
	) {
		const Row=$('<button class=Row />').append(
			this.$Cat.append(
				this.$Icon.addClass('I'+CategoryInfo.IconID),
				this.$Name.text(CategoryInfo.Title).attr('data-translation-key', CategoryInfo.Title),
			),
			this.$Counts,
		).appendTo($ParentEl);

		Row.on('click', this.CategoryClicked.bind(this));
		CategoryInfo.CallOnUpdate.Add('CatGroupsWindow', () => this.UpdateInfo());
		this.UpdateInfo();
	}
	private CategoryClicked()
	{
		Share.DS.SetCategoryState(
			this.CategoryInfo,
			(Share.DS.constructor as typeof DataStorage).GetNextToggleState(this.CategoryInfo.ToggleState)
		);
	}
	private UpdateInfo()
	{
		this.$Cat.parent()
			.toggleClass('Completed'		, this.CategoryInfo.CurrentCount>=this.CategoryInfo.TotalCount	);
		this.$Cat
			.toggleClass('StateAll'			, this.CategoryInfo.ToggleState===CategoryToggleState.All		)
			.toggleClass('StateIncomplete'	, this.CategoryInfo.ToggleState===CategoryToggleState.Incomplete)
			.toggleClass('StateNone'		, this.CategoryInfo.ToggleState===CategoryToggleState.None		);
		this.$Counts.text(StatStr.NeedsTranslate+`${this.CategoryInfo.CurrentCount}/${this.CategoryInfo.TotalCount}`);
	}
	protected Unload() { this.CategoryInfo.CallOnUpdate.Remove('CatGroupsWindow'); }
}

abstract class CategoryRow_Friend extends CategoryRow implements FriendClass
{
	public static override Init($ParentEl:JQuery, CategoryInfo:Category) { return super.Init($ParentEl, CategoryInfo); }
	public override Unload() { this.Stub(); }
	//Ignore these
	protected constructor(_$ParentEl:JQuery, _CategoryInfo:Category) { super(null!, null!); this.Stub(); }
	public Stub<T>(_V?:T): T { throw new Error('This function is a stub'); }
}