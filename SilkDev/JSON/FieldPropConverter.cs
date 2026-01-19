using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace SilkDev.JSON;

//Convert a class by setting its fields and properties, no matter their accessibility status
public class FieldPropConverter<MyClass> : JsonConverter<MyClass> where MyClass : class
{
	//Field and property accessor types
	private enum ValueType { Int, String, Float, StringArray, Unknown }
	private abstract class Accessor(Type T)
	{
		public ValueType MyValueType=
			  T==typeof(int		) ? ValueType.Int
			: T==typeof(float	) ? ValueType.Float
			: T==typeof(string	) ? ValueType.String
			: T==typeof(string[]) ? ValueType.StringArray
			:						ValueType.Unknown;
		public abstract object? Get(MyClass? O);
		public abstract void Set(MyClass O, JToken? Val);
		public object? ConvertValue(JToken? Val) =>
			MyValueType switch {
				ValueType.Int			=> Val?.ToObject<int>()		?? 0,
				ValueType.String		=> Val?.ToObject<string>()	?? string.Empty,
				ValueType.Float			=> Val?.ToObject<float>()	?? 0f,
				ValueType.StringArray	=> Val?.ToObject<string[]>()?? null,
				_						=> Val?.ToObject(T)
			};
	}
	private class FieldAccessor(FieldInfo FI) : Accessor(FI.FieldType)
	{
		public override object? Get(MyClass? O) => O==null ? null : FI.GetValue(O);
		public override void Set(MyClass O, JToken? Val) => FI.SetValue(O, ConvertValue(Val));
	}
	private class PropertyAccessor(PropertyInfo PI) : Accessor(PI.PropertyType)
	{
		public override object? Get(MyClass? O) => O==null ? null : PI.GetValue(O);
		public override void Set(MyClass O, JToken? Val) => PI.SetValue(O, ConvertValue(Val));
	}

	//Store the valid accessors by name
	private readonly Dictionary<string, Accessor> Accessors=[];
	private void AddAccessor(string Name, Accessor A) => Misc.IFF(
		!Accessors.ContainsKey(Name) && !Name.Contains("k__BackingField"),
		() => Accessors[Name]=A
	);
	private readonly bool OutputNulls;

	//Pull all the accessors during construction
	public FieldPropConverter(bool OutputNulls=true)
	{
		this.OutputNulls=OutputNulls;
		BindingFlags BF=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.DeclaredOnly;
		for(Type CurrentType=typeof(MyClass); CurrentType!=null && CurrentType!=typeof(object); CurrentType=CurrentType.BaseType) {
			foreach(FieldInfo FI in CurrentType.GetFields(bindingAttr:BF))
				AddAccessor(FI.Name, new FieldAccessor(FI));
			foreach(PropertyInfo PI in CurrentType.GetProperties(bindingAttr:BF))
				AddAccessor(PI.Name, new PropertyAccessor(PI));
		}
	}

	//For any value that has an accessor, set it
	public override MyClass? ReadJson(JsonReader Reader, Type ObjectType, MyClass? ExistingValue, bool HasExistingValue, JsonSerializer Serializer)
	{
		MyClass Instance=(MyClass)Activator.CreateInstance(typeof(MyClass));
		foreach((string Key, JToken? Value) in JObject.Load(Reader))
			Accessors.Get(Key)?.Set(Instance, Value);
		return Instance;
	}

	//For any value that has an accessor, output it
	public override void WriteJson(JsonWriter Writer, MyClass? Value, JsonSerializer Serializer)
	{
		Writer.WriteStartObject();
		foreach((string Key, Accessor AValue) in Accessors) {
			object? Val=AValue.Get(Value);
			if(Val==null && !OutputNulls)
				continue;
			Writer.WritePropertyName(Key);
			if(AValue.MyValueType==ValueType.StringArray)
				Serializer.Serialize(Writer, Val);
			else
				Writer.WriteValue(Val);
		}
		Writer.WriteEndObject();
	}
}