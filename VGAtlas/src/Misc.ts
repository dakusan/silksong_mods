export const InitFuncs:(() => void)[]=[];

//Browser debugging
import { Share } from "./main";
import * as AllShared from "./SharedClasses";
import { LC } from "./AtlasConfig"
InitFuncs.push(() =>
	(window as unknown as {Atlas:object}).Atlas={...Share, SharedClasses:AllShared, LC}
);