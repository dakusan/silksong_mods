import $ from "jquery"
import { Window } from "../WindowManager"
import { Category, CategoryGroup, CategoryToggleState } from "../CategoriesAndItems"
import type DataStorage from "../DataStorage"
import { FriendClass, WillBeSet } from "../SharedClasses"
import { Share } from "../Share"

export default class CategoryGroupsWindow extends Window
{
	private static _Self:CategoryGroupsWindow=WillBeSet;
	public static get Self() { return this._Self ??= new CategoryGroupsWindow(); }

	public readonly $CatTable=$("<div class=CategoryGroups>");
	public readonly Rows:ReadonlyMap<number, CategoryRow>=new Map();

	constructor()
	{
		super({Title:"Categories", MinWidth:234, Width:707, Height:598});
		this.$Content.attr('id', 'CategoryGroupsWindow');

		$("<div class=CategoryGroupButtons>").appendTo(this.$Content).append(
			$(document.createElement("button")).text("Show All"			).on("click", Share.DS.SetAllCategoriesStates			.bind(Share.DS, CategoryToggleState.All			)),
			$(document.createElement("button")).text("Show Incomplete"	).on("click", Share.DS.SetAllCategoriesStates			.bind(Share.DS, CategoryToggleState.Incomplete	)),
			$(document.createElement("button")).text("Hide all"			).on("click", Share.DS.SetAllCategoriesStates			.bind(Share.DS, CategoryToggleState.None		)),
			$(document.createElement("button")).text("Needed for 100%"	).on("click", Share.DS.SetCategoriesStatesFor100Percent	.bind(Share.DS									)),
		);

		for(const CatGrp of Share.DS.CategoryGroups)
			this.InitGroup(CatGrp);
		this.$CatTable.appendTo(this.$Content);
	}
	private InitGroup(CG:CategoryGroup)
	{
		//Category section label
		const $CatGroup=$("<div class=Group>").appendTo(this.$CatTable);
		const SectionTitle=$("<button class=Title>").text(CG.Title).appendTo($CatGroup);
		SectionTitle.on("click", Share.DS.CycleGroupCategoryState.bind(Share.DS, CG));

		//Category items
		for(const Cat of CG.AsOrdered)
			(this.Rows as Map<number, CategoryRow>).set(Cat.ID,
				CategoryRow_Friend.Init($CatGroup, Cat)
			);
	}

	public override OnClosing() { this.Visible=false; return true; }
}

class CategoryRow
{
	public readonly $Cat	:JQuery=$(document.createElement("span")).addClass("Category"	);
	public readonly $Counts	:JQuery=$(document.createElement("span")).addClass("Counts"		);
	public readonly $Icon	:JQuery=$(document.createElement("span")).addClass("ItemIcon"	);
	public readonly $Name	:JQuery=$(document.createElement("span")).addClass("Name"		);

	protected static Init($ParentEl:JQuery, CategoryInfo:Category) { return new CategoryRow($ParentEl, CategoryInfo); }
	protected constructor(
		$ParentEl:JQuery,
		public readonly CategoryInfo:Category
	) {
		const Row=$("<button class=Row />").append(
			this.$Cat.append(
				this.$Icon.addClass("I"+CategoryInfo.IconID),
				this.$Name.text(CategoryInfo.Title),
			),
			this.$Counts,
		).appendTo($ParentEl);

		Row.on("click", this.CategoryClicked.bind(this));
		CategoryInfo.CallOnUpdate.push(this.UpdateInfo.bind(this));
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
			.toggleClass("Completed"		, this.CategoryInfo.CurrentCount>=this.CategoryInfo.TotalCount	);
		this.$Cat
			.toggleClass("StateAll"			, this.CategoryInfo.ToggleState===CategoryToggleState.All		)
			.toggleClass("StateIncomplete"	, this.CategoryInfo.ToggleState===CategoryToggleState.Incomplete)
			.toggleClass("StateNone"		, this.CategoryInfo.ToggleState===CategoryToggleState.None		);
		this.$Counts.text(`${this.CategoryInfo.CurrentCount}/${this.CategoryInfo.TotalCount}`);
	}
}

abstract class CategoryRow_Friend extends CategoryRow implements FriendClass
{
	public static override Init($ParentEl:JQuery, CategoryInfo:Category) { return super.Init($ParentEl, CategoryInfo); }
	//Ignore these
	protected constructor(_$ParentEl:JQuery, _CategoryInfo:Category) { super(null!, null!); this.Stub(); }
	public Stub<T>(_V?:T): T { throw new Error("This function is a stub"); }
}