using System;
using System.Reflection;

namespace SilkDev;

//Access private fields/properties/methods
public static class Reflectors
{
	public class RField<ObjType, FieldType>(ObjType? Obj, string Name) where ObjType : class //Do not use Get() and Set() unless Obj has been set
	{
		public readonly FieldInfo FI=
			typeof(ObjType).GetField(Name, bindingAttr: BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static) ??
				throw new MemberAccessException($"Could not get field {typeof(ObjType).Name}.{Name}");
		public ObjType Obj=Obj!;
		public RField<ObjType, FieldType> UpdateObj(ObjType Obj) { this.Obj=Obj; return this; }
		public FieldType Get() => (FieldType)FI.GetValue(Obj);
		public void Set(FieldType Val) => FI.SetValue(Obj, Val);
		public static implicit operator FieldType(RField<ObjType, FieldType> rField) => rField.Get();
	}
	public class RProp<ObjType, PropType>(ObjType? Obj, string Name) where ObjType : class //Do not use Get() and Set() unless Obj has been set
	{
		public readonly PropertyInfo PI=
			typeof(ObjType).GetProperty(Name, bindingAttr: BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static) ??
				throw new MemberAccessException($"Could not get property {typeof(ObjType).Name}.{Name}");
		public ObjType Obj=Obj!;
		public RProp<ObjType, PropType> UpdateObj(ObjType Obj) { this.Obj=Obj; return this; }
		public PropType Get() => (PropType)PI.GetValue(Obj);
		public void Set(PropType Val) => PI.SetValue(Obj, Val);
		public static implicit operator PropType(RProp<ObjType, PropType> rField) => rField.Get();
	}
	public class RMethod<ObjType, RetType>(ObjType? Obj, string Name) where ObjType : class //Do not use Invoke() or InvokeArr() unless Obj has been set
	{
		public readonly MethodInfo MI=
			typeof(ObjType).GetMethod(Name, bindingAttr: BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static) ??
				throw new MemberAccessException($"Could not get private method {typeof(ObjType).Name}.{Name}");
		public ObjType Obj=Obj!;
		public RMethod<ObjType, RetType> UpdateObj(ObjType Obj) { this.Obj=Obj; return this; }
		public RetType Invoke(params object?[] args) => (RetType)MI.Invoke(Obj, args)!;
		//You may need this if there are "out" variables that need to be captured, since they will be captured into the array
		public RetType InvokeArr(object?[] args) => (RetType)MI.Invoke(Obj, args)!;
	}
}