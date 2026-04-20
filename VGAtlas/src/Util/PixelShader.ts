//noinspection SpellCheckingInspection
/* eslint-disable @typescript-eslint/naming-convention */
import REGL from 'regl';
import { ColorRGBA, WillBeSet } from './SharedClasses';

type Attributes={position:number[]};
type Props={
	UImage:REGL.Texture2D;
	UColor:[number, number, number, number];
};

const VertShader=`
precision mediump float;
attribute vec2 position;
varying vec2 Vuv;
void main()
{
	Vuv=position*0.5+0.5;
	Vuv.y=1.0-Vuv.y;
	gl_Position=vec4(position, 0.0, 1.0);
}
`;

export const TintShader=`
precision mediump float;
uniform sampler2D UImage;
uniform vec4 UColor;
varying vec2 Vuv;
void main()
{
	gl_FragColor=texture2D(UImage, Vuv)*UColor;
}
`;

export const HSVAShader=`
precision mediump float;
uniform sampler2D UImage;
uniform vec4 UColor; //hsva: x=hue shift, y=sat mul, z=val mul, w=alpha mul
varying vec2 Vuv;

vec3 rgb2hsv(vec3 c)
{
	vec4 K=vec4(0.0, -1.0/3.0, 2.0/3.0, -1.0);
	vec4 p=c.g<c.b ? vec4(c.bg, K.wz) : vec4(c.gb, K.xy);
	vec4 q=c.r<p.x ? vec4(p.xyw, c.r) : vec4(c.r, p.yzx);
	float d =q.x-min(q.w, q.y);
	float e =1e-10;
	return vec3(abs(q.z+(q.w-q.y)/(6.0*d+e)), d/(q.x+e), q.x);
}

vec3 hsv2rgb(vec3 c)
{
	vec3 K=vec3(1.0, 2.0/3.0, 1.0/3.0);
	vec3 p=abs(fract(c.xxx+K)*6.0-3.0);
	return c.z*mix(vec3(1.0), clamp(p-1.0, 0.0, 1.0), c.y);
}

void main()
{
	float Hue	= UColor.x;
	float Sat	= UColor.y;
	float Val	= UColor.z;
	float Alpha	= UColor.w;

	vec4 c	 = texture2D(UImage, Vuv);
	vec3 hsv = rgb2hsv(c.rgb);
	hsv.x	 = fract(hsv.x + Hue);
	hsv.y	*= Sat;
	hsv.z	*= Val;
	c.rgb	 = hsv2rgb(hsv);
	c.a		*= Alpha;
	gl_FragColor=c;
}
`;

export class RGBAShader
{
	private readonly REGL:REGL.Regl;
	private Texture?:REGL.Texture2D;
	public C:ColorRGBA=new ColorRGBA(1, 1, 1, 1);
	private readonly Draw:REGL.DrawCommand;
	public Canvas:OffscreenCanvas=WillBeSet;

	public constructor(PixelShader:string, Width:number, Height:number) {
		this.REGL=REGL({
			canvas:(this.Canvas=new OffscreenCanvas(Width, Height)) as unknown as HTMLCanvasElement,
			attributes:{
				alpha:true, depth:false, stencil:false, antialias:false,
				premultipliedAlpha:false, preserveDrawingBuffer:true,
			},
		});

		this.Draw=this.REGL<Props, Attributes, Props>({
			vert:VertShader,
			frag:PixelShader,
			attributes:{position:[-1, -1, 1, -1, -1, 1, -1, 1, 1, -1, 1, 1]},
			uniforms:{
				UImage:this.REGL.prop<Props, 'UImage'>('UImage'),
				UColor:this.REGL.prop<Props, 'UColor'>('UColor'),
			},
			count:6,
		});
	}

	public Render(Image:OffscreenCanvas):OffscreenCanvas
	{
		if(this.Canvas.width!==Image.width || this.Canvas.height!==Image.height)
			throw new Error("Image size cannot change");

		if(this.Texture===undefined)
			this.Texture=this.REGL.texture({
				data:Image as unknown as REGL.TextureImageData,
				width:Image.width, height:Image.height,
				flipY:false, premultiplyAlpha:false, min:'nearest', mag:'nearest', wrap:'clamp',
			});
		else
			this.Texture({data:Image as unknown as REGL.TextureImageData});

		this.REGL.clear({color:[0, 0, 0, 0], depth:1, stencil:0});
		this.Draw({
			UImage:this.Texture,
			UColor:[this.C.r, this.C.g, this.C.b, this.C.a],
		});

		return this.Canvas;
	}

	public Dispose()
	{
		if(this.Texture) {
			this.Texture.destroy();
			this.Texture=undefined;
		}
		this.REGL.destroy();
	}
}