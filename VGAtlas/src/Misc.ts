export const InitFuncs:(() => void)[]=[];

//Browser debugging
import * as AllShared from "./SharedClasses"
import { Share } from "./Share"

InitFuncs.push(async () =>
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
);