using InControl;
using SilkDev;
using UnityEngine;

namespace NoClip;

//This duplicates HeroController.ToggleNoClip() [But allows whether to choose to flip on or off] and also turns off gravity
public class NCActivate
{
	public bool IsActive => IsEnabled && UpdateReflector()!=null; //If enabled and hero controller exists
	public Rigidbody2D GetPlayerRigidBody => HC_RB2D!.Get();
	public bool IsEnabled { get; private set; } = false;

	//All functions confirm this is set before trying to use it
	private readonly Reflectors.RField<HeroController, Rigidbody2D> HC_RB2D=new(null, "rb2d");
	private readonly Reflectors.RField<HeroController, Collider2D> HC_COL2D=new(null, "col2d");

	private static NCActivate _Self=null!; public static NCActivate Self => _Self; //Singleton

	public NCActivate()
	{
		Misc.InitSingleton(this, ref _Self);
		SilkDev.Events.GameEvents.OnUpdate += CheckKeys;
	}

	//Toggle no clip
	public void Toggle(bool Enable)
	{
		if(IsEnabled==Enable)
			return;

		//If enabling and the hero controller does not exist, then turn the config back off
		HeroController? Hero=UpdateReflector();
		if(Hero==null) {
			Config.C.ToggleNoClip.V=false;
			return;
		}

		//No gravity
		IsEnabled=Enable;

		//Noclip
		HC_COL2D.Get().enabled=!Enable;
		HC_RB2D.Get().linearVelocity=Vector2.zero;
		Hero.playerData.isInvincible=Enable;
		Hero.playerData.infiniteAirJump=Enable;
		MoveUser(0, 0);
	}

	//Check for keys
	internal void CheckKeys()
	{
		//Handle keyboard shortcuts
		if(Config.C.Key_ToggleNoClip.IsDown())
			Config.C.ToggleNoClip.V=!Config.C.ToggleNoClip;

		//Exit here if noclip is not on
		if(!IsEnabled)
			return;

		//Move user up and down during noclip mode
		const float StickMagnitudeMinimum=.2f;
		float MoveX=0, MoveY=0;
		TwoAxisInputControl LStick=InputManager.ActiveDevice.LeftStick;
		if(Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.DownArrow))
			MoveY=(Input.GetKey(KeyCode.UpArrow) ? 1 : -1);
		else if(Mathf.Abs(LStick.Y)>StickMagnitudeMinimum)
			MoveY=LStick.Y;
		if(Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow))
			MoveX=(Input.GetKey(KeyCode.RightArrow) ? 1 : -1);
		else if(Mathf.Abs(LStick.X)>StickMagnitudeMinimum)
			MoveX=LStick.X;

		if(MoveX!=0 || MoveY!=0)
			MoveUser(MoveX*0.23f*Config.C.NoClipScale, MoveY*0.33f*Config.C.NoClipScale);
	}

	//Move the user position
	private void MoveUser(float AmountX, float AmountY)
	{
		HeroController? HC=UpdateReflector();
		if(HC==null)
			return;
		Rigidbody2D RB2D=HC_RB2D;
		RB2D.position+=new Vector2(AmountX, AmountY);
		RB2D.gravityScale=0;
		RB2D.linearVelocity=Vector2.zero;
	}

	//Get the hero controller and make sure the rigid body field reflecter is up to date
	private HeroController? UpdateReflector()
	{
		HeroController HC=HeroController.instance;
		if(HC==null)
			return null;
		else if((object)HC_RB2D.Obj!=HC)
			HC_RB2D.Obj=HC_COL2D.Obj=HC;
		return HC;
	}
}