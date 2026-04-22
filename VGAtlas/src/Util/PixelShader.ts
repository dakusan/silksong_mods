//noinspection SpellCheckingInspection
/* eslint-disable @typescript-eslint/naming-convention */
import { ColorRGBA } from './SharedClasses';

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
	public Canvas:OffscreenCanvas;
	private readonly GL:WebGLRenderingContext;
	private readonly Program:WebGLProgram;
	private readonly PositionBuffer:WebGLBuffer;
	private readonly Texture:WebGLTexture;
	private readonly AttrPosition:number;
	private readonly UniformUImage:WebGLUniformLocation;
	private readonly UniformUColor:WebGLUniformLocation;
	public C:ColorRGBA=new ColorRGBA(1, 1, 1, 1);

	public constructor(PixelShader:string, Width:number, Height:number) {
		function ThrowOnNull<T>(Str:string): T { throw new Error(Str); }
		function CompileShader(GL:WebGLRenderingContext, Type:number, Source:string): WebGLShader
		{
			const Shader=GL.createShader(Type) ?? ThrowOnNull<WebGLShader>("Could not create shader");
			GL.shaderSource(Shader, Source);
			GL.compileShader(Shader);
			if(GL.getShaderParameter(Shader, GL.COMPILE_STATUS))
				return Shader;
			const Info=GL.getShaderInfoLog(Shader);
			GL.deleteShader(Shader);
			throw new Error(`Shader compile failed: ${Info ?? "Unknown error"}`);
		}
		function CreateProgram(GL:WebGLRenderingContext, VertSource:string, FragSource:string): WebGLProgram
		{
			const Vert=CompileShader(GL, GL.VERTEX_SHADER, VertSource);
			const Frag=CompileShader(GL, GL.FRAGMENT_SHADER, FragSource);
			const Program=GL.createProgram() ?? ThrowOnNull("Could not create program");

			try {
				GL.attachShader(Program, Vert);
				GL.attachShader(Program, Frag);
				GL.linkProgram(Program);
				if(!GL.getProgramParameter(Program, GL.LINK_STATUS))
					throw new Error(`Program link failed: ${GL.getProgramInfoLog(Program) ?? "Unknown error"}`);
			} finally {
				GL.deleteShader(Vert);
				GL.deleteShader(Frag);
			}

			return Program;
		}

		this.Canvas=new OffscreenCanvas(Width, Height);
		const GL=this.GL=this.Canvas.getContext('webgl', {
			alpha:true, depth:false, stencil:false, antialias:false, premultipliedAlpha:false,
		}) ?? ThrowOnNull("Could not create WebGL context");

		this.Program=CreateProgram(GL, VertShader, PixelShader);
		this.AttrPosition=GL.getAttribLocation(this.Program, 'position');
		if(this.AttrPosition<0)
			throw new Error("Could not find attribute position");

		this.UniformUImage=GL.getUniformLocation(this.Program, 'UImage') ?? ThrowOnNull("Could not find uniform UImage");
		this.UniformUColor=GL.getUniformLocation(this.Program, 'UColor') ?? ThrowOnNull("Could not find uniform UColor");

		this.PositionBuffer=GL.createBuffer() ?? ThrowOnNull("Could not create position buffer");
		GL.bindBuffer(GL.ARRAY_BUFFER, this.PositionBuffer);
		GL.bufferData(GL.ARRAY_BUFFER, new Float32Array([-1, -1, 1, -1, -1, 1, -1, 1, 1, -1, 1, 1]), GL.STATIC_DRAW);

		this.Texture=GL.createTexture() ?? ThrowOnNull("Could not create texture");
		GL.bindTexture(GL.TEXTURE_2D, this.Texture);
		GL.texParameteri(GL.TEXTURE_2D, GL.TEXTURE_MIN_FILTER, GL.NEAREST);
		GL.texParameteri(GL.TEXTURE_2D, GL.TEXTURE_MAG_FILTER, GL.NEAREST);
		GL.texParameteri(GL.TEXTURE_2D, GL.TEXTURE_WRAP_S, GL.CLAMP_TO_EDGE);
		GL.texParameteri(GL.TEXTURE_2D, GL.TEXTURE_WRAP_T, GL.CLAMP_TO_EDGE);
	}

	public Render(Image:OffscreenCanvas):OffscreenCanvas
	{
		if(this.Canvas.width!==Image.width || this.Canvas.height!==Image.height)
			throw new Error("Image size cannot change");

		const GL=this.GL;
		GL.viewport(0, 0, this.Canvas.width, this.Canvas.height);
		GL.useProgram(this.Program);

		GL.activeTexture(GL.TEXTURE0);
		GL.bindTexture(GL.TEXTURE_2D, this.Texture);
		GL.pixelStorei(GL.UNPACK_FLIP_Y_WEBGL, false);
		GL.pixelStorei(GL.UNPACK_PREMULTIPLY_ALPHA_WEBGL, false);
		GL.texImage2D(GL.TEXTURE_2D, 0, GL.RGBA, GL.RGBA, GL.UNSIGNED_BYTE, Image);

		GL.uniform1i(this.UniformUImage, 0);
		GL.uniform4f(this.UniformUColor, this.C.r, this.C.g, this.C.b, this.C.a);

		GL.bindBuffer(GL.ARRAY_BUFFER, this.PositionBuffer);
		GL.enableVertexAttribArray(this.AttrPosition);
		GL.vertexAttribPointer(this.AttrPosition, 2, GL.FLOAT, false, 0, 0);
		GL.drawArrays(GL.TRIANGLES, 0, 6);

		return this.Canvas;
	}

	public Dispose()
	{
		this.GL.deleteTexture(this.Texture);
		this.GL.deleteBuffer(this.PositionBuffer);
		this.GL.deleteProgram(this.Program);
	}
}