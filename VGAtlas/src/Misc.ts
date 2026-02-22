export const InitFuncs:(() => void)[]=[];

//Browser debugging
InitFuncs.push(async () => {
	const [{ Share }, AllShared]=await Promise.all([
		import("./Share"),
		import("./SharedClasses"),
	]);

	(window as unknown as {Atlas:object}).Atlas={
		...Share, ...AllShared,
		Modules:{
			CategoriesAndItems	:await import("./CategoriesAndItems"),
			LoadJSON			:await import("./JSON"),
			MapCanvas			:await import("./MapCanvas"),
			MapIcon				:await import("./MapIcon"),
			TempClasses			:await import("./TempClasses"),
			SaveData			:await import("./SaveData"),
		},
	}
});