import $ from "jquery";

export class Vector2 { constructor(public x:number, public y:number) { } }
export class Rect
{
	constructor(public x:number, public y:number, public width:number, public height:number) { }
	public SetWidth	(W:number) { this.width =W; return this; }
	public SetHeight(H:number) { this.height=H; return this; }
}

export class ColorRGBA
{
	constructor(public readonly r:number, public readonly g:number, public readonly b:number, public readonly a:number)
	{
		if(
			r<0 || g<0 || b<0 || a<0 ||
			r>1 || g>1 || b>1 || a>1
		)
			throw new Error("Color channels must be in the range 0-1");
	}
	public static Black	=new ColorRGBA(0, 0, 0, 1);
	public static White	=new ColorRGBA(1, 1, 1, 1);
	public static Red	=new ColorRGBA(1, 0, 0, 1);
	public static Green	=new ColorRGBA(0, 1, 0, 1);
	public static Blue	=new ColorRGBA(0, 0, 1, 1);

	private static C01(v:number) { return Math.max(Math.min(v, 1), 0); }
	private static CBy(v:number) { return ColorRGBA.C01(v/255); }
	public static CreateClamp	 (r:number, g:number, b:number, a:number) { return new ColorRGBA(ColorRGBA.C01(r), ColorRGBA.C01(g), ColorRGBA.C01(b), ColorRGBA.C01(a)); }
	public static CreateByteClamp(r:number, g:number, b:number, a:number) { return new ColorRGBA(ColorRGBA.CBy(r), ColorRGBA.CBy(g), ColorRGBA.CBy(b), ColorRGBA.CBy(a)); }
}

export namespace Util
{
	export async function LoadImage(ImageURL:string)
	{
		const LoadImage=new Image();
		await new Promise<void>((Resolve, Reject) => {
			LoadImage.onload=() => Resolve();
			LoadImage.onerror=() => Reject(new Error("Image load failed for:\n"+ImageURL));
			LoadImage.src=ImageURL;
		});
		return await createImageBitmap(LoadImage);
	}

	export function SameType(a:unknown, b:unknown)
	{
		return	a===null || b===null || a===undefined || b===undefined	? a===b
			:	typeof(a)!==typeof(b)									? false
			:	typeof(a)!=="object"									? true
			:							  								a.constructor===(b as object).constructor
	}

	export function TypeName(Val:unknown)
	{
		return	Val===undefined			? "undefined"
			:	Val===null				? "null"
			:	typeof(Val)!=="object"	? typeof(Val)
			:	Array.isArray(Val)		? "Array"
			:	Val.constructor?.name	? Val.constructor.name
			:							  "object";
	}

	export function GetErrorMessage(e:unknown) {
		return	e instanceof Error		? e.message
			:	typeof(e)!=='object'	? String(e)
			:							  JSON.stringify(e);
	}

	export const MaxInt=(1<<30)*2-1;
	export type Primitive=string|number|bigint|boolean|symbol|null|undefined;

	export function OutputException(Name:string, e:unknown)
	{
		Log.Error(`${Name} failed: ${Util.GetErrorMessage(e)}`, e);
	}

	//Sets a member if Obj is not null (used to facilitate C# foo?.bar=baz)
	export function SetNullable<TObj extends object, K extends keyof TObj>(Obj:TObj|undefined|null, Key:K, Value:TObj[K])
	{
		if(Obj!==null && Obj!==undefined)
			Obj[Key]=Value;
	}
}

export namespace Log
{
	export function Info (...Objs:unknown[]) { console.log(...Objs); }
	export function Error(...Objs:unknown[]) { console.log("ERROR", ...Objs); }
}

export namespace DevStrings
{
	const ConvertEl=document.createElement('div');
	export function SafeRich(Str:string)
	{
		ConvertEl.innerText=Str;
		return ConvertEl.innerHTML;
	}
}

export const enum StatStr {
	Empty="",
	NewLine="\n",
}

export class Iter<T> implements Iterable<T>
{
	constructor(private readonly MyIterable:Iterable<T>) { }
	public [Symbol.iterator]() { return this.MyIterable[Symbol.iterator](); }
	public toArray() { return [...this.MyIterable]; }

	public forEach(Fn:(Val:T) => void) {
		for(const Val of this.MyIterable)
			Fn(Val);
	}

	public map<U>(Fn:(Val:T) => U) {
		const Self=this;
		return new Iter<U>(function*() {
			for(const Val of Self.MyIterable)
				yield Fn(Val);
		}());
	}

	public filter(Fn:(Val:T) => boolean) {
		const Self=this;
		return new Iter<T>(function*() {
			for(const Val of Self.MyIterable)
				if(Fn(Val))
					yield Val;
		}());
	}

	public skip(n:number) {
		if(n<=0)
			return this;

		const Self=this;
		return new Iter<T>(function*() {
			let i=0;
			for(const Val of Self.MyIterable)
				if(++i>n)
					yield Val;
		}());
	}
}

export class PopupMessage
{
	private Container=$('<div class=PopupMessage><div><div class=CloseMessage>Click anywhere to close this popup</div><div class=MessageText></div></div></div>').appendTo('body');
	public set Text(Text:string) { this.Container.find(".MessageText").text(Text); }
	constructor(Text:string)
	{
		this.Text=Text;
		this.Container.on('click', () => this.Container.remove());
	}
}

export const WillBeSet=undefined!;