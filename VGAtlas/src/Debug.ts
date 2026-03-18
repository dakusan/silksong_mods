export {}; //Make this a module

//Browser debugging
(async () => {
	const [{ Share }, AllShared]=await Promise.all([
		import('./Share'),
		import('./Util/SharedClasses'),
	]);

	//noinspection JSUnusedGlobalSymbols
	const AtlasInfo=(window as unknown as {Atlas:object}).Atlas={
		...Share, ...AllShared,
		Modules:{
			CategoriesAndItems	:await import('./CategoriesAndItems'),
			LinkedLabel			:await import('./LinkedLabel'),
			LoadJSON			:await import('./Util/JSON'),
			Translations		:await import('./Util/Translations'),
			MapCanvas			:await import('./MapCanvas'),
			MapIcon				:await import('./MapIcon'),
			TempClasses			:await import('./TempClasses'),
			SaveData			:await import('./SaveData'),
			WindowManager		:await import('./Util/WindowManager'),
			CategoryGroupsWindow:await import('./DockableWindows/CategoryGroupsWindow'),
		},
		async ExportDefaultData(TrailingCommas=true, Compact=false, MatchModOutput=false, UseTestHTMLExport=false): Promise<string>
		{
			return AtlasInfo.Modules.LoadJSON.SaveJson.ExportDefaultData(
				{Categories:AtlasInfo.DS.Categories, Items:AtlasInfo.DS.Items},
				TrailingCommas, Compact, MatchModOutput, UseTestHTMLExport
			);
		},
	};
})();