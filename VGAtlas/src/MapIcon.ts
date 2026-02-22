import { CategoryToggleState, Item } from "./CategoriesAndItems"
import { ColorRGBA, Log, Rect, Vector2, WillBeSet } from "./SharedClasses"
import { InitFuncs } from "./Misc"
import { Share } from "./Share"
import Color from "color"

class SpriteRenderInfo { constructor(public readonly Image:ImageBitmap, public readonly ImageRect:Rect, public readonly Center:Vector2) { } }

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
	private static ErrImage:ImageBitmap;
	static { void Sprite.CreateErrorImage().catch(() => {}); }
	private static async CreateErrorImage()
	{
		//noinspection SpellCheckingInspection
		const Res=await fetch(`data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAADcAAAA2AQMAAABZSZOgAAAABlBMVEX/ANwAAAAtIRQiAAAAIElEQVR4AWNgsH/A/4P5H8X0/wPyH9j/MFBKj7oHPw0AR228KREmCC0AAAAASUVORK5CYII=`);
		Sprite.ErrImage=await createImageBitmap(await Res.blob());
	}

	public get Image	() { return this._Image		; }; public set Image	 (Val) { this._Image	=Val; this.OnChange(); }
	public get ImageRect() { return this._ImageRect	; }; public set ImageRect(Val) { this._ImageRect=Val; this.OnChange(); }
	public get Center	() { return this._Center	; }; public set Center	 (Val) { this._Center	=Val; this.OnChange(); }
	constructor(private _Image:ImageBitmap|undefined, private _ImageRect:Rect, private _Center:Vector2) { super(); }

	public GetRenderInfo(): SpriteRenderInfo|undefined
	{
		return	this.Image		? new SpriteRenderInfo(this.Image, this.ImageRect, this.Center)
			:	Sprite.ErrImage	? new SpriteRenderInfo(Sprite.ErrImage, new Rect(0, 0, Sprite.ErrImage.width, Sprite.ErrImage.height), new Vector2(0.5, 0.5))
			:					  undefined;
	}
}

//Emulates a UnityObject sprite
class GameObject extends Versioned
{
	private static OrderedGameObjectList:GameObject[]=[];
	private static DrawAll(Ctx:CanvasRenderingContext2D) { Log.Debug("Drawing complete. Icons rendered: "+
		GameObject.OrderedGameObjectList.map(GO => GO.Draw(Ctx)).filter(B => B).length
	); }
	public static get AllObjects() { return GameObject.OrderedGameObjectList.values(); }
	private static ColorShader:RGBTintShader;
	private static InitClass()
	{
		GameObject.ColorShader=new RGBTintShader();
		Share.MCanvas.Events.Draw.Add("GameObjects", GameObject.DrawAll);
	}
	static { InitFuncs.push(GameObject.InitClass); }

	private _Color?:ColorRGBA	=undefined; public get Color		() { return this._Color		; }; public set Color		(Val) { this._Color		=Val; this.OnChange		(); }
	private _Active				=true	  ; public get Active		() { return this._Active	; }; public set Active		(Val) { this._Active	=Val; this.RefreshCanvas(); }
	private _LocalScale:Vector2	=WillBeSet; public get LocalScale	() { return this._LocalScale; }; public set LocalScale	(Val) { this._LocalScale=Val; this.RefreshCanvas(); }
	private _Pos:Vector2				  ; public get Pos			() { return this._Pos		; }; public set Pos			(Val) { this._Pos		=Val; this.RefreshCanvas(); }
	private _Material?:Material	=undefined; public get Material		() { return this._Material	; }; public set Material	(Val) {
		this._Material?.Deregister(this);
		this._Material=Val;
		this._Material?.Register(this);
		this.OnChange();
	}

	private SpriteVersion=-1; private RenderedVersion=-1;
	private SRI?:SpriteRenderInfo=undefined;
	private RenderedSprite:OffscreenCanvas=WillBeSet;

	constructor(public Title:string, Pos:Vector2, private MySprite:Sprite)
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

		Log.Debug("Rerendering: "+this.Title);

		//See if we can reuse the current canvas source
		if(
			   !(this.RenderedSprite instanceof OffscreenCanvas)
			|| this.RenderedSprite.width !==this.SRI.ImageRect.Width
			|| this.RenderedSprite.height!==this.SRI.ImageRect.Height
		)
			this.RenderedSprite=new OffscreenCanvas(this.SRI.ImageRect.Width, this.SRI.ImageRect.Height);

		//Render to the new canvas
		const Ctx=this.RenderedSprite.getContext("2d")!;
		const SRIRect=this.SRI.ImageRect;
		Ctx.reset();
		Ctx.drawImage(
			this.SRI.Image, SRIRect.x, SRIRect.y, SRIRect.Width, SRIRect.Height,
			0, 0, SRIRect.Width, SRIRect.Height
		);
		for(const Shader of [
			this.Color!==undefined ? GameObject.ColorShader.SetColor(this.Color) : undefined,
			this.Material
		].filter(S => S!==undefined)) {
			const UpdatedImage=Shader.Run(this.RenderedSprite);
			Ctx.reset();
			Ctx.drawImage(UpdatedImage, 0, 0);
		}
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
		Ctx.drawImage(
			this.RenderedSprite, 0, 0, this.RenderedSprite.width, this.RenderedSprite.height,
			R.x, R.y, R.Width, R.Height
		);

		return true;
	}

	public get RenderRect(): Rect|undefined
	{
		if(this.SRI===undefined)
			return undefined;
		const RenderWidth =this.SRI.ImageRect.Width *this.LocalScale.x;
		const RenderHeight=this.SRI.ImageRect.Height*this.LocalScale.y;
		const MapPos=Share.MCanvas.MapToCanvas(this.Pos);
		MapPos.x-=RenderWidth *this.SRI.Center.x;
		MapPos.y-=RenderHeight*this.SRI.Center.y;
		return new Rect(MapPos.x, MapPos.y, RenderWidth, RenderHeight);
	}
}

//The physical map icons to display on the canvas
export class MapIcon
{
	//Basic instance members
	private readonly IconGO:GameObject;
	private IsUsingNewMaterial	=false;

	constructor(Item:Item, MySprite:Sprite)
	{
		this.IconGO=new GameObject(`Pin - ${Item.Title} [${Item.ID}]`, Item.Pos, MySprite);
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
		const NewColor=this.IconGO.Color=
			  this.IsSelected ? ColorRGBA.Green
			: this.IsHovered ? ColorRGBA.Blue
			: Share.MC?.ShowLinkedStatus && !this.IsLinked ? ColorRGBA.Red
			: undefined;

		//Update the IsFound shader
		const UseFoundShader=this.IsFound && NewColor===undefined;
		if(UseFoundShader!==this.IsUsingNewMaterial)
			this.IconGO.Material=(this.IsUsingNewMaterial=UseFoundShader) ? MapIcon.MyShader : undefined;
	}

	//Material shader for HSV conversion
	private static MyShader:HSVShader;
	static {
		setTimeout(() => {
			MapIcon.MyShader=new HSVShader();
			MapIcon.UpdateShaderColor(Share.LC.Color_FoundIcon.V);
		}, 0);
		Share.LC.Color_FoundIcon.SettingChanged.Add("MapIcon.UpdateShaderColor", MapIcon.UpdateShaderColor.bind(this));
	}
	private static UpdateShaderColor(C:ColorRGBA)
	{
		this.MyShader.Hue	=C.r-0.5;
		this.MyShader.Sat	=C.g*2	;
		this.MyShader.Val	=C.b*2	;
		this.MyShader.Alpha	=C.a	;
		this.MyShader.RunAllRegistered();
	}

	public BringToFront() { this.IconGO.BringToFront(); }
	public UpdateSize(IconSize:number) { if(this.IconGO.LocalScale?.x!==IconSize) this.IconGO.LocalScale=new Vector2(IconSize, IconSize); }
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

	//Keep a canvas ready for any x*y multiplier
	private static readonly MapKeyMultiplier=10_000;
	private static CanvasList=new Map<number, OffscreenCanvas>();
	protected static GetCanvas(x:number, y:number)
	{
		if(x<=0 || y<=0)
			throw new Error("Size must be greater than 0");
		if(x>=Material.MapKeyMultiplier || y>=Material.MapKeyMultiplier)
			throw new Error("Size cannot be greater than "+Material.MapKeyMultiplier);
		let Ret=Material.CanvasList.get(y*Material.MapKeyMultiplier+x);
		if(!Ret)
			Material.CanvasList.set(y*Material.MapKeyMultiplier+x, Ret=new OffscreenCanvas(x, y));
		return Ret;
	}

	public Run(In:OffscreenCanvas)
	{
		const Canvas=Material.GetCanvas(In.width, In.height);
		const Ctx=Canvas.getContext("2d")!;
		Ctx.reset();
		Ctx.drawImage(In, 0, 0);
		const ImageData=Ctx.getImageData(0, 0, In.width, In.height);
		const Data=ImageData.data;
		this.Process(Data, In.width, In.height);
		Ctx.putImageData(ImageData, 0, 0);
		return Canvas;
	}

	protected abstract Process(Pixels:Uint8ClampedArray, Width:number, Height:number): void;

	private readonly RegisteredGOs=new Set<GameObject>();
	public Register  (GO:GameObject) { this.RegisteredGOs.add(GO); }
	public Deregister(GO:GameObject) { this.RegisteredGOs.delete(GO); }
	public RunAllRegistered() { this.RegisteredGOs.forEach(GO => GO.IncVersion()); }
}

class HSVShader extends Material
{
	public Hue	:number=0.5;
	public Sat	:number=0.5;
	public Val	:number=0.5;
	public Alpha:number=1.0;
	protected override Process(Pixels:Uint8ClampedArray)
	{
		for(let i=0; i<Pixels.length; i+=4) {
			const C=Color({ r:Pixels[i], g:Pixels[i+1], b:Pixels[i+2] }).hsv();
			const NewColor=C
				.rotate(this.Hue*360)
				.saturationv(Math.min(100, C.saturationv()*this.Sat))
				.value(Math.min(100, C.value()*this.Val));
			Pixels[i  ]=NewColor.red();
			Pixels[i+1]=NewColor.green();
			Pixels[i+2]=NewColor.blue();
			Pixels[i+3]*=this.Alpha;
		}
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
}