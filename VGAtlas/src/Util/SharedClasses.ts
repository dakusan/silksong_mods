import $ from 'jquery';
import { type ConfigSerializer } from '../Config/Abstract/ConfigItem';

export interface Equatable<T>
{
	Equals(Other?:T): boolean;
}

export class Vector2 implements Equatable<Vector2>
{
	constructor(public readonly X:number, public readonly Y:number) { }

	public Equals			(Other?:Vector2			): boolean	{ return Other?.X===this.X && Other.Y===this.Y; }
	public Distance			(Vec:Vector2			): number	{ return Vector2.Distance(this, Vec); }
	public static Distance	(a:Vector2, b:Vector2	): number	{ return Math.hypot(a.X-b.X, a.Y-b.Y); }
	public Add				(Vec:Vector2			): Vector2	{ return new Vector2(this.X+Vec.X, this.Y+Vec.Y); }
	public Sub				(Vec:Vector2			): Vector2	{ return new Vector2(this.X-Vec.X, this.Y-Vec.Y); }
	public toString			(						): string	{ return `(${this.X}, ${this.Y})`; }
}

export class Rect implements Equatable<Rect>
{
	constructor(public X:number, public Y:number, public Width:number, public Height:number) { }
	public Equals(Other?:Rect): boolean	{ return Other?.X===this.X && Other.Y===this.Y && Other.Width===this.Width && Other.Height===this.Height; }
	public SetWidth (W:number): this	{ this.Width =W; return this; }
	public SetHeight(H:number): this	{ this.Height=H; return this; }
	public Intersects(R:Rect ): boolean	{ return Rect.Intersects(this, R); }
	public static Intersects(a:Rect, b:Rect): boolean
	{
		return (
			   a.X<b.X+b.Width
			&& b.X<a.X+a.Width
			&& a.Y<b.Y+b.Height
			&& b.Y<a.Y+a.Height
		);
	}
	public toString(): string { return `(${this.X}, ${this.Y}, ${this.Width}, ${this.Height})`; }
}

export class ColorRGBA implements ConfigSerializer<ColorRGBA>, Equatable<ColorRGBA>
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

	private static C01(v:number): number { return Util.Clamp(v, 0, 1); }
	private static CBy(v:number): number { return ColorRGBA.C01(v/255); }
	public static CreateClamp	 (r:number, g:number, b:number, a:number): ColorRGBA { return new ColorRGBA(ColorRGBA.C01(r), ColorRGBA.C01(g), ColorRGBA.C01(b), ColorRGBA.C01(a)); }
	public static CreateByteClamp(r:number, g:number, b:number, a:number): ColorRGBA { return new ColorRGBA(ColorRGBA.CBy(r), ColorRGBA.CBy(g), ColorRGBA.CBy(b), ColorRGBA.CBy(a)); }
	public Equals(Other?:ColorRGBA): boolean { return Other?.r===this.r && Other.g===this.g && Other.b===this.b && Other.a===this.a; }

	public ConfigSerialize(): string
	{
		return StatStr.Empty
			+CToHex(this.r)
			+CToHex(this.g)
			+CToHex(this.b)
			+CToHex(this.a);
	}
	public ConfigDeserialize(Str:string): ColorRGBA
	{
		Str=(/^([0-9a-fA-F]{6,8})$/.test(Str) ? Str : this.ConfigSerialize()).padEnd(8, 'F').toUpperCase();

		return new ColorRGBA(
			HexToC(Str, 0),
			HexToC(Str, 1),
			HexToC(Str, 2),
			HexToC(Str, 3),
		);
	}
}
function CToHex(C:number): string { return Math.round(C*255).toString(16).toUpperCase().padStart(2, '0'); }
function HexToC(Str:string, Pos:number): number { return Number.parseInt(Str.substring(Pos*2, Pos*2+2), 16)/255; }

export namespace Util
{
	export async function LoadImage(ImageURL:string): Promise<ImageBitmap>
	{
		const LoadImage=new Image();
		await new Promise<void>((Resolve, Reject) => {
			LoadImage.onload=() => Resolve();
			LoadImage.onerror=() => Reject(new Error("Image load failed for:"+StatStr.NewLine+ImageURL));
			LoadImage.src=ImageURL;
		});
		return await createImageBitmap(LoadImage);
	}

	export function SameType(a:unknown, b:unknown): boolean
	{
		return	a===null || b===null || a===undefined || b===undefined	? a===b
			:	typeof(a)!==typeof(b)									? false
			:	typeof(a)!=='object'									? true
			:							  								a.constructor===(b as object).constructor
	}

	export function TypeName(Val:unknown): string
	{
		return	Val===undefined			? 'undefined'
			:	Val===null				? 'null'
			:	typeof(Val)!=='object'	? typeof(Val)
			:	Array.isArray(Val)		? 'Array'
			:	Val.constructor?.name	? Val.constructor.name
			:							  'object';
	}

	export function GetErrorMessage(e:unknown): string
	{
		return	e instanceof Error		? e.message
			:	typeof(e)!=='object'	? String(e)
			:							  JSON.stringify(e);
	}

	export const MaxInt=(1<<30)*2-1;
	export type Primitive=string|number|bigint|boolean|symbol|null|undefined;

	export function OutputException(Name:string, e:unknown): void
	{
		Log.Error(StatStr.NeedsTranslate+`${Name} failed: ${Util.GetErrorMessage(e)}`, e);
	}

	//Sets a member if Obj is not null (used to facilitate C# foo?.bar=baz)
	export function SetNullable<TObj extends object, K extends keyof TObj>(Obj:TObj|undefined|null, Key:K, Value:TObj[K]): void
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
	//If SetOnEqual is false, only copies members that are different in Src and Target (using !== as equality check).
	export function AssignProps<T extends object>(Target:T, Src:Partial<T>, SetOnEqual=true): T
	{
		for(const K in Src)
		{
			const KK=K as keyof T;
			const V=Src[KK];
			if(V!==undefined && (SetOnEqual || Target[KK]!==V))
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

	export function IsMobileWidth(): boolean
	{
		let MobileWidth=Number.parseInt(getComputedStyle(document.documentElement).getPropertyValue('--mobile-width'));
		if(!Number.isFinite(MobileWidth))
			MobileWidth=599;
		return window.outerWidth<=MobileWidth;
	}

	export function IsMobile(): boolean { return matchMedia('(pointer:coarse)').matches; }

	//Returns a number only when the trimmed string is a valid floating point number; otherwise, returns null
	//If a decimal point is present, at least one digit must follow it
	//If AsInt is truthy, truncates the finite number; it does not require the input to be an integer
	export function GetNumber(Str:string|null|undefined, AsInt=false): number|null
	{
		if(typeof(Str)!=='string' || !TestNumberRegEx.test(Str))
			return null;
		const Num=Number(Str);
		return (
			  !Number.isFinite(Num)	? null
			: AsInt					? Math.trunc(Num)
			:						  Num
		);
	}
	const TestNumberRegEx=/^\s*[+-]?(?:\d+|\d*\.\d+)(?:[eE][+-]?\d+)?\s*$/;

	//Returns a number only when the trimmed string is a base-10 integer with optional +/- sign.
	//Value must be within ±(2^53-1) (Number.MAX_SAFE_INTEGER to Number.MIN_SAFE_INTEGER)
	export function GetInt(Str:string|null|undefined): number|null
	{
		if(typeof(Str)!=='string' || !TestIntRegEx.test(Str))
			return null;
		const Num=+Str;
		return Number.isSafeInteger(Num) ? Num : null;
	}
	const TestIntRegEx=/^\s*[+-]?\d+\s*$/;

	//This function helps guard against HMR graph version updates
	export function OneTimeInit<T>(Name:string, InitVal:() => T): T
	{
		const Glob=globalThis as typeof globalThis & Record<symbol, T|undefined>;
		return Glob[Symbol.for('__OneTimeInit__'+Name)] ??= InitVal();
	}

	export type Mutable<T>={ -readonly [K in keyof T]: T[K]; };
	export function GetMutable<T>(Obj:T): Mutable<T> { return Obj as Mutable<T>; }
}

class LogLine
{
	public readonly Time=new Date();
	constructor(public readonly LogInfo:unknown[], public readonly IsError:boolean) {}
}
class LogClass
{
	private LogLines:LogLine[]=[];
	public get AllLogLines():readonly LogLine[] { return this.LogLines; }
	public readonly OnLog=new CallbackList<[LogLine]>('OnLog');
	public MaxStoredLogLines=0; //LogLines not shortened until next Add()
	private Add(Info:unknown[], IsError:boolean): unknown[] //Returns Info
	{
		if(this.LogLines.length>this.MaxStoredLogLines-1)
			this.LogLines=this.LogLines.slice(-(this.MaxStoredLogLines-1));
		const NewLine=new LogLine(Info, IsError);
		this.LogLines.push(NewLine);
		this.OnLog.Execute(NewLine);
		return Info;
	}

	public Debug(...Objs:unknown[]): void { console.debug	(...Objs); }
	public Info (...Objs:unknown[]): void { console.info	(...this.Add(Objs, false)); }
	public Error(...Objs:unknown[]): void { console.error	(...this.Add(Objs, true )); }
}

export namespace DevStrings
{
	const ConvertEl=document.createElement('div');
	export function SafeRich(Str:string): string
	{
		ConvertEl.innerText=Str;
		return ConvertEl.innerHTML;
	}
	export function HtmlToText(Html:string): string
	{
		ConvertEl.innerHTML=Html;
		return ConvertEl.innerText;
	}
}

export const enum StatStr {
	Empty='',
	NewLine='\n',
	//eslint-disable-next-line @typescript-eslint/no-duplicate-enum-values
	NeedsTranslate='',
	PrivateChar='\uE000', //First character in private use area
}

export class Iter<T> implements Iterable<T>
{
	constructor(private readonly MyIterable:Iterable<T>) { }
	public [Symbol.iterator]() { return this.MyIterable[Symbol.iterator](); }
	public toArray(): T[] { return [...this.MyIterable]; }

	private static MakeIter<U>(Fn:() => IterableIterator<U>): Iter<U> { return new Iter<U>({ [Symbol.iterator]: Fn }); }

	public forEach(Fn:(Val:T) => void): void
	{
		for(const Val of this.MyIterable)
			Fn(Val);
	}

	public map<U>(Fn:(Val:T) => U): Iter<U>
	{
		const Self=this;
		return Iter.MakeIter(function*() {
			for(const Val of Self.MyIterable)
				yield Fn(Val);
		});
	}

	public filter(Fn:(Val:T) => boolean): Iter<T>
	{
		const Self=this;
		return Iter.MakeIter(function*() {
			for(const Val of Self.MyIterable)
				if(Fn(Val))
					yield Val;
		});
	}

	public skip(n:number): Iter<T>
	{
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

	public take(n:number): Iter<T>
	{
		if(n<=0)
			return this;

		const Self=this;
		return Iter.MakeIter(function*() {
			let i=0;
			for(const Val of Self.MyIterable)
				if(++i<=n)
					yield Val;
				else
					break;
		});
	}

	public concat(...Items:Iterable<T>[]): Iter<T>
	{
		const Self=this;
		return Iter.MakeIter(function*() {
			yield* Self.MyIterable;
			for(const I of Items)
				yield* I;
		});
	}

	public every(Fn:(Val:T) => boolean): boolean
	{
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

	private _HTMLContent:string=WillBeSet; public get HTMLContent(): string { return this._HTMLContent; }
	public set Text(Contents:string) { this.HTML=DevStrings.SafeRich(Contents); }
	public set HTML(Contents:string) { this.MessageTextEl.children().eq(0).html(this._HTMLContent=Contents); this.ReadjustSize(); }

	constructor(TextOrHTML:string, IsHTML=false)
	{
		this[!IsHTML ? 'Text' : 'HTML']=TextOrHTML;
		this.Container.on('click', () => this.Close());
		PopupMessage.PopupMessages.set(this.Container[0], this);
		PopupMessage.Observer.observe(this.Container[0]);
	}

	public Close(): void
	{
		if(this.HasClosed)
			return;
		this.HasClosed=true;

		PopupMessage.Observer.unobserve(this.Container[0]);
		PopupMessage.PopupMessages.delete(this.Container[0]);
		this.Container.remove()
	}

	private ReadjustSize(): void
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

type Callback<Args extends unknown[]=unknown[], TRet=void> = (...args: Args) => TRet;
export class CallbackList<Args extends unknown[], TRet=void>
{
	constructor(
		public readonly Name:string,
	) { }

	private readonly Callbacks=new Map<string, Callback<Args, TRet>>();
	public Add(Name:string, CB:Callback<Args, TRet>, InsertBefore?:string): void
	{
		if(this.Has(Name))
			throw new Error("Callback already exists: "+Name);
		if(!InsertBefore || !this.Callbacks.has(InsertBefore))
			return void(this.Callbacks.set(Name, CB));

		const MapClone=[...this.Callbacks.entries()];
		this.Callbacks.clear();
		for(const [Key, Value] of MapClone) {
			if(Key===InsertBefore)
				this.Callbacks.set(Name, CB);
			this.Callbacks.set(Key, Value);
		}
	}
	public Remove	(Name:string							): boolean { return	this.Callbacks.delete	(Name		); }
	public Has		(Name:string							): boolean { return	this.Callbacks.has		(Name		); }
	public Execute(...Params:Args): void
	{
		for(const [CBName, CB] of this.Callbacks.entries())
			try { CB(...Params); }
			catch(e) { Log.Error(StatStr.NeedsTranslate+`Callback “${CBName}” for ${this.Name} failed: ${Util.GetErrorMessage(e)}`); }
	}

	//RetCB is called with each callback’s return value. If RetCB returns true, execution stops and this method returns true. Otherwise, it continues and returns false if all callbacks ran.
	public ExecuteWithRetCB(RetCB:(RetVal:TRet) => boolean, ...Params:Args): boolean
	{
		for(const [CBName, CB] of this.Callbacks.entries())
			try {
				//noinspection PointlessBooleanExpressionJS :: Need to actually confirm the result is true and not just truthy
				if(RetCB(CB(...Params))===true)
					return true;
			}
			catch(e) { Log.Error(StatStr.NeedsTranslate+`Callback “${CBName}” for ${this.Name} failed: ${Util.GetErrorMessage(e)}`); }
		return false;
	}
}

export class PreallocatedPusher<T>
{
	private readonly Arr: T[];
	private Len=0;
	constructor(Capacity:number) { this.Arr=new Array<T>(Capacity); }
	public push(Item:T): void
	{
		if(this.Len<this.Arr.length)
			this.Arr[this.Len]=Item;
		else
			this.Arr.push(Item);
		this.Len++;
	}
	public get length		(				): number	{ return this.Len; }
	public get raw			(				): T[]		{ return this.Arr; }
	public get finalize		(				): T[]		{ this.Arr.length=this.Len; return this.Arr; }
	public get FinalizeSlice(				): T[]		{ return this.Arr.slice(0, this.Len); }
	public Reset			(Capacity:number): void		{ this.Arr.length=Capacity; this.Len=0; }
	public ResetLen			(				): void		{ this.Len=0; }
}

export namespace KeyState
{
	const Keys=new Map<string, boolean>();
	window.addEventListener('keydown', e => Keys.set(e.code, true ), { passive:false });
	window.addEventListener('keyup'  , e => Keys.set(e.code, false), { passive:false });
	export function GetKeyDown(Name:string): boolean { return Keys.get(Name) ?? false; }
}

export const WillBeSet=undefined!;
export const Log=Util.OneTimeInit('Log', () => new LogClass());

//These run at the end of initialization
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