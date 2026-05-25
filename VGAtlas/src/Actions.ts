import { Log, StatStr, Util } from './Util/SharedClasses';
import { Share } from './Share';
import { Item } from './CategoriesAndItems';
import CustomItem from './CustomItem';

//noinspection JSUnusedGlobalSymbols
const NullFunc=() => null;
const Actions:Record<string, (Value:string) => string|null>={
	AddCI			: AddCIFunc,
	DelCI			: Value	=> DelCIFunc  (Value)===0 ? "No items found for: "+Value : null,
	ClearCI			: ()	=> ClearCIFunc(		)===0 ? "No items found" : null,
	X:NullFunc, Y:NullFunc, Duration:NullFunc, ZoomScale:NullFunc,
} as const;

export function ProcessActions(Values:ArrayIterator<[string, string]>)
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

function AddCIFunc(Value:string)
{
	class PartError extends Error { }
	let AddParts:{X:number, Y:number, Label:string, Title:string, Description:string, ID?:number};
	try { (() => { //Put in function to ignore inspection - This code is temporary anyway until I implement a proper validation library
		AddParts=JSON.parse(Value);
		if(AddParts===null || typeof(AddParts)!=='object' || Array.isArray(AddParts))
			throw new SyntaxError("Not an object");
		for(const Name of ['X', 'Y'].concat(AddParts.ID!==undefined ? ['ID'] : []) as (keyof typeof AddParts)[])
			if(typeof(AddParts[Name])!=='number' || !Number.isFinite(AddParts[Name]))
				throw new PartError(Name);
		for(const Name of ['Label', 'Title', 'Description'] as const)
			if(typeof(AddParts[Name])!=='string' || AddParts[Name].trim().length<1)
				throw new PartError(Name);
		if(AddParts.ID!==undefined && (!Number.isInteger(AddParts.ID) || !Item.IDInRange(AddParts.ID)))
			throw new PartError('ID');
		new CustomItem(AddParts.X, AddParts.Y, AddParts.Title, AddParts.Description, AddParts.Label, false, undefined, AddParts.ID);
	})() } catch(e) {
		if(e instanceof PartError)
			return StatStr.NeedsTranslate+`Invalid value for “${e.message}”: `+JSON.stringify(AddParts![e.message as keyof typeof AddParts]);
		else if(e instanceof SyntaxError)
			return StatStr.NeedsTranslate+`Error parsing “${Util.GetErrorMessage(e)}”: `+Value;
		else
			return StatStr.NeedsTranslate+`Error creating custom item: `+Util.GetErrorMessage(e);
	}

	return null;
}

function DelCIFunc(Value:string){ return DelCI(I => (/^\d+$/.test(Value) && I.ID===Number(Value)) || I.MyLabel===Value); }
function ClearCIFunc()			{ return DelCI(() => true); }
function DelCI(CheckFunc:(I:CustomItem) => boolean): number
{
	let Count=0;
	for(const I of Share.DS.Items.values())
		if(I instanceof CustomItem && CheckFunc(I)) {
			I.Delete();
			Count++;
		}
	return Count;
}