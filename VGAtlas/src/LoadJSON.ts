import { Util } from "./SharedClasses";

export class JsonConverter<ObjType, InVal, RetVal>
{
	constructor(
		public ConvertFunc?:((TheObj:ObjType, Value:InVal) => RetVal)|null,
		public FieldRename?:string
	) { }
}
export type JsonConverter_Generic<ObjType>=JsonConverter<ObjType, unknown, unknown>;

class JSProps { constructor(public IsRequired:boolean=false, public ExpectedType:unknown=undefined) { } }
//eslint-disable-next-line @typescript-eslint/no-extraneous-class
export abstract class JsonClass { public static ClassJSProps:Map<string, JSProps>; }

export namespace LoadJson
{
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

	export function ClassFromObj<ObjType extends object>(Obj:ObjType, ValuesObj:object)
	{
		const ClassJSProps=(Obj.constructor as typeof JsonClass).ClassJSProps as Map<keyof ObjType, JSProps>;
		const Values=ValuesObj as Record<keyof ObjType, unknown>;
		for (let K of Object.keys(Values) as (keyof ObjType)[]) {
			//Skip null values
			let V=Values[K];
			if(V===null)
				continue;

			//Make sure there is a direct class member and the type matches
			if(!(K in Obj))
				continue;
			let ExpectedType=ClassJSProps.get(K)?.ExpectedType;
			ExpectedType ??= Obj[K];
			if(!Util.SameType(ExpectedType, V))
				throw new Error(`Mismatch ${Obj.constructor.name}.${String(K)}: ${Util.TypeName(ExpectedType)}!=${Util.TypeName(V)}`);

			(Obj as Record<keyof ObjType, unknown>)[K]=V;
		}

		//Make sure all the required properties have been set
		for(const [FieldName, JP] of ClassJSProps.entries())
			if(JP.IsRequired && !Values.hasOwnProperty(FieldName))
				throw new Error(`Missing required field ${Obj.constructor.name}.${String(FieldName)}`);

		return Obj;
	}
}

export function JsonPropsDec(IsRequired:boolean=false, ExpectedType:unknown=undefined) {
	return function<T>(Target:T, Name:string) {
		const Ctor=(Target as JsonClass).constructor as typeof JsonClass;
		if(!Object.prototype.hasOwnProperty.call(Ctor, "ClassJSProps")) {
			Object.defineProperty(Ctor, "ClassJSProps", {
				value:new Map<PropertyKey, JSProps>(),
				writable:false, enumerable:false, configurable:false,
			});
		}
		Ctor.ClassJSProps.set(Name, new JSProps(IsRequired, ExpectedType));
	};
}