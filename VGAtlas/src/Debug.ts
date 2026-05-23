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
		Share:Share,
		$:(await import('jquery')).default,
		Modules:{
			CategoriesAndItems	: await import('./CategoriesAndItems'),
			LinkedLabel			:(await import('./LinkedLabel')).default,
			Config				: await import('./Config/Config'),
			LoadJSON			: await import('./Util/JSON'),
			Translations		: await import('./Util/Translations'),
			MapIcon				: await import('./MapIcon'),
			CustomItem			:(await import('./CustomItem')).default,
			WindowManager		: await import('./Util/WindowManager'),
			CategoryGroupsWindow:(await import('./Windows/CategoryGroupsWindow/CategoryGroupsWindow')).default,
		},
		async ExportDefaultData(TrailingCommas=true, Compact=false, MatchModOutput=false, UseTestHTMLExport=false): Promise<string>
		{
			return AtlasInfo.Modules.LoadJSON.SaveJson.ExportDefaultData(
				{Categories:AtlasInfo.DS.Categories, Items:AtlasInfo.DS.Items},
				TrailingCommas, Compact, MatchModOutput,
				!UseTestHTMLExport ? undefined : Str => new AtlasInfo.Modules.LinkedLabel(Str).RenderedContents
			);
		},
	};

	delete (AtlasInfo as {SaveData:unknown}).SaveData; //Since this variable is mutable, do not keep it in AtlasInfo
})();