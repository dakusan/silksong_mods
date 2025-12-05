using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace SilkDev.JSON;

//Sorts lists and dictionary by numeric or alphabetical order (Good for diffing)
public class SortedConverter(bool AllowUse=false) : JsonConverter
{
	//Block the rest of the engine from using this class
	private readonly bool AllowUse=AllowUse;

	private object? DefaultKeySelector(object? Key) => Key?.ToString() ?? Misc.Empty;
	private object? NumericKeySelector(object? Key) =>
		  Key is int v ? v
		: (Key != null && int.TryParse(Key.ToString(), out int parsed) ? parsed
		: int.MaxValue
	);

	public override bool CanConvert(Type ObjectType) =>
		AllowUse && (
			   typeof(IDictionary).IsAssignableFrom(ObjectType)
			|| (ObjectType.IsGenericType && ObjectType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
			|| typeof(IList).IsAssignableFrom(ObjectType)
			|| (ObjectType.IsGenericType && ObjectType.GetGenericTypeDefinition() == typeof(List<>))
		);

	public override void WriteJson(JsonWriter Writer, object? Value, JsonSerializer Serializer) =>
		(ConvertJson(Value, Serializer) ?? JValue.CreateNull()).WriteTo(Writer);

	private JToken? ConvertJson(object? Value, JsonSerializer Serializer) =>
		  Value==null ? null
		: Value is IDictionary Dict ? (JToken?)SortAsDict(Dict, Serializer)
		: Value is IList Arr ? SortAsList(Arr, Serializer)
		: null;

	private JObject? SortAsDict(IDictionary Dict, JsonSerializer Serializer) =>
		new(
			GetDictKeyValuePairs(Dict, GetSorter(Dict.Keys.Cast<object>()))
				.Select(KV => new JProperty(
					KV.Key?.ToString() ?? Misc.Empty,
					KV.Value is null ? JValue.CreateNull() : JToken.FromObject(KV.Value, Serializer)
				))
		);

	private IOrderedEnumerable<KeyValuePair<object, object>> GetDictKeyValuePairs(IDictionary Dict, Func<object?, object?> KeySelector) =>
		Dict.Cast<object>().Select(static o => new KeyValuePair<object, object>(
			o.GetType().GetProperty("Key")!.GetValue(o),
			o.GetType().GetProperty("Value")!.GetValue(o)
		)).OrderBy(KV => KeySelector(KV.Key));

	private JArray? SortAsList(IList Arr, JsonSerializer Serializer) =>
		new(
			Arr.Cast<object>().OrderBy(GetSorter(Arr.Cast<object>())).Select(Item =>
				Item is null ? JValue.CreateNull() : JToken.FromObject(Item, Serializer)
		));

	private Func<object?, object?> GetSorter(IEnumerable<object> TheList) =>
		TheList.All(static Item =>
			Item is int ||
			(Item is string s && int.TryParse(s, out _))
		) ? NumericKeySelector : DefaultKeySelector;

	//This should NEVER be used
	public override object? ReadJson(JsonReader Reader, Type ObjectType, object? ExistingValue, JsonSerializer Serializer) =>
		Reader.TokenType==JsonToken.Null ? null : Serializer.Deserialize(Reader, ObjectType); //JToken.Load(Reader)
}

#if NO_COMPILE
class TestObject
{
	public Dictionary<int, string> T1=new() { { 1, "a" }, { 10, "b" }, { 100, "c" }, { 11, "d" }, { 2, "e" } };
	public Dictionary<string, string> T2=new() { { "1", "a" }, { "10", "b" }, { "100", "c" }, { "11", "d" }, { "2", "e" } };
	public Dictionary<string, string> T3=new() { { "1", "a" }, { "10", "b" }, { "100", "c" }, { "11", "d" }, {"x", "q" }, { "2", "e" } };
	public List<int> T4=[1, 10, 100, 11, 2];
	public List<string> T5=["1", "10", "100", "11", "2"];
	public List<string> T6=["1", "10", "100", "11", "x", "2"];
	public List<object> T7=["1", 10, "100", "11", "2"];
	public List<object> T8=["1", 10, "100", "11", "x", "2"];
}
//Result:
//{"T1":{"1":"a","2":"e","10":"b","11":"d","100":"c"},"T2":{"1":"a","2":"e","10":"b","11":"d","100":"c"},"T3":{"1":"a","10":"b","100":"c","11":"d","2":"e","x":"q"},"T4":[1,2,10,11,100],"T5":["1","2","10","11","100"],"T6":["1","10","100","11","2","x"],"T7":["1","2",10,"11","100"],"T8":["1",10,"100","11","2","x"]}
#endif