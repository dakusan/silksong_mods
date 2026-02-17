export const InitFuncs:(() => void)[]=[];

//Browser debugging
import { Share } from "./Main"
import * as AllShared from "./SharedClasses"
import { LC } from "./AtlasConfig"

InitFuncs.push(async () =>
	(window as unknown as {Atlas:object}).Atlas={
		...Share, ...AllShared, LC,
		Modules:{
			CategoriesAndItems	:await import("./CategoriesAndItems"),
			LoadJSON			:await import("./LoadJSON"),
			MapCanvas			:await import("./MapCanvas"),
			MapIcon				:await import("./MapIcon"),
			TempClasses			:await import("./TempClasses"),
		},
	}
);