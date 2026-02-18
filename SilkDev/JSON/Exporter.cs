using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace SilkDev.JSON;

[AttributeUsage(AttributeTargets.Field|AttributeTargets.Property, Inherited=true, AllowMultiple=false)] public sealed class ExpNoAttribute	: Attribute { } //Do not include
[AttributeUsage(AttributeTargets.Field|AttributeTargets.Property, Inherited=true, AllowMultiple=false)] public sealed class ExpYesAttribute	: Attribute { } //Include (for private members)
[AttributeUsage(AttributeTargets.Field|AttributeTargets.Property, Inherited=true, AllowMultiple=false)] public sealed class NullYesAttribute: Attribute { } //Include even if null
[AttributeUsage(AttributeTargets.Field|AttributeTargets.Property, Inherited=true, AllowMultiple=false)] public sealed class ExpNameAttribute(string Name) : Attribute { public string Name=Name; } //Rename

/// <summary>
/// Reflection-based attribute-driven JSON exporter built on Newtonsoft.Json.
///
/// - Serializes non-static, non-const instance fields and readable properties.
/// - Public members are included by default.
/// - [ExpNo] excludes a member.
/// - [ExpYes] forces inclusion of a non-public member.
/// - [ExpName(string)] renamed the member.
/// - Null values are omitted unless [NullYes] is applied.
/// - If the object implements IExpOverride, its ExpOverride getter value is serialized directly instead of enumerating members.
/// - If the object is an IExpFieldOrder:
///		- Attributes are reordered to fit its given order.
///		- Duplicate named members will order by derived-first.
///		- Unnamed members will stay in their original order.
/// </summary>
public sealed class Exporter : DefaultContractResolver
{
	//Handle IExpOverride
	public interface IExpOverride { public string ExpOverride { get; } } //A class will be serialized through this function
	protected override JsonContract CreateContract(Type ObjectType)
	{
		JsonContract C=base.CreateContract(ObjectType);
		if(typeof(IExpOverride).IsAssignableFrom(ObjectType))
			C.Converter=ExpOverrideConverter.Instance;
		if(ObjectType==typeof(double) || ObjectType==typeof(double?))
			C.Converter=DoubleG17.Instance;
		return C;
	}
	private abstract class LocalJsonWriteConverter<T>(bool AllowUse=false) : JsonConverter
	{
		private readonly bool AllowUse=AllowUse; //Block the rest of the engine from using this class
		public override bool CanConvert(Type ObjectType) => AllowUse && typeof(T).IsAssignableFrom(ObjectType);
		public override bool CanRead => false;
		public override object? ReadJson(JsonReader _, Type __, object? ___, JsonSerializer ____) => throw new NotSupportedException();
	}
	private sealed class ExpOverrideConverter(bool AllowUse=false) : LocalJsonWriteConverter<IExpOverride>(AllowUse)
	{
		public static readonly ExpOverrideConverter Instance=new(true);
		public override void WriteJson(JsonWriter Writer, object? Value, JsonSerializer _) => Writer.WriteValue(Value is null ? null : ((IExpOverride)Value).ExpOverride);
	}
	private sealed class DoubleG17(bool AllowUse=false) : LocalJsonWriteConverter<double>(AllowUse)
	{
		public static readonly DoubleG17 Instance=new(true);
		public override void WriteJson(JsonWriter Writer, object? Value, JsonSerializer _)
		{
			var Str=((double)Value!).ToString("G17", CultureInfo.InvariantCulture);
			if(Str.Contains('.'))
				Str=Str.TrimEnd('0').TrimEnd('.');
			Writer.WriteRawValue(Str);
		}
	}

	//Get list of members to serialize from a class
	public interface IExpFieldOrder { public static string[]? ExpFieldOrder { get; } } //Order of fields
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

		//If no order, return as is
		if(
			!typeof(IExpFieldOrder).IsAssignableFrom(ObjectType)
			|| ObjectType.GetProperty(nameof(IExpFieldOrder.ExpFieldOrder), BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic)?.GetValue(null) is not string[] Ordered
			|| Ordered.Length==0
		)
			return Members;

		//Reorder according to the rules
		Dictionary<string, int> DOrder=Ordered.Select((Name, Index) => (Name, Index)).ToDictionary(static KVP => KVP.Name, static KVP => KVP.Index);
		return [..
			Members.Select((Member, Index) => (
				SortOrder:DOrder.TryGetValue(Member.Name, out int OrderNum) ? OrderNum+Index/1000.0 : 10000+Index,
				M:Member
			)).OrderBy(KVP => KVP.SortOrder).Select(KVP => KVP.M)
		];
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

		//Rename
		string? Name=Member.GetCustomAttribute<ExpNameAttribute>(inherit:true)?.Name;
		if(Name is not null)
			JP.PropertyName=Name;

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