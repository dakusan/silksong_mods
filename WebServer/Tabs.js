export default class Tab
{
	/** @type {Tab} */ static RootTab;

	/** @type {Tab|null} */ Parent=null; //Only null for top level parent
	Title="";
	Slug="";
	/** @type {HTMLElement|null} */ $Tab=null; //Only null for top level parent
	/** @type {HTMLElement|null} */ $Contents=null; //Only null for top level parent
	/** @type {Tab[]} */ SubTabs=[];
	/** @type {Tab|null} */ LastSubTab=null;
	constructor(Parent, Slug, Title, $Tab, $Contents) { //Consider this protected
		[this.Parent, this.Slug, this.Title, this.$Tab, this.$Contents]=[Parent, Slug, Title, $Tab, $Contents];
		if(this.Parent!==null)
			this.constructor.BindDOMTab(this.$Tab, this.Select.bind(this));
	}

	Select(UpdateHashAndTitle=true, AutoSelectFirstChild=true)
	{
		if(this.Parent===null) return;
		this.Parent.LastSubTab?.DeSelect();
		this.constructor.ToggleDOMTab((this.Parent.LastSubTab=this).$Tab, true);
		this.$Contents.classList.toggle("Selected", true);
		if(AutoSelectFirstChild && this.SubTabs.length)
			return void this.SubTabs[0].Select(UpdateHashAndTitle, AutoSelectFirstChild);

		if(!UpdateHashAndTitle)
			return;
		const TabList=this.BranchToLeaf();
		const Target='#'+TabList.map(T => encodeURIComponent(T.Slug)).join('-');
		if(location.hash!==Target)
			history.pushState(null, '', Target);
		document.title=TabList.map(T => T.Title).join(' - ')+': '+this.constructor.RootTab.Title;
	}
	DeSelect()
	{
		if(this.Parent===null) return;
		this.LastSubTab?.DeSelect();
		this.constructor.ToggleDOMTab(this.$Tab, false);
		this.$Contents.classList.toggle("Selected", false);
		this.Parent.LastSubTab=null;
	}

	/** @returns {Tab} */
	AddSubTab($Tab)
	{
		//aria-controls is like: panel-foo-bar-baz (where foo-bar are parent slugs)
		const Aria=$Tab.getAttribute('aria-controls');
		if(!Aria?.startsWith('panel-'))
			throw new Error("aria-controls missing or invalid");

		//Get the slug
		const SlugParts=Aria.substring('panel-'.length).split('-');
		const Slug=SlugParts.pop();
		const Parents=this.BranchToLeaf();
		if(Parents.length!==SlugParts.length)
			throw new Error("Incorrect number of slug parts");
		for(const [Index, SlugPart] of SlugParts.entries())
			if(Parents[Index]?.Slug!==SlugPart)
				throw new Error("Mismatch in parent slug");

		const NewTab=new this.constructor(
			this, Slug,
			$Tab.dataset.title ?? Slug.replace(/_([a-z])/g, M => ' '+M[1].toUpperCase()),
			$Tab, document.getElementById(Aria)
		);
		this.SubTabs.push(NewTab);
		return NewTab;
	}

	//Does not include the root
	/** @returns {Tab[]} */
	BranchToLeaf()
	{
		const List=[];
		for(let CurTab=this; CurTab!==null; CurTab=CurTab.Parent)
			List.unshift(CurTab);
		return List.slice(1);
	}

	static BindDOMTab($Tab, Func)
	{
		$Tab.addEventListener('click', Func);
		$Tab.addEventListener('keydown', e => {
			if(e.key==='Enter' || e.key===' ') {
				e.preventDefault();
				Func();
			}
		});
	}
	static ToggleDOMTab($Tab, IsSelected)
	{
		$Tab.classList.toggle	('Selected'		, IsSelected);
		$Tab.setAttribute		('aria-selected', IsSelected ? 'true' : 'false');
		$Tab.tabIndex=IsSelected ? 0 : -1;
	}

	static HashUpdated(FirstRun=false)
	{
		let CurTab=this.RootTab;
		for(const HashPart of decodeURIComponent((location.hash || '').slice(1)).trim().split('-').filter(Boolean)) {
			/** @type {Tab} */ const NextTab=CurTab.SubTabs.find(T => T.Slug===HashPart);
			if(!NextTab)
				break;
			NextTab.Select(false, false);
			CurTab=NextTab;
		}
		if(FirstRun || CurTab!==this.RootTab)
			(CurTab===this.RootTab ? CurTab.SubTabs[0] : CurTab).Select(false, true);
	}

	/** @param {Tab|string} TitleOrRootTab */
	static InitRoot(TitleOrRootTab)
	{
		const NewTab=
			  TitleOrRootTab instanceof Tab ? TitleOrRootTab
			: new Tab(null, null, TitleOrRootTab.toString(), null, null);
		this.RootTab=NewTab;
		NewTab.ProcessTagGroup(document.getElementById('RootTabs'));
		window.addEventListener('hashchange', this.HashUpdated.bind(this, false));
		setTimeout(this.HashUpdated.bind(this, true), 0); //Delay so RootTab will be set
		return NewTab;
	}

	/** @param {HTMLElement} $TabSection */
	ProcessTagGroup($TabSection)
	{
		for(const ST of $TabSection.querySelectorAll(":scope > [aria-controls]")) {
			const NewTab=this.AddSubTab(ST);
			NewTab.ProcessTagGroup(NewTab.$Contents);
		}
	}
}