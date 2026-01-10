using InControl;
using SilkDev.Configs;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static SilkDev.DevInput.Joystick;
using GetICFunc=System.Func<InControl.InputDevice, InControl.IInputControl>;
using ConfigEntryKS=BepInEx.Configuration.ConfigEntry<BepInEx.Configuration.KeyboardShortcut>;

namespace SilkDev.DevInput;

//Returns key presses after a repeat delay. Change in keys ignores delay.
//Each InputType can have an embedded value that is returned from IsReadyValue.
//All operations act on the assumption only 1 key can be pressed at once, except IsReadyInputTypes and GetAllPressedInputs which work on multiple.
public class InputRepeatDelay<EmbeddedType>(float RepeatDelay=0.5f)
{
	//Members
	private readonly List<InputType> Inputs=[], LastPressedInputs=[];
	public IReadOnlyList<InputType> GetInputs => Inputs.AsReadOnly();
	public float RepeatDelay=RepeatDelay;
	public DateTime LastPressTime { get; private set; } = DateTime.MinValue; //Reset to MinValue when nothing pressed

	//Joystick members
	public float JoystickAngleDeviation=20, JoystickMinMagnitude=.4f; //See Joystick.GetOrdinalDirectionAndMagnitude
	public bool JoystickMultiplyReturnValueByMagnitude=true; //Only for EmbeddedType=float/double/decimal
	private readonly record struct LastJoystickInfo(int WhichFrame, Direction Direction, float Magnitude);
	private readonly LastJoystickInfo[] StickInfo=[new(-1, Direction.None, 0), new(-1, Direction.None, 0)]; //Cache the left[0]/right[1] stick info to only be calculated once per frame

	//Base input type
	public abstract class InputType(EmbeddedType? ReturnValue=default)
	{
		public virtual EmbeddedType? ReturnValue { get; set; } = ReturnValue;
		public abstract bool IsPressed { get; }
		public static implicit operator bool(InputType IT) => IT.IsPressed;

		//Tuple conversions for cleaner creation
		public static implicit operator InputType((bool IgnoreExtra,ConfigEntryKS CE							) v ) => new IRDShortcutKeyConfig	(v.  CE, v.IgnoreExtra					);
		public static implicit operator InputType((bool IgnoreExtra,ConfigEntryKS CE, EmbeddedType? ReturnValue	) v ) => new IRDShortcutKeyConfig	(v.  CE, v.IgnoreExtra,	v.ReturnValue	);
		public static implicit operator InputType( ConfigEntryTKeyboardShortcut KSCE								) => new IRDShortcutKeyConfig	(  KSCE									);
		public static implicit operator InputType((ConfigEntryTKeyboardShortcut KSCE, EmbeddedType? ReturnValue	) v ) => new IRDShortcutKeyConfig	(v.KSCE,				v.ReturnValue	);
		public static implicit operator InputType( GetICFunc					GICF								) => new IRDInputControl		(  GICF									);
		public static implicit operator InputType((GetICFunc					GICF, EmbeddedType? ReturnValue	) v ) => new IRDInputControl		(v.GICF,				v.ReturnValue	);
		public static implicit operator InputType((bool IsLeftStick, Direction   Dir							) v ) => new IRDJoystickInput		(v.IsLeftStick, v.Dir					);
		public static implicit operator InputType((bool IsLeftStick, Direction   Dir, EmbeddedType? ReturnValue	) v ) => new IRDJoystickInput		(v.IsLeftStick, v.Dir,	v.ReturnValue	);
		public static implicit operator InputType( KeyCode						  KC								) => new IRDKeyCode				(    KC									);
		public static implicit operator InputType((KeyCode						  KC, EmbeddedType? ReturnValue	) v ) => new IRDKeyCode				(v.  KC,				v.ReturnValue	);
	}

	//Get the result of the delayed IsPressed checks on the first found key. Returns null on no input.
	private readonly bool IsReferenceType=!typeof(EmbeddedType).IsValueType;
	public InputType? IsReadyInputType => IsReadyInputTypesReal(true)?.First();
	public EmbeddedType? IsReadyValue =>
		!IsReferenceType ? throw new InvalidOperationException("You cannot use this function with a ValueType as C# won’t make a Nullable<T> on unconstrained generics. Use IsReadyValueVType instead.") :
		IsReadyInputType is InputType Input ? Input.ReturnValue : default;
	public object? IsReadyValueVType => IsReadyInputType is InputType Input ? Input.ReturnValue : null;
	public bool IsReady => IsReadyInputType is not null;
	public static implicit operator bool(InputRepeatDelay<EmbeddedType> IRD) => IRD.IsReady;

	//Get the result of the delayed IsPressed check for multiple keys. Returns null on no inputs.
	public  IEnumerable<InputType>? IsReadyInputTypes => IsReadyInputTypesReal(false);
	private IEnumerable<InputType>? IsReadyInputTypesReal(bool CheckSingle)
	{
		//If no input then just reset everything
		IEnumerable<InputType> CurInput=CheckSingle ? GetAllPressedInputs.Take(1) : GetAllPressedInputs;
		if(CurInput.Any()==false) {
			LastPressedInputs.Clear();
			LastPressTime=DateTime.MinValue;
			return null;
		}

		//If value has changed then reset the delay counter
		if(!CurInput.SequenceEqual(LastPressedInputs)) {
			LastPressedInputs.Clear();
			LastPressedInputs.AddRange(CurInput);
			LastPressTime=DateTime.MinValue;
		}

		//If enough time has not passed, exit now
		if((DateTime.Now-LastPressTime).TotalSeconds<RepeatDelay)
			return null;

		//Update the last press time and return success
		LastPressTime=DateTime.Now;
		return CurInput;
	}

	//Return the inputs that succeeded. Does not check the repeat delay.
	public IEnumerable<InputType> GetAllPressedInputs => Inputs.Where(static Input => Input.IsPressed);

	//Add and remove from the input list
	public InputRepeatDelay(float RepeatDelay=0.5f, params InputType[] Inputs) : this(RepeatDelay) =>
		Add(Inputs);
	public void Add(params InputType[] Inputs)
	{
		this.Inputs.AddRange(Inputs);
		foreach(InputType I in Inputs)
			if(I is IRDJoystickInput J)
				J.Parent=this;
	}
	private bool RemoveItem(Predicate<InputType> FindItem)
	{
		int Index=Inputs.FindIndex(FindItem);
		if(Index>=0)
			Inputs.RemoveAt(Index);
		return Index>=0;
	}
	public bool RemoveItem(ConfigEntryKS				Input	) => IRDShortcutKeyConfig	.Remove(this, Input					);
	public bool RemoveItem(ConfigEntryTKeyboardShortcut	Input	) => IRDShortcutKeyConfig	.Remove(this, Input.CE				);
	public bool RemoveItem(KeyCode KeyCode						) => IRDKeyCode				.Remove(this, KeyCode				);
	public bool RemoveItem(GetICFunc GetICF						) => IRDInputControl		.Remove(this, GetICF				);
	public bool RemoveItem(bool IsLeftStick, Direction Direction) => IRDJoystickInput		.Remove(this, IsLeftStick, Direction);

	//-------------------------The different input types-------------------------
	//Monitor a config shortcut key
	public class IRDShortcutKeyConfig(ConfigEntryTKeyboardShortcut Input, EmbeddedType? Value=default) : InputType(Value)
	{
		public ConfigEntryTKeyboardShortcut Input=Input;
		public override bool IsPressed => Input.IsPressed();
		internal static bool Remove(InputRepeatDelay<EmbeddedType> Parent, ConfigEntryKS RemInput) =>
			Parent.RemoveItem(I => I is IRDShortcutKeyConfig TestInput && TestInput.Input.CE==RemInput);
		public IRDShortcutKeyConfig(ConfigEntryKS Input, bool IgnoreExtra, EmbeddedType? Value=default) :
			this(new ConfigEntryTKeyboardShortcut(Input) { IgnoreExtraKeysOnShortcut=IgnoreExtra }, Value) { }
	}

	//Monitor a standard key
	public class IRDKeyCode(KeyCode KeyCode, EmbeddedType? Value=default) : InputType(Value)
	{
		public KeyCode KeyCode=KeyCode;
		public override bool IsPressed => Input.GetKey(KeyCode);
		internal static bool Remove(InputRepeatDelay<EmbeddedType> Parent, KeyCode KeyCode) =>
			Parent.RemoveItem(I => I is IRDKeyCode Input && Input.KeyCode==KeyCode);
	}

	//Monitor input from the current active device
	public class IRDInputControl(GetICFunc GetICF, EmbeddedType? Value=default) : InputType(Value)
	{
		public GetICFunc GetICF=GetICF;
		public override bool IsPressed => GetICF(InputManager.ActiveDevice).IsPressed;
		internal static bool Remove(InputRepeatDelay<EmbeddedType> Parent, GetICFunc GetICF) =>
			Parent.RemoveItem(I => I is IRDInputControl Input && Input.GetICF==GetICF);
	}

	//Monitor joysticks
	public class IRDJoystickInput(bool IsLeftStick, Direction Direction, EmbeddedType? Value=default) : InputType(Value)
	{
		//Members
		public InputRepeatDelay<EmbeddedType> Parent { get; internal set; } = null!; //Set during list construction
		public bool IsLeftStick=IsLeftStick;
		public Direction Direction=Direction;
		public float Magnitude { get; private set; } //Set when IsPressed succeeds

		//Override/default functions
		public override bool IsPressed => CheckIsPressed();
		public override EmbeddedType? ReturnValue
		{
			get => !Parent.JoystickMultiplyReturnValueByMagnitude
				? base.ReturnValue
				: base.ReturnValue is float   f ? (EmbeddedType)(object)(f*Magnitude)
				: base.ReturnValue is double  d ? (EmbeddedType)(object)(d*Magnitude)
				: base.ReturnValue is decimal m ? (EmbeddedType)(object)(m*(decimal)Magnitude)
				: base.ReturnValue;
			set =>base.ReturnValue=value;
		}
		internal static bool Remove(InputRepeatDelay<EmbeddedType> Parent, bool IsLeftStick, Direction Direction) =>
			Parent.RemoveItem(I => I is IRDJoystickInput Input && Input.IsLeftStick==IsLeftStick && Input.Direction==Direction);

		//Checks if pressed and sets magnitude if so
		private bool CheckIsPressed()
		{
			//Update the stick information if new frame
			int StickNum=IsLeftStick ? 0 : 1;
			if(Parent.StickInfo[StickNum].WhichFrame!=Time.frameCount) {
				Direction Dir=GetOrdinalDirectionAndMagnitude(IsLeftStick, Parent.JoystickAngleDeviation, Parent.JoystickMinMagnitude, out float Mag);
				Parent.StickInfo[StickNum]=new LastJoystickInfo(Time.frameCount, Dir, Mag);
			}

			//Check if pressed
			bool Matching=Parent.StickInfo[StickNum].Direction==Direction;
			if(Matching)
				Magnitude=Parent.StickInfo[StickNum].Magnitude;
			return Matching;
		}
	}
}