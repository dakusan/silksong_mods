using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace SilkDev.JSON;

[AttributeUsage(AttributeTargets.Field|AttributeTargets.Property, Inherited=true, AllowMultiple=false)] public sealed class ExpNoAttribute	: Attribute { } //Do not include
[AttributeUsage(AttributeTargets.Field|AttributeTargets.Property, Inherited=true, AllowMultiple=false)] public sealed class ExpYesAttribute	: Attribute { } //Include (for private members)
[AttributeUsage(AttributeTargets.Field|AttributeTargets.Property, Inherited=true, AllowMultiple=false)] public sealed class NullYesAttribute: Attribute { } //Include even if null

/// <summary>
/// Reflection-based attribute-driven JSON exporter built on Newtonsoft.Json.
///
/// - Serializes non-static, non-const instance fields and readable properties.
/// - Public members are included by default.
/// - [ExpNo] excludes a member.
/// - [ExpYes] forces inclusion of a non-public member.
/// - Null values are omitted unless [NullYes] is applied.
/// - If the object implements IExpOverride, its ExpOverride getter value is serialized directly instead of enumerating members.
/// </summary>
public sealed class Exporter : DefaultContractResolver
{
	//Handle IExpOverride
	public interface IExpOverride { public string ExpOverride { get; } } //A class will be serialized through this function
	protected override JsonContract CreateContract(Type objectType)
	{
		JsonContract C=base.CreateContract(objectType);
		if(typeof(IExpOverride).IsAssignableFrom(objectType))
			C.Converter=ExpOverrideConverter.Instance;
		return C;
	}
	private sealed class ExpOverrideConverter(bool AllowUse=false) : JsonConverter
	{
		private readonly bool AllowUse=AllowUse; //Block the rest of the engine from using this class
		public static readonly ExpOverrideConverter Instance=new(true);
		public override bool CanConvert(Type ObjectType) => AllowUse && typeof(IExpOverride).IsAssignableFrom(ObjectType);
		public override bool CanRead => false;
		public override object? ReadJson(JsonReader _, Type __, object? ___, JsonSerializer ____) => throw new NotSupportedException();
		public override void WriteJson(JsonWriter Writer, object? Value, JsonSerializer _) => Writer.WriteValue(Value is null ? null : ((IExpOverride)Value).ExpOverride);
	}

	//Get list of members to serialize from a class
	protected override List<MemberInfo> GetSerializableMembers(Type ObjectType)
	{
		var Members=new List<MemberInfo>();

		//Go through the class inheritance chain from top to bottom
		for(Type? Cur=ObjectType; Cur is not null; Cur=Cur.BaseType) {
			const BindingFlags BF=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly;
			foreach(FieldInfo Fld in Cur.GetFields(BF))
				if(!(
					   Fld.IsStatic || Fld.IsLiteral || Has<ExpNoAttribute>(Fld)//No static, const, or ExpNo
					|| (!Fld.IsPublic && !Has<ExpYesAttribute>(Fld))			//Only public, or private w/ ExpYes
				))
					Members.Add(Fld);

			MethodInfo? Getter;
			foreach(PropertyInfo Prop in Cur.GetProperties(BF))
				if(!(
					   (Getter=Prop.GetGetMethod(nonPublic: true)) is null || Prop.GetIndexParameters().Length!=0	//Must be a getter with no properties
					|| Getter.IsStatic || Has<ExpNoAttribute>(Prop)													//Not static or ExpNo
					|| (!Getter.IsPublic && !Has<ExpYesAttribute>(Prop))											//Only public, or private w/ ExpYes
				))
					Members.Add(Prop);
		}

		return Members;
	}

	//Overwrite settings per property
	protected override JsonProperty CreateProperty(MemberInfo Member, MemberSerialization MemberSerialization)
	{
		//Null handling
		var JP=base.CreateProperty(Member, MemberSerialization);
		JP.NullValueHandling=Has<NullYesAttribute>(Member)
			? NullValueHandling.Include
			: NullValueHandling.Ignore;

		//Force members with ExpYes
		if(Has<ExpYesAttribute>(Member)) {
			JP.Readable=true;
			JP.ValueProvider=CreateMemberValueProvider(Member);
		}

		return JP;
	}

	private static bool Has<T>(MemberInfo MI) where T : Attribute => Attribute.IsDefined(MI, typeof(T), inherit:true);
}

/* A JsonConverter method I may revisit later that does all the work of the converter, putting everything into maps and arrays. Which probably need be use JSON objects instead
private object BuildObject(object Obj, MemberInfo? MI=null)
{
	var T=Obj.GetType();
	var Map=new Dictionary<string, object?>(StringComparer.Ordinal);
	object? Val;
	MethodInfo? Getter;

	IReadOnlyList<string>? Ignores =Get<ExpIgnoreAttribute >(MI)?.Names;
	IReadOnlyList<string>? Includes=Get<ExpIncludeAttribute>(MI)?.Names;
	bool NameAllowed(string Name) =>
		  !(Ignores?.Contains(Name) ?? false)
		&& (Includes?.Contains(Name) ?? true);

	//Fields
	foreach(FieldInfo Fld in T.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic))
		if(NameAllowed(Fld.Name) && !(
			   Fld.IsStatic || Fld.IsLiteral || Has<ExpNoAttribute>(Fld)        //No static, const, or ExpNo
			|| (!Fld.IsPublic && !Has<ExpYesAttribute>(Fld))                    //Only public, or private w/ ExpYes
			|| ((Val=Fld.GetValue(Obj)) is null && !Has<NullYesAttribute>(Fld)) //Only take null if NullYes
		))
			_=Map.TryAdd(Fld.Name, Convert(Val, Fld));

	//Properties
	foreach(PropertyInfo Prop in T.GetProperties(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic))
		if(NameAllowed(Prop.Name) && !(
			   (Getter=Prop.GetGetMethod(nonPublic: true)) is null || Prop.GetIndexParameters().Length!=0  //Must be a getter with no properties
			|| Getter.IsStatic || Has<ExpNoAttribute>(Prop)                                                 //Not static or ExpNo
			|| (!Getter.IsPublic && !Has<ExpYesAttribute>(Prop))                                            //Only public, or private w/ ExpYes
			|| ((Val=Prop.GetValue(Obj, index: null)) is null && !Has<NullYesAttribute>(Prop))              //Only take null if NullYes
		))
			_=Map.TryAdd(Prop.Name, Convert(Val, Prop));

	return Map;
}

private object? Convert(object? Obj, MemberInfo? MI) =>
	  Obj is IExpOverride IEO ? IEO.ExpOverride
	: Obj is null or string or decimal or DateTime or DateTimeOffset or Guid or TimeSpan || Obj.GetType().IsPrimitive || Obj.GetType().IsEnum ? Obj
	: Obj is IDictionary Dict ? Dict.GetEnumerator().AsEnumerable<DictionaryEntry>().ToDictionary(
		static KVP => KVP.Key?.ToString() ?? "",
		KVP => Convert(KVP.Value, MI)
	  )
	: Obj is IEnumerable En ? En.Cast<object>().Select(Val => Convert(Val, MI))
	: BuildObject(Obj, MI);
*/