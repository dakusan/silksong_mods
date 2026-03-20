import $ from 'jquery';
import { type SaveAsString } from '../Config/Abstract/ConfigItem';

export class Vector2
{
	constructor(public X:number, public Y:number) { }
	public Distance(Vec:Vector2) { return Vector2.Distance(this, Vec); }
	public static Distance(a:Vector2, b:Vector2) { return Math.hypot(a.X-b.X, a.Y-b.Y); }
	public Add(Vec:Vector2) { return new Vector2(this.X+Vec.X, this.Y+Vec.Y); }
	public Sub(Vec:Vector2) { return new Vector2(this.X-Vec.X, this.Y-Vec.Y); }
}

export class Rect
{
	constructor(public X:number, public Y:number, public Width:number, public Height:number) { }
	public Equals(Other?:Rect) { return Other?.X===this.X && Other.Y===this.Y && Other.Width===this.Width && Other.Height===this.Height; }
	public SetWidth	(W:number) { this.Width =W; return this; }
	public SetHeight(H:number) { this.Height=H; return this; }
	public Intersects(R:Rect) { return Rect.Intersects(this, R); }
	public static Intersects(a:Rect, b:Rect)
	{
		return (
			   a.X<b.X+b.Width
			&& b.X<a.X+a.Width
			&& a.Y<b.Y+b.Height
			&& b.Y<a.Y+a.Height
		);
	}
}

export class ColorRGBA implements SaveAsString<ColorRGBA>
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

	private static C01(v:number) { return Util.Clamp(v, 0, 1); }
	private static CBy(v:number) { return ColorRGBA.C01(v/255); }
	public static CreateClamp	 (r:number, g:number, b:number, a:number) { return new ColorRGBA(ColorRGBA.C01(r), ColorRGBA.C01(g), ColorRGBA.C01(b), ColorRGBA.C01(a)); }
	public static CreateByteClamp(r:number, g:number, b:number, a:number) { return new ColorRGBA(ColorRGBA.CBy(r), ColorRGBA.CBy(g), ColorRGBA.CBy(b), ColorRGBA.CBy(a)); }

	public ToString()
	{
		return StatStr.Empty
			+CToHex(this.r)
			+CToHex(this.g)
			+CToHex(this.b)
			+CToHex(this.a);
	}
	public FromString(Str:string)
	{
		Str=(/^([0-9a-fA-F]{6,8})$/.test(Str) ? Str : this.ToString()).padEnd(8, 'F').toUpperCase();

		return new ColorRGBA(
			HexToC(Str, 0),
			HexToC(Str, 1),
			HexToC(Str, 2),
			HexToC(Str, 3),
		);
	}
}
function CToHex(C:number) { return Math.round(C*255).toString(16).toUpperCase().padStart(2, '0'); }
function HexToC(Str:string, Pos:number) { return Number.parseInt(Str.substring(Pos*2, Pos*2+2), 16)/255; }

export namespace Util
{
	export async function LoadImage(ImageURL:string)
	{
		const LoadImage=new Image();
		await new Promise<void>((Resolve, Reject) => {
			LoadImage.onload=() => Resolve();
			LoadImage.onerror=() => Reject(new Error("Image load failed for:"+StatStr.NewLine+ImageURL));
			LoadImage.src=ImageURL;
		});
		return await createImageBitmap(LoadImage);
	}

	export function SameType(a:unknown, b:unknown)
	{
		return	a===null || b===null || a===undefined || b===undefined	? a===b
			:	typeof(a)!==typeof(b)									? false
			:	typeof(a)!=='object'									? true
			:							  								a.constructor===(b as object).constructor
	}

	export function TypeName(Val:unknown)
	{
		return	Val===undefined			? 'undefined'
			:	Val===null				? 'null'
			:	typeof(Val)!=='object'	? typeof(Val)
			:	Array.isArray(Val)		? 'Array'
			:	Val.constructor?.name	? Val.constructor.name
			:							  'object';
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
		Log.Error(StatStr.NeedsTranslate+`${Name} failed: ${Util.GetErrorMessage(e)}`, e);
	}

	//Sets a member if Obj is not null (used to facilitate C# foo?.bar=baz)
	export function SetNullable<TObj extends object, K extends keyof TObj>(Obj:TObj|undefined|null, Key:K, Value:TObj[K])
	{
		if(Obj!==null && Obj!==undefined)
			Obj[Key]=Value;
	}

	export function ThrowOnNull<T>(Val:T|undefined|null, Err:string): T
	{
		if(Val===undefined || Val===null)
			throw new Error(Err);
		return Val;
	}

	//Copies members from Src to Target. Does not copy undefined values.
	//Compile-time checks require that Src member types match the corresponding T member types.
	//If the Src object literal is created directly in the function call, it also checks at compile time if there are extra members in Src that do not exist in T.
	export function AssignProps<T extends object>(Target:T, Src:Partial<T>): T
	{
		for(const K in Src)
		{
			const KK=K as keyof T;
			const V=Src[KK];
			if(V!==undefined)
				Target[KK]=V;
		}
		return Target;
	}

	export function Clamp(n:number, min:number, max:number): number
	{
		return	n<min ? min
			:	n>max ? max
			:			n;
	}
}

export namespace Log
{
	export function Debug(...Objs:unknown[]) { console.debug(...Objs); }
	export function Info (...Objs:unknown[]) { console.info	(...Objs); }
	export function Error(...Objs:unknown[]) { console.error(...Objs); }
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
	Empty='',
	NewLine='\n',
	//eslint-disable-next-line @typescript-eslint/no-duplicate-enum-values
	NeedsTranslate='',
}

export class Iter<T> implements Iterable<T>
{
	constructor(private readonly MyIterable:Iterable<T>) { }
	public [Symbol.iterator]() { return this.MyIterable[Symbol.iterator](); }
	public toArray() { return [...this.MyIterable]; }

	private static MakeIter<U>(Fn:() => IterableIterator<U>) { return new Iter<U>({ [Symbol.iterator]: Fn }); }

	public forEach(Fn:(Val:T) => void) {
		for(const Val of this.MyIterable)
			Fn(Val);
	}

	public map<U>(Fn:(Val:T) => U) {
		const Self=this;
		return Iter.MakeIter(function*() {
			for(const Val of Self.MyIterable)
				yield Fn(Val);
		});
	}

	public filter(Fn:(Val:T) => boolean) {
		const Self=this;
		return Iter.MakeIter(function*() {
			for(const Val of Self.MyIterable)
				if(Fn(Val))
					yield Val;
		});
	}

	public skip(n:number) {
		if(n<=0)
			return this;

		const Self=this;
		return Iter.MakeIter(function*() {
			let i=0;
			for(const Val of Self.MyIterable)
				if(++i>n)
					yield Val;
		});
	}

	public every(Fn:(Val:T) => boolean) {
		for(const Val of this.MyIterable)
			if(!Fn(Val))
				return false;
		return true;
	}
}

export class PopupMessage
{
	private static PopupMessages=new Map<Element, PopupMessage>();
	private static Observer=new ResizeObserver(Entries => Entries.forEach(Entry => this.PopupMessages.get(Entry.target)?.ReadjustSize()));
	private readonly Container=$('<div class=PopupMessage><div><div class=CloseMessage>'+"Click anywhere to close this popup"+'</div><div class=MessageText><div></div></div></div></div>').appendTo('body');
	private readonly MessageTextEl=this.Container.find('.MessageText'); //Note: There is an extra div under this element that actually receives the text
	private readonly StartTextSize=parseFloat(this.MessageTextEl.css('font-size')) || 80;
	private HasClosed=false;

	private _HTMLContent:string=WillBeSet; public get HTMLContent() { return this._HTMLContent; }
	public set Text(Contents:string) { this.HTML=DevStrings.SafeRich(Contents); }
	public set HTML(Contents:string) { this.MessageTextEl.children().eq(0).html(this._HTMLContent=Contents); this.ReadjustSize(); }

	constructor(TextOrHTML:string, IsHTML=false)
	{
		this[!IsHTML ? 'Text' : 'HTML']=TextOrHTML;
		this.Container.on('click', () => this.Close());
		PopupMessage.PopupMessages.set(this.Container[0], this);
		PopupMessage.Observer.observe(this.Container[0]);
	}

	public Close()
	{
		if(this.HasClosed)
			return;
		this.HasClosed=true;

		PopupMessage.Observer.unobserve(this.Container[0]);
		PopupMessage.PopupMessages.delete(this.Container[0]);
		this.Container.remove()
	}

	private ReadjustSize()
	{
		const Parent=this.MessageTextEl[0];
		const El=Parent.firstElementChild as HTMLElement;
		let Min=10, Max=this.StartTextSize;
		while(Min<=Max) {
			const Mid=Math.floor((Min+Max)/2);
			El.style.fontSize=Mid+'px';
			if(El.scrollWidth>Parent.clientWidth || El.scrollHeight>Parent.clientHeight)
				Max=Mid-1;
			else
				Min=Mid+1;
		}
		El.style.fontSize=Max+'px';
	}
}

type Callback<Args extends unknown[]=unknown[]> = (...args: Args) => void;
export class CallbackList<Args extends unknown[]>
{
	constructor(
		public readonly Name:string,
	) { }

	private readonly Callbacks=new Map<string, Callback<Args>>();
	public Add		(Name:string, CB:Callback<Args>	) {			this.Callbacks.set		(Name, CB	); }
	public Remove	(Name:string					) { return	this.Callbacks.delete	(Name		); }
	public Has		(Name:string					) { return	this.Callbacks.has		(Name		); }
	public Execute(...Params:Args)
	{
		for(const [CBName, CB] of this.Callbacks.entries())
			try { CB(...Params); }
			catch(e) { Log.Error(StatStr.NeedsTranslate+`Callback “${CBName}” for ${this.Name} failed: ${Util.GetErrorMessage(e)}`); }
	}
}

export namespace KeyState
{
	const Keys=new Map<string, boolean>();
	window.addEventListener('keydown', e => Keys.set(e.code, true ), { passive:false });
	window.addEventListener('keyup'  , e => Keys.set(e.code, false), { passive:false });
	export function GetKeyDown(Name:string) { return Keys.get(Name) ?? false; }
}

export const WillBeSet=undefined!;

//These are ran at the end of initialization
export const InitFuncs:(() => void)[]=[];

/*
```
Mimic C++ friend / C# internal.
Friend classes allow outside classes to access protected members on base classes via fake typecasting stubs.

Friend classes must be abstract and implement FriendClass.
The constructor must call Stub().
All [stub] members must:
	- Be “override”
	- Have no defaults in parameters
	- Call Stub()
NEVER extend the friend class. Consider it sealed.
Static functions WILL be called and must be passed through

Example usage:
class Foo {
	protected Bar:SomeClass=new SomeClass();
	protected Baz(Apple:number, Pear:string, Lemon:string='Sour'): number { return Apple+Pear.length+Lemon.length; }
	protected static Moo(V:number): number { return V+100; }
	private Cow() { } //Cannot be friended!
}
abstract class Friend_Foo extends FooParent implements FriendClass {
	public override Bar:SomeClass;
	public override Baz(Apple:number, Pear:string, Lemon:string): number { return this.Stub(-100); }
	public static Moo(V:number): number { return super.Moo(V); }

	protected constructor() { super(); this.Stub(); }
	public Stub<T>(_V?:T): T { throw new Error('This function is a stub'); }
}
function Example() {
	const Ex=new Foo();
	console.log((Ex as Friend_Foo).Baz(5, 'Green', 'Yellow')); //Logs 16
	console.log(Friend_Foo.Moo(20); //Logs 120
}
```

<b>This is a type-system escape hatch. It does not grant runtime encapsulation. It can make code type-check while throwing at runtime if misused.</b>
*/
export interface FriendClass { Stub<T>(_V?:T): T; } // { throw new Error('This function is a stub'); }