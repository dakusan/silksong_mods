import { ColorRGBA, Equatable, InitFuncs, Log, Rect, Util, Vector2, WillBeSet } from './Util/SharedClasses';
import { HSVAShader, RGBAShader, TintShader } from './Util/PixelShader';
import { Share } from './Share';
import { CategoryToggleState, Item } from './CategoriesAndItems';

class SpriteRenderInfo { constructor(public readonly SSV:SpriteSheetVariations, public readonly ImageRect:Rect, public readonly Center:Vector2) { } }

type ExtraDrawFunc=(Ctx:CanvasRenderingContext2D, CanvasRect:Rect) => void;

//Keep a counter of when things change so users of the object know to refresh its render
abstract class Versioned
{
	private Version=0;
	public get CurrentVersion() { return this.Version; }
	public IncVersion() { this.Version++; Share.MCanvas.Refresh(); }

	protected RefreshCanvas() { Share.MCanvas.Refresh(); }
	protected OnChange() { this.IncVersion(); }
}

export class Sprite extends Versioned
{
	private static ErrImage?:SpriteSheetVariations;
	static { void Sprite.CreateErrorImage().catch(() => {}); }
	private static async CreateErrorImage()
	{
		//noinspection SpellCheckingInspection
		const Res=await fetch('data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAADcAAAA2AQMAAABZSZOgAAAABlBMVEX/ANwAAAAtIRQiAAAAIElEQVR4AWNgsH/A/4P5H8X0/wPyH9j/MFBKj7oHPw0AR228KREmCC0AAAAASUVORK5CYII=');
		const NewErrImage=new SpriteSheetVariations();
		await NewErrImage.Update(await createImageBitmap(await Res.blob()));
		this.ErrImage=NewErrImage;
	}

	public get SSV		() { return this._SSV		; }; public set SSV		 (Val) { this._SSV		=Val; this.OnChange(); }
	public get ImageRect() { return this._ImageRect	; }; public set ImageRect(Val) { this._ImageRect=Val; this.OnChange(); }
	public get Center	() { return this._Center	; }; public set Center	 (Val) { this._Center	=Val; this.OnChange(); }
	constructor(private _SSV:SpriteSheetVariations|undefined, private _ImageRect:Rect, private _Center:Vector2) { super(); }

	public GetRenderInfo(): SpriteRenderInfo|undefined
	{
		return	this.SSV?.HasImage	? new SpriteRenderInfo(this.SSV, this.ImageRect, this.Center)
			:	Sprite.ErrImage		? new SpriteRenderInfo(Sprite.ErrImage, new Rect(0, 0, Sprite.ErrImage.IB!.width, Sprite.ErrImage.IB!.height), new Vector2(0.5, 0.5))
			:						  undefined;
	}
}

//type UpdatableSettableFields=Partial<Pick<GameObject, '_Color'|'_Active'|'_LocalScale'|'_Pos'>>; //Unfortunately, cannot use this style on private fields
type UpdatableSettableFields={
	_SSVVar?:	SSVVar;
	_Active:	boolean;
	_LocalScale:Vector2;
	_Pos:		Vector2;
};

//Emulates a UnityObject sprite
class GameObject extends Versioned
{
	private static OrderedGameObjectList:GameObject[]=[];
	private static DrawAll(Ctx:CanvasRenderingContext2D) { Log.Debug("Drawing complete. Icons rendered: "+
		GameObject.OrderedGameObjectList.map(GO => GO.Draw(Ctx)).filter(B => B).length
	); }
	public static get AllObjects(): readonly GameObject[] { return GameObject.OrderedGameObjectList; }
	private static InitClass()
	{
		Share.MCanvas.Events.Draw.Add('GameObjects', GameObject.DrawAll);
	}
	static { InitFuncs.push(GameObject.InitClass); }

	private UpdateSettableVal<K extends keyof UpdatableSettableFields>(ValName:K, NewVal:UpdatableSettableFields[K], Callback:() => void)
	{
		const Self=this as GameObject & UpdatableSettableFields;
		if(
			   Self[ValName]===NewVal
			|| (Self[ValName] as Equatable<UpdatableSettableFields[K]|undefined>)?.Equals?.(NewVal)
		)
			return;

		(Self as {[P in K]: UpdatableSettableFields[P]})[ValName]=NewVal;
		Callback.call(this);
	}
	private _SSVVar:SSVVar		=SSVDefault;public get SSVVar		() { return this._SSVVar	; }; public set SSVVar		(Val) { this.UpdateSettableVal('_SSVVar',		Val, this.OnChange		); }
	private _Active				=true	  ; public get Active		() { return this._Active	; }; public set Active		(Val) { this.UpdateSettableVal('_Active',		Val, this.RefreshCanvas	); }
	private _LocalScale:Vector2	=WillBeSet; public get LocalScale	() { return this._LocalScale; }; public set LocalScale	(Val) { this.UpdateSettableVal('_LocalScale',	Val, this.RefreshCanvas	); }
	//noinspection TypeScriptFieldCanBeMadeReadonly :: Field is set in Pos.setter→UpdateVal
	private _Pos:Vector2				  ; public get Pos			() { return this._Pos		; }; public set Pos			(Val) { this.UpdateSettableVal('_Pos',			Val, this.RefreshCanvas	); }

	private SpriteVersion=-1; private RenderedVersion=-1;
	private SRI?:SpriteRenderInfo=undefined;
	private RenderedSprite:OffscreenCanvas=WillBeSet;

	constructor(public Title:string, Pos:Vector2, private MySprite:Sprite, private ExtraDraw?:ExtraDrawFunc)
	{
		super();
		this._Pos=Pos;
		GameObject.OrderedGameObjectList.push(this);
	}

	public BringToFront()
	{
		const Arr=GameObject.OrderedGameObjectList;
		Arr.push(Arr.splice(Arr.indexOf(this), 1)[0]);
		Share.MCanvas.Refresh();
	}

	private ReRender()
	{
		//Type guard
		if(this.SRI===undefined)
			return;

		//Log.Debug("Rerendering: "+this.Title);
		this.RenderedSprite=this.SRI.SSV.Vars[this.SSVVar].Canvas!;
	}

	public Draw(Ctx:CanvasRenderingContext2D): boolean
	{
		//Make sure the sprite info is up to date
		if(this.MySprite.CurrentVersion>this.SpriteVersion) {
			this.SpriteVersion=this.MySprite.CurrentVersion;
			this.SRI=this.MySprite.GetRenderInfo();
			this.IncVersion();
		}
		if(this.SRI===undefined || !this.Active)
			return false;

		//Determine if the sprite will be displayed on the screen
		const R=this.RenderRect!;
		if(!R.Intersects(new Rect(0, 0, Share.MCanvas.Width, Share.MCanvas.Height)))
			return false;

		//Rerender when needed
		if(this.RenderedVersion<this.CurrentVersion) {
			this.RenderedVersion=this.CurrentVersion;
			this.ReRender();
		}

		//Draw to the canvas
		const IR=this.SRI.ImageRect;
		Ctx.drawImage(
			this.RenderedSprite, IR.X, IR.Y, IR.Width, IR.Height,
			R.X, R.Y, R.Width, R.Height
		);
		this.ExtraDraw?.(Ctx, R);

		return true;
	}

	public get RenderRect(): Rect|undefined
	{
		if(this.SRI===undefined)
			return undefined;
		const RenderWidth =this.SRI.ImageRect.Width *this.LocalScale.X;
		const RenderHeight=this.SRI.ImageRect.Height*this.LocalScale.Y;
		const MapPos:Util.Mutable<Vector2>=Share.MCanvas.MapToCanvas(this.Pos);
		MapPos.X-=RenderWidth *this.SRI.Center.X;
		MapPos.Y-=RenderHeight*this.SRI.Center.Y;
		return new Rect(MapPos.X, MapPos.Y, RenderWidth, RenderHeight);
	}

	public SpriteUpdated() { this.MySprite.IncVersion(); }

	public Delete() {
		const Index=GameObject.OrderedGameObjectList.indexOf(this);
		if(Index!==-1)
			GameObject.OrderedGameObjectList.splice(Index, 1);
		return Index!==-1;
	}
}

//The physical map icons to display on the canvas
export class MapIcon
{
	//Basic instance members
	private readonly IconGO:GameObject;

	constructor(Item:Item, MySprite:Sprite, ExtraDraw?:ExtraDrawFunc)
	{
		this.IconGO=new GameObject(`Pin - ${Item.Title} [${Item.ID}]`, Item.Pos, MySprite, ExtraDraw);
		this.UpdateSize(0.75*2/3); //Use an arbitrary start size which will be reset upon MapControl load
	}

	private _CTS=CategoryToggleState.Unknown;
	public get CTS() { return this._CTS; }
	public set CTS(NewState)
	{
		if(this._CTS===NewState || NewState===CategoryToggleState.Unknown)
			return;

		this._CTS=NewState;
		this.UpdateActive();
	}

	private UpdateActive(_:boolean=true)
	{
		this.IconGO.Active=(
			    this.CTS===CategoryToggleState.All
			|| (this.CTS===CategoryToggleState.Incomplete && !this.IsFound)
		);
		this.SetIconColor();
	}

	private _IsFound			=false; public get IsFound	 () { return this._IsFound		; } public set IsFound		(Val) { if(this._IsFound	!==Val) this.UpdateActive (this._IsFound	=Val); }
	private _IsHovered			=false; public get IsHovered () { return this._IsHovered	; } public set IsHovered	(Val) { if(this._IsHovered	!==Val) this._SetIconColor(this._IsHovered	=Val); }
	private _IsSelected			=false; public get IsSelected() { return this._IsSelected	; } public set IsSelected	(Val) { if(this._IsSelected	!==Val) this._SetIconColor(this._IsSelected	=Val); }
	private _IsLinked			=false; public get IsLinked	 () { return this._IsLinked		; } public set IsLinked		(Val) { if(this._IsLinked	!==Val) this._SetIconColor(this._IsLinked	=Val); }
	private _SetIconColor(_:boolean) { this.SetIconColor(); }

	public SetIconColor()
	{
		this.IconGO.SSVVar=
			  this.IsSelected								? 'Green'
			: this.IsHovered								? 'Blue'
			: Share.MC?.ShowLinkedStatus && !this.IsLinked	? 'Red'
			: this.IsFound									? 'IsFound'
			:												  SSVDefault;
	}

	//Material shader for HSV conversion
	static {
		setTimeout(() => {
			MapIcon.UpdateShaderColor(Share.LC.Color_FoundIcon.V);
		}, 0);
		Share.LC.Color_FoundIcon.SettingChanged.Add('MapIcon.UpdateShaderColor', MapIcon.UpdateShaderColor.bind(this));
	}
	private static UpdateShaderColor_TimeoutHandle?:number;
	private static UpdateShaderColor(C:ColorRGBA)
	{
		const MyShader=DefaultSSV.Vars.IsFound.Shaders[0] as HSVShader;
		MyShader.Hue	=C.r-0.5;
		MyShader.Sat	=C.g*2	;
		MyShader.Val	=C.b*2	;
		MyShader.Alpha	=C.a	;

		function DoRerender()
		{
			DefaultSSV.Rerender('IsFound');
			for(const I of Share.DS?.Items.values() ?? [])
				if(I.MapIcon?.IconGO.SSVVar==='IsFound')
					I.MapIcon?.IconGO.IncVersion();
		}
		if(!MyShader.IsUsingCPUShader)
			return DoRerender();

		//If using CPU shader, give a 1.5 second timeout before running the update
		clearTimeout(this.UpdateShaderColor_TimeoutHandle);
		this.UpdateShaderColor_TimeoutHandle=setTimeout(() => {
			this.UpdateShaderColor_TimeoutHandle=undefined;
			DoRerender();
		}, 1500);
	}

	public static UpdateDefaultSpriteSheet(IB:ImageBitmap)
	{
		DefaultSSV.Update(IB).then(() => {
			for(const I of (Share.DS?.Items.values() ?? []))
				I.MapIcon?.IconGO.SpriteUpdated();
		});
	}

	public Delete() { this.IconGO.Delete(); }
	public BringToFront() { this.IconGO.BringToFront(); }
	public UpdateSize(IconSize:number) { if(this.IconGO.LocalScale?.X!==IconSize) this.IconGO.LocalScale=new Vector2(IconSize, IconSize); }
	public get RenderRect() { return this.IconGO.RenderRect; }
	public set ForceVisibility(Val:boolean) { this.IconGO.Active=Val; }
	public get IsIconVisible() { return this.IconGO.Active; }
}

abstract class Material
{
	//TODO: For now, I’m not implementing ChangesEachFrame materials as I’m trying to keep this lightweight and not render frames when it’s not needed.
	//I guess it would be possible to just rerender a single sprite as long as transparent pixels did not change and its on top. Would have to keep a snapshot of the area before rendering the sprite for the first time.
	//noinspection JSUnusedGlobalSymbols
	public readonly ChangesEachFrame=false;

	private _IsUsingCPUShader=false; public get IsUsingCPUShader() { return this._IsUsingCPUShader; }
	public Run(In:OffscreenCanvas)
	{
		if(this.GPUShader===undefined)
			try { this.GPUShader=this.GetGPUShader(In.width, In.height); }
			catch(e) {
				Log.Error("Error creating shader. Falling back to CPU shader: "+Util.GetErrorMessage(e));
				this.GPUShader=null;
			}

		if(this.GPUShader)
			try {
				this.PrepGPUShader(this.GPUShader);
				this.GPUShader.Render(In);
				this._IsUsingCPUShader=false;
				return this.GPUShader.Canvas;
			} catch(e) {
				if(!this.IsUsingCPUShader)
					Log.Error("GPU Shader failed. Falling back to CPU shader: "+Util.GetErrorMessage(e));
			}

		this._IsUsingCPUShader=true;
		const Canvas=new OffscreenCanvas(In.width, In.height);
		//eslint-disable-next-line @typescript-eslint/naming-convention
		const Ctx=Canvas.getContext('2d', {willReadFrequently:true})!;
		Ctx.reset();
		Ctx.drawImage(In, 0, 0);
		const ImageData=Ctx.getImageData(0, 0, In.width, In.height);
		this.Process(ImageData.data, In.width, In.height);
		Ctx.putImageData(ImageData, 0, 0);
		return Canvas;
	}

	protected abstract Process(Pixels:Uint8ClampedArray, Width:number, Height:number): void;

	private GPUShader:RGBAShader|undefined|null;
	protected abstract GetGPUShader(Width:number, Height:number): RGBAShader|null;
	protected abstract PrepGPUShader(Shader:RGBAShader): void;
}

class HSVShader extends Material
{
	public Hue	:number=0.5;
	public Sat	:number=0.5;
	public Val	:number=0.5;
	public Alpha:number=1.0;
	private static ColorClass?:typeof import('./Util/Color');
	private static LoadColorClass?:Promise<void>;
	protected override Process(Pixels:Uint8ClampedArray)
	{
		if(!HSVShader.ColorClass)
			if(HSVShader.LoadColorClass)
				throw new Error('PROMISE', {cause:HSVShader.LoadColorClass});
			else
				throw new Error('PROMISE', {cause:HSVShader.LoadColorClass=new Promise<void>(async Resolve => {
					HSVShader.ColorClass=(await import('./Util/Color'));
					Resolve();
				})});

		for(let i=0; i<Pixels.length; i+=4) {
			const HSV=HSVShader.ColorClass.rgb2hsv(Pixels[i]/255, Pixels[i+1]/255, Pixels[i+2]/255);
			HSV.h+=this.Hue;
			const NewColor=HSVShader.ColorClass.hsv2rgb(
				HSV.h-Math.floor(HSV.h),
				Math.min(1, HSV.s*this.Sat),
				Math.min(1, HSV.v*this.Val)
			);
			Pixels[i  ]=NewColor.r*255;
			Pixels[i+1]=NewColor.g*255;
			Pixels[i+2]=NewColor.b*255;
			Pixels[i+3]*=this.Alpha;
		}
	}
	protected override GetGPUShader(Width:number, Height:number) { return new RGBAShader(HSVAShader, Width, Height); }
	protected override PrepGPUShader(Shader:RGBAShader)
	{
		Shader.C={
			r:this.Hue,
			g:this.Sat,
			b:this.Val,
			a:this.Alpha,
		} as ColorRGBA;
	}
}

class RGBTintShader extends Material
{
	private C:ColorRGBA=WillBeSet;
	public SetColor(C:ColorRGBA) { this.C=C; return this; }
	protected override Process(Pixels:Uint8ClampedArray)
	{
		const C=this.C;
		for(let i=0; i<Pixels.length; i+=4) {
			Pixels[i  ]*=C.r;
			Pixels[i+1]*=C.g;
			Pixels[i+2]*=C.b;
			Pixels[i+3]*=C.a;
		}
	}
	protected override GetGPUShader(Width:number, Height:number) { return new RGBAShader(TintShader, Width, Height); }
	protected override PrepGPUShader(Shader:RGBAShader) { Shader.C=this.C; }
}

//Note: Items used to each have their own canvas with their current sprite (which is how the C# engine basically handled things). And when GameObject.ReRender() was called, it actually rerendered!
//It was a ridiculous waste of memory and processing, however, so now sprites are pulled from individual sprite sheets, each with their own pre-processed shaders run.
//I kept the way sprites were created/updated in case this ever needs to be reverted, and the overhead is minimal. Plus, there is a chance I may have sprites that use their own sheet
class SpriteSheetVariation
{
	private _Canvas?:OffscreenCanvas; public get Canvas() { return this._Canvas; }
	public readonly Shaders:readonly Material[];
	constructor(
		Color?:ColorRGBA,
		Material?:Material,
	) {
		const NewMats=[];
		if(Color) {
			const CShader=new RGBTintShader();
			CShader.SetColor(Color);
			NewMats.push(CShader);
		}
		if(Material)
			NewMats.push(Material);
		this.Shaders=NewMats;
	}

	//If only 1 shader, returns what’s needed to finalize the process. This allows for parallel processing of the GPU data
	public Rerender(IB:ImageBitmap): RenderExecution
	{
		if(this.Canvas?.width!==IB.width || this.Canvas.height!==IB.height)
			this._Canvas=new OffscreenCanvas(IB.width, IB.height);

		const Ctx=this.Canvas!.getContext('2d')!;
		Ctx.reset();
		Ctx.drawImage(IB, 0, 0);

		if(this.Shaders.length===0)
			return new RenderExecution();
		if(this.Shaders.length===1)
			return new RenderExecution(this.Shaders[0].Run(this.Canvas!), Ctx);

		for(const Shader of this.Shaders) {
			const UpdatedImage=Shader.Run(this.Canvas!);
			Ctx.reset();
			Ctx.drawImage(UpdatedImage, 0, 0);
		}
		return new RenderExecution();
	}
}
class RenderExecution
{
	constructor(
		public readonly Rendered?:OffscreenCanvas,
		public readonly Ctx?:OffscreenCanvasRenderingContext2D,
	) { }
	public Complete()
	{
		if(!this.Rendered)
			return;
		this.Ctx!.reset();
		this.Ctx!.drawImage(this.Rendered, 0, 0);
	}
}
const SSVDefault='Default';
type SSVVar=keyof SpriteSheetVariations['Vars'];
class SpriteSheetVariations
{
	public get HasImage() { return !!this._IB; }
	private _IB?:ImageBitmap; public get IB() { return this._IB; }

	constructor(
		public Vars:Record<string, SpriteSheetVariation>={
			Red		:new SpriteSheetVariation(ColorRGBA.Red),
			Green	:new SpriteSheetVariation(ColorRGBA.Green),
			Blue	:new SpriteSheetVariation(ColorRGBA.Blue),
			IsFound	:new SpriteSheetVariation(undefined, new HSVShader()),
		}
	) {
		Vars[SSVDefault]=new SpriteSheetVariation(); //Default is required
	}

	public async Update(IB:ImageBitmap)
	{
		this._IB=IB;
		let Renders:RenderExecution[];

		//If an error is thrown with a promise, wait for it and then process again
		try {
			Renders=Object.values(this.Vars).map(SSV => SSV.Rerender(IB));
		} catch(e) {
			if((e as Error)?.message!=='PROMISE') {
				Log.Error("Error during shader processing: "+Util.GetErrorMessage(e));
				return;
			}
			await ((e as Error).cause as Promise<void>);
			Renders=Object.values(this.Vars).map(SSV => SSV.Rerender(IB));
		}
		Renders.forEach(R => R.Complete());
	}
	public Rerender(Var:SSVVar) { if(this._IB) this.Vars[Var]?.Rerender(this._IB).Complete(); }
}
export const DefaultSSV=Util.OneTimeInit('SpriteSheetVariations', () => new SpriteSheetVariations());