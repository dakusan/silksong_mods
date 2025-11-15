using SilkDev.Textures;
using System;
using UnityEngine;
using OA=System.ObsoleteAttribute;

namespace SilkDev.Windows;

#pragma warning disable CS0809 //Obsolete member overrides non-obsolete member

//Draws on-screen geometry measured in pixels. Mouse interaction is ignored.
//If BorderSize <= 0, no border (solid fill with MainTexture).
//If BorderSize > 0, hollow with MainTexture on the border only.
//When BGTexture is set:
//  • If BGTextureUnderBorder = true (default) → BGTexture is drawn over the full rectangle, behind the main/border.
//  • If BGTextureUnderBorder = false → BGTexture is drawn only in the inner area.
//Border side textures are rotated 90° clockwise so a single texture works for top/bottom + sides.
public static class DrawGeometry
{
	//A lot of inherited members in the below classes do not apply to those classes and are hidden through virtual inheritance.
	//Used with System.ObsoleteAttribute to signal members that cannot be accessed in derived classes.
	private const string NA="This member is not applicable on this derived type";
	private const bool T=true;
	private static object NaF() => throw new InvalidOperationException(NA);

	//Only a texture or a color can be activated at once
	public class ColorOrTexture
	{
		private Texture2D? ColorTexture, UserTexture;

		public Color? Color
		{
			get;
			set {
				if(field==value)
					return;
				ColorTexture?.TDestroy();
				ColorTexture=UserTexture=null;
				if((field=value).HasValue)
					ColorTexture=value!.Value.MakeTexture();
			}
		}

		public Texture2D? Texture
		{
			get => UserTexture;
			set {
				if(UserTexture==value)
					return;
				Color=null;
				UserTexture=value;
			}
		}

		public Texture2D? ActiveTexture => ColorTexture ?? UserTexture;
	}

	private static readonly Rect Rotate90CW=new(0f, 1f, 1f, -1f);

	//Base class other geometries derive from
	public abstract class Geometry : Window
	{
		public ColorOrTexture MainTexture=new(); //The texture used for the entire geometric object or the border
		private ColorOrTexture _BGTexture=new(); //See BGTexture
		public virtual ColorOrTexture BGTexture { get => _BGTexture; set => _BGTexture=value; } //Used as the background texture when borders are drawn.
		public virtual bool BGTextureUnderBorder { get; set; } = true; //BGTexture is normally rendered across the entire shape and would be partially hidden behind the border. If false, the texture render starts inside the border.
		public Vector2 Pos { get => new(X, Y); set => (X, Y)=((int)value.x, (int)value.y); }
		public int X, Y;

		protected Geometry(int X, int Y, Color Color  , string Title) : base(Title, true, 5000) =>
			(this.X, this.Y, MainTexture.Color  , UnboundDraw)=(X, Y, Color, true);
		protected Geometry(int X, int Y, Texture2D Tex, string Title) : base(Title, true, 5000) =>
			(this.X, this.Y, MainTexture.Texture, UnboundDraw)=(X, Y, Tex  , true);

		public override void Close()
		{
			MainTexture.Color=_BGTexture.Color=null;
			base.Close();
		}
		protected override bool IsMouseOverWindow(Vector2 _) => false; //Ignore mouse (For now!)
	}

	//A pixel on the screen (Subclass of square)
	public class Dot : Square
	{
		[OA(NA,T)] public override ColorOrTexture BGTexture { get => (ColorOrTexture)	NaF(); set => NaF(); }
		[OA(NA,T)] public override bool BGTextureUnderBorder{ get => (bool)				NaF(); set => NaF(); }
		[OA(NA,T)] public override int BorderSize			{ get => (int)				NaF(); set => NaF(); }
		[OA(NA,T)] public override int Size					{ get => (int)				NaF(); set => NaF(); }
		public Dot(int X, int Y,  Color Color) : base(         X,          Y, 1, Color, -1, "Dot"	) { }
		public Dot(int X, int Y,Texture2D Tex) : base(         X,          Y, 1, Tex  , -1, "Dot"	) { }
		public Dot(Vector2 Pos,   Color Color) : this((int)Pos.x, (int)Pos.y,    Color				) { }
		public Dot(Vector2 Pos, Texture2D Tex) : this((int)Pos.x, (int)Pos.y,    Tex				) { }
	}

	//A square on the screen (Subclass of rectangle)
	public class Square : Rectangle
	{
		[OA(NA,T)] public override int Width		{ get => (int)NaF(); set => NaF(); }
		[OA(NA,T)] public override int Height		{ get => (int)NaF(); set => NaF(); }
		public virtual int Size { get => base.Width; set => base.Width=base.Height=value; }
		public   Square(int X, int Y, int Size, Color   Color, int BorderSize=-1)			: this(         X,          Y, Size,       Color, BorderSize, "Square")	{ }
		public   Square(int X, int Y, int Size, Texture2D Tex, int BorderSize=-1)			: this(         X,          Y, Size,       Tex,   BorderSize, "Square")	{ }
		public   Square(Vector2 Pos,  int Size, Color   Color, int BorderSize=-1)			: this((int)Pos.x, (int)Pos.y, Size,       Color, BorderSize)			{ }
		public   Square(Vector2 Pos,  int Size, Texture2D Tex, int BorderSize=-1)			: this((int)Pos.x, (int)Pos.y, Size,       Tex,   BorderSize)			{ }
		internal Square(int X, int Y, int Size, Color   Color, int BorderSize, string Title): base(X, Y, Size, Size, Color, BorderSize, Title) { }
		internal Square(int X, int Y, int Size, Texture2D Tex, int BorderSize, string Title): base(X, Y, Size, Size, Tex  , BorderSize, Title) { }
	}

	//A rectangle on the screen
	public class Rectangle : Geometry
	{
		private int _Width, _Height, _BorderSize;
		public virtual int Width		{ get => _Width		; set => _Width		=value; }
		public virtual int Height		{ get => _Height	; set => _Height	=value; }
		public virtual int BorderSize	{ get => _BorderSize; set => _BorderSize=Mathf.Max(value, 0); }
		public   Rectangle(int X, int Y, int Width, int Height, Color   Color, int BorderSize=-1)			: this(               X,           Y, Width, Height, Color, BorderSize, "Rectangle"){ }
		public   Rectangle(int X, int Y, int Width, int Height, Texture2D Tex, int BorderSize=-1)			: this(               X,           Y, Width, Height, Tex  , BorderSize, "Rectangle"){ }
		public   Rectangle(Vector2 Pos,  int Width, int Height, Color   Color, int BorderSize=-1)			: this(     (int) Pos.x, (int) Pos.y, Width, Height, Color, BorderSize)				{ }
		public   Rectangle(Vector2 Pos,  int Width, int Height, Texture2D Tex, int BorderSize=-1)			: this(     (int) Pos.x, (int) Pos.y, Width, Height, Tex  , BorderSize)				{ }
		public   Rectangle(Vector2 Pos,  Vector2 Size,          Color   Color, int BorderSize=-1)			: this(Pos, (int)Size.x, (int)Size.y,                Color, BorderSize)				{ }
		public   Rectangle(Vector2 Pos,  Vector2 Size,          Texture2D Tex, int BorderSize=-1)			: this(Pos, (int)Size.x, (int)Size.y,                Tex  , BorderSize)				{ }
		public   Rectangle(Rect Rect,                           Color   Color, int BorderSize=-1)			: this(Rect.position,    Rect.size,                  Color, BorderSize)				{ }
		public   Rectangle(Rect Rect,                           Texture2D Tex, int BorderSize=-1)			: this(Rect.position,    Rect.size,                  Tex  , BorderSize)				{ }
		internal Rectangle(int X, int Y, int Width, int Height, Color   Color, int BorderSize, string Title): base(X, Y, Color, Title) => (_Width, _Height, _BorderSize)=(Width, Height, BorderSize);
		internal Rectangle(int X, int Y, int Width, int Height, Texture2D Tex, int BorderSize, string Title): base(X, Y, Tex  , Title) => (_Width, _Height, _BorderSize)=(Width, Height, BorderSize);
		public Rect Rect { get => new(X, Y, _Width, _Height); set => (X, Y, _Width, _Height)=((int)value.x, (int)value.y, (int)value.width, (int)value.height); }

		protected override void DoLayout(int _, Event __)
		{
			//If no width or height, nothing to do
			if(_Width<=0 || _Height<=0)
				return;

			//If main texture is null, throw an error
			Texture2D MainTex=MainTexture.ActiveTexture ?? throw new InvalidOperationException("MainTexture.Color or MainTexture.Texture must be set");

			//Optionally draw the background texture
			if(base.BGTexture.ActiveTexture!=null) {
				int Shrink=base.BGTextureUnderBorder ? 0 : _BorderSize;
				Rect BGRect=new Rect(X, Y, _Width, _Height).Grow(-Shrink, -Shrink);
				if(BGRect.width>0 && BGRect.height>0)
					GUI.DrawTexture(BGRect, base.BGTexture.ActiveTexture);
			}

			//Draw a solid rectangle
			if(_BorderSize<=0 || _BorderSize*2>=_Width || _BorderSize*2>=_Height) {
				GUI.DrawTexture(new Rect(X, Y, _Width, _Height), MainTex);
				return;
			}

			//Draw a hollow rectangle by drawing the borders
			int BS=_BorderSize;
			GUI.DrawTexture				(new Rect(X,			Y,				_Width,	BS			), MainTex				);
			GUI.DrawTexture				(new Rect(X,			Y+_Height-BS,	_Width,	BS			), MainTex				);
			GUI.DrawTextureWithTexCoords(new Rect(X,			Y+BS,			BS,		_Height-BS*2	), MainTex, Rotate90CW	);
			GUI.DrawTextureWithTexCoords(new Rect(X+_Width-BS,	Y+BS,			BS,		_Height-BS*2	), MainTex, Rotate90CW	);
		}
	}
}