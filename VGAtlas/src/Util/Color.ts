export function rgb2hsv(r:number, g:number, b:number): {h:number, s:number, v:number}
{
	const x=0, y=1, z=2, w=3;
	const K0=0.0, K1=-1.0/3.0, K2=2.0/3.0, K3=-1.0;
	const p=(g<b)	? [b,    g,    K3,   K2] : [g, b   , K0,   K1  ];
	const q=(r<p[x])? [p[x], p[y], p[w], r ] : [r, p[y], p[z], p[x]];
	const d=q[x]-Math.min(q[w], q[y]);
	const e=1e-10;
	return {
		h:Math.abs(q[z]+(q[w]-q[y])/(6*d+e)),
		s:d/(q[x]+e),
		v:q[x]
	};
}

export function hsv2rgb(h:number, s:number, v:number): {r:number, g:number, b:number}
{
	const x=0, y=1, z=2;
	const K=[1.0, 2.0/3.0, 1.0/3.0];
	const Fract=(n:number)	=>n-Math.floor(n);
	const Clamp=(n:number)	=>Math.min(1, Math.max(0, n));
	const GetP =(n:number)	=>Math.abs(Fract(h+K[n])*6-3);
	const CLerp=(n:number)	=>v*(1+(Clamp(GetP(n)-1)-1)*s);
	return {r:CLerp(x), g:CLerp(y), b:CLerp(z)};
}