export const InitFuncs:(() => void)[]=[];

//Browser debugging
import { Share } from "./main";
import * as AllShared from "./SharedClasses";
InitFuncs.push(() =>
	(window as any).Atlas={...Share, SharedClasses:AllShared }
);