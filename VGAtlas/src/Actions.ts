import { Log, StatStr, Util } from './Util/SharedClasses';
import { Share } from './Share';
import { Item } from './CategoriesAndItems';
import CustomItem from './CustomItem';

//noinspection JSUnusedGlobalSymbols
const NullFunc=() => null;
const Actions:Record<string, (Value:string) => string|null>={
	AddCI			: AddCI,
	DelCI			: Value	=> DelCI  (Value)===0 ? "No items found for: "+Value : null,
	ClearCI			: ()	=> ClearCI(		)===0 ? "No items found" : null,
	X:NullFunc, Y:NullFunc, Duration:NullFunc, ZoomScale:NullFunc,
} as const;

export function ProcessActions(Values:ArrayIterator<[string, string]>): boolean
{
	let HasErrors=false;
	let Err:string|null|undefined;
	for(const [Command, Value] of Values) {
		if((Err=Actions[Command]?.(Value))===null)
			continue;
		Log.Error(
			StatStr.NeedsTranslate+`Error processing command “${Command}”: `+
			(Err ?? "Invalid command")
		);
		HasErrors=true;
	}

	return !HasErrors;
}

type CIType=Readonly<{X:number, Y:number, Label:string, Title:string, Description:string, ID?:number}>;
export function AddCI(Value:string|CIType): string|null
{
	class PartError extends Error { }
	let AddParts:CIType;
	try {
		AddParts=(typeof(Value)==='string' ? JSON.parse(Value) : Value);
		if(AddParts===null || typeof(AddParts)!=='object' || Array.isArray(AddParts))
			throw new SyntaxError("Not an object");
		for(const Name of ['X', 'Y'].concat(AddParts.ID!==undefined ? ['ID'] : []) as ('X'|'Y'|'ID')[])
			if(!Number.isFinite(AddParts[Name]))
				throw new PartError(Name);
		for(const Name of ['Label', 'Title', 'Description'] as const)
			if(typeof(AddParts[Name])!=='string' || AddParts[Name].trim().length<1)
				throw new PartError(Name);
		if(AddParts.ID!==undefined && (!Number.isInteger(AddParts.ID) || !Item.IDInRange(AddParts.ID)))
			throw new PartError('ID');
		new CustomItem(AddParts.X, AddParts.Y, AddParts.Title, AddParts.Description, AddParts.Label, false, undefined, AddParts.ID);
	} catch(e) {
		if(e instanceof PartError)
			return StatStr.NeedsTranslate+`Invalid value for “${e.message}”: `+JSON.stringify(AddParts![e.message as keyof typeof AddParts]);
		else if(e instanceof SyntaxError)
			return StatStr.NeedsTranslate+`Error parsing “${Util.GetErrorMessage(e)}”: `+Value;
		else
			return StatStr.NeedsTranslate+`Error creating custom item: `+Util.GetErrorMessage(e);
	}

	return null;
}

export function DelCI(Value:string)	: number { return DelCIReal(I => (I.ID===Util.GetInt(Value)) || I.MyLabel===Value); }
export function ClearCI()			: number { return DelCIReal(() => true); }
function DelCIReal(CheckFunc:(I:CustomItem) => boolean): number
{
	let Count=0;
	for(const I of Share.DS.Items.values())
		if(I instanceof CustomItem && CheckFunc(I)) {
			I.Delete();
			Count++;
		}
	return Count;
}