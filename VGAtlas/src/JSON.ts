import { Util } from "./SharedClasses"
import { Share } from "./Main"

//See ClassFromObj.Converters
export class JsonConverter<
	//None of these are required for a field rename. They can just be set to unknown
	ObjType,//The object type being worked on
	InVal,	//The incoming value type to be converted
	RetVal	//The outgoing value type once conversion is finished. Since we are guaranteeing the type with this, the base member does not need to have a JSProps.ExpectedType
> {
	constructor(
		public ConvertFunc?:((TheObj:ObjType, Value:InVal) => RetVal)|null,
		public FieldRename?:string
	) { }
}
export type JsonConverter_Generic<ObjType>=JsonConverter<ObjType, unknown, unknown>;

//Filled in from decorators on object members
class JSProps
{
	public IsRequired=false; //Required on import
	public ExpectedType?:unknown=undefined; //Must match this type on import. Not needed if value is set by default in class declaration or a JsonConverter was used to make the member

	//Used for exporting fields
	public ExpNo	=false; //Do not include a field
	public ExpYes	=false; //Supposed to be used for including private fields, but as we can’t distinguish private fields, this is just there for reference.
	public NullYes	=false; //Include a field in the output even if its value is null
}
export const JsonPropsDec=(IsRequired:boolean=false, ExpectedType:unknown=undefined)=> (Target:object, Name:string) => SetJSProps(Target, Name, {IsRequired, ExpectedType	});
export const ExpNo	=()																=> (Target:object, Name:string) => SetJSProps(Target, Name, {ExpNo	:true				});
export const ExpYes	=()																=> (Target:object, Name:string) => SetJSProps(Target, Name, {ExpYes	:true				});
export const NullYes=()																=> (Target:object, Name:string) => SetJSProps(Target, Name, {NullYes:true				});
function SetJSProps<K extends keyof JSProps>(Target:object, Name:string, Values:Pick<JSProps, K>)
{
		const Ctor=(Target as JsonClass).constructor as typeof JsonClass;
		if(!Object.prototype.hasOwnProperty.call(Ctor, "ClassJSProps"))
			Object.defineProperty(Ctor, "ClassJSProps", {
				value:new Map<PropertyKey, JSProps>(),
				writable:false, enumerable:false, configurable:false,
			});
		let Props=Ctor.ClassJSProps!.get(Name);
		if(!Props)
			Ctor.ClassJSProps!.set(Name, Props=new JSProps());
		for(const [FieldName, Value] of Object.entries(Values) as [K, JSProps[K]][])
			Props[FieldName]=Value;
}

//Any class that will have JSProp decorators. This is automatically filled in by the system upon handling of the first JSProps decorator in the class
//eslint-disable-next-line @typescript-eslint/no-extraneous-class
export abstract class JsonClass { public static ClassJSProps?:Map<string, JSProps>; }

export namespace LoadJson
{
	//Loads in JSON object from a URL. Allows for JSONC comments and trailing commas
	export async function FromURL(url:string)
	{
		const Result=await fetch(url);
		if(!Result.ok)
			throw new Error(`Failed to load ${url}: ${Result.status}`);
		const Text=await Result.text();
		const Parsed=JSON.parse(Text.replace(/,(\n\t*[}\]])/g, '$1').replace(/^[ \t]*\/\/.*/m, ''));

		if(!(Parsed instanceof Object))
			throw new Error(`Failed to load ${url}: JSON is not an object`);
		return Parsed as object;
	}

	//Convert from imported json (ValuesObj) to the final class based object (Obj). Converters will transform members.
	export function ClassFromObj<ObjType extends object>(Obj:ObjType, ValuesObj:object, Converters?:Record<keyof ObjType, JsonConverter_Generic<ObjType>>)
	{
		const ClassJSProps=(Obj.constructor as typeof JsonClass).ClassJSProps as Map<keyof ObjType, JSProps>|undefined;
		const Values=ValuesObj as Record<keyof ObjType, unknown>;
		for (let K of Object.keys(Values) as (keyof ObjType)[]) {
			//Skip null values
			let V=Values[K];
			if(V===null)
				continue;

			//If there are converters (chained), run them
			let WasConverted=false;
			while(true) {
				const Converter=(Converters ?? ({} as Record<keyof ObjType, JsonConverter_Generic<ObjType>>))[K] as JsonConverter_Generic<ObjType>;

				if(Converter?.ConvertFunc)
					[V, WasConverted]=[Converter.ConvertFunc!(Obj, V), true];
				if(Converter?.FieldRename) //Empty string also ignored
					Values[K=(Converter.FieldRename as keyof ObjType)]='RENAMED_FROM';
				else
					break;
			}

			//Make sure there is a corresponding class member and the type matches
			if(!(K in Obj))
				throw new Error(`Invalid ${Obj.constructor.name} field: ${String(K)}`);
			let ExpectedType=ClassJSProps?.get(K)?.ExpectedType;
			if(ExpectedType!==undefined || !WasConverted) {
				ExpectedType ??= Obj[K];
				if(!Util.SameType(ExpectedType, V))
					throw new Error(`Mismatch ${Obj.constructor.name}.${String(K)}: ${Util.TypeName(ExpectedType)}!=${Util.TypeName(V)}`);
			}

			//Set the value
			(Obj as Record<keyof ObjType, unknown>)[K]=V;
		}

		//Make sure all the required properties have been set
		if(ClassJSProps)
			for(const [FieldName, JP] of ClassJSProps.entries())
				if(JP.IsRequired && !Values.hasOwnProperty(FieldName))
					throw new Error(`Missing required field ${Obj.constructor.name}.${String(FieldName)}`);

		//Return the finalized object
		return Obj;
	}
}

export namespace SaveJson
{
	export interface IExpOverride { readonly ExpOverride:string; } //The class will be serialized through this function on export

	//Internal encoding functions
	const EmptyJP=new JSProps();
	function EncodeObj(InObj:object, OutObj:object)
	{
		const AnyIn =InObj  as Record<string, unknown>;
		const AnyOut=OutObj as Record<string, unknown>;

		const JPs=(InObj.constructor as typeof JsonClass).ClassJSProps;
		function Emit(Name:string)
		{
			//if(IsPrivate && !JP.ExpYes) return; //Unfortunately, private fields cannot be recognized
			const JP=JPs?.get(Name) ?? EmptyJP;
			const Value=AnyIn[Name];
			if(!JP.ExpNo && (Value!==null || JP.NullYes))
				AnyOut[Name]=EncodeVal(Value);
		}

		//Instance fields
		for(const Name of Object.keys(AnyIn))
			Emit(Name);

		//Prototype getters (non-static)
		for(
			let Proto=Object.getPrototypeOf(InObj) as object|null;
			Proto && Proto!==Object.prototype;
				Proto=Object.getPrototypeOf(Proto) as object|null
		)
			for(const [Name, Desc] of Object.entries(Object.getOwnPropertyDescriptors(Proto)))
				if(Name!=="constructor" || Desc.get)
					Emit(Name);

		return OutObj;
	}
	function EncodeVal(Value:unknown): unknown
	{
		return	Value===null || Value===undefined || typeof(Value)!=="object" ? Value
			:	Array.isArray(Value) ? EncodeArr(Value)
			:	Value instanceof Map ? EncodeMap(Value)
			:	EncodeObj(Value as object, Object.create(null));
	}
	function EncodeArr(InArr:unknown[]): unknown[]
	{
		const OutArr:unknown[]=[];
		for(const V of InArr)
			OutArr.push(EncodeVal(V));
		return OutArr;
	}
	function EncodeMap(InMap:Map<unknown, unknown>): object
	{
		const OutObj=Object.create(null) as Record<string, unknown>;
		for(const [K, V] of InMap.entries())
			OutObj[typeof(K)==='number' ? PlaceholderChar+K : String(K)]=EncodeVal(V);
		return OutObj;
	}

	//Exporting functions
	const PlaceholderChar="\uE001";
	//Encodes to JSON. Classes can be handled by IExpOverride; Object fields and getters are handled according to JSProps exporting decorators.
	//Objects maintain their member order. Getters always come after fields. Numeric keys in maps are kept in their original order.
	export function Stringify(Data:unknown, Compact=false, TrailingCommas=true, Replacer?:(this:unknown, key:string, value:unknown) => unknown)
	{
		//The primary encoding process
		let Output=JSON.stringify(EncodeVal(Data), Replacer, Compact ? undefined : "\t").replaceAll(PlaceholderChar, "");

		//Post formatting
		if(TrailingCommas)
			Output=Output.replace(/([^,{[])(\r?\n[ \t]*)(?=[}\]])/g, "$1,$2");

		return Output;
	}

	//Exports DS.Categories and DS.Items through Stringify. If MatchModOutput is enabled, the output from the C# module will be matched exactly.
	export function ExportDefaultData(TrailingCommas=true, Compact=false, MatchModOutput=false)
	{
		const Start=new Date();
		let Output=Stringify({Categories:Share.DS.Categories, Items:Share.DS.Items}, Compact, TrailingCommas, MatchModOutput ? PreFormatLikeMod : undefined);
		if(MatchModOutput)
			Output=PostFormatLikeMod(Output);

		console.log("Time to export: "+(Date.now()-Start.getTime())/1000);
		return Output;
	}

	//Handle encoding certain types
	function PreFormatLikeMod	( Key:string, Value:number		): number|string; //Non-integers are formatted in G17 as a string, turned back into non-strings during post-processing
	function PreFormatLikeMod	( Key:string, Value:IExpOverride): string;
	function PreFormatLikeMod<T>( Key:string, Value:T			): T; //Passes through unchanged
	function PreFormatLikeMod	(_Key:string, Value:unknown		): unknown
	{
		//Pass through types
		if(Value===null || Value===undefined)
			return Value;

		//Non-integers are formatted in G17 as a string, turned back into non-strings during post-processing
		if(typeof(Value)==="number" && Number.isFinite(Value) && !Number.isInteger(Value))
			return ToG17Str(Value);

		//Only handle objects past here
		if(typeof(Value)!=="object")
			return Value;

		//Handle IExpOverride
		if(((V): V is IExpOverride => Object.prototype.hasOwnProperty.call(V, "ExpOverride"))(Value))
			return (Value as IExpOverride).ExpOverride;

		return Value;
	}

	//Convert a double to G17 format
	function ToG17Str(Num:number)
	{
		const Str=Num.toPrecision(17);
		return Str.includes(".") ? Str.replace(/\.?0+$/, "") : Str;
	}

	//Post-encoding fixes
	function PostFormatLikeMod(Str:string)
	{
		Str=Str.replace(/^(\t*"[xy]": )"(.*?)"/gm, '$1$2'); //Doubles were formatted to G17 as string, so revert them

		//Replace anchors with LinkID
		//noinspection RegExpSuspiciousBackref
		Str=Str.replace(
			/<a data-LinkID=(?:\\")?([^ "]+)(?:\\")?(?: data-ItemID=(\d+))? href=\\"(#\2|https:[^\\]+)\\"(?: style=\\"color:(#?\w+)\\")?>(.*?)<\/a>/sg,
			(_Full, LinkID, ItemID, Href, NormalColor, Inner) =>
				`<LinkID=${LinkID}>${ItemID ? `<ATTR=ItemID>${ItemID}</ATTR>` : ""}${Href!=="#"+ItemID ? `<ATTR=href>${Href}</ATTR>` : ""}${NormalColor ? `<ATTR=NormalColor>${NormalColor}</ATTR>` : ""}${Inner}</LinkID>`
		);

		//Replace span+color w/ <color>
		for(let LastStr:string|undefined=undefined; Str!==LastStr; )
			Str=(LastStr=Str).replace(/<span style=\\"color:(#?\w+)\\">((?:(?!<span\b)[\s\S])*?)<\/span>/g, `<color=$1>$2</color>`); //Color spans

		return Str;
	}
}