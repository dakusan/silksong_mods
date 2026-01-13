using System.Text.RegularExpressions;
using Newtonsoft.Json;
using StringWriter=System.IO.StringWriter;

namespace SilkDev.JSON;

public static class JsonUtils
{
	public static string Serialize(
		object Obj,					//The object to serialize
		bool Compact		=false,	//If false, add Formatting.Indented
		bool TabIndent		=true ,	//If true, use tab indentions instead of spaces
		bool UnixNewLine	=true ,	//If true, use \n instead of \r\n
		bool Sorted			=false,	//If true, add SortedConverter
		bool TrailingCommas	=false  //If true, commas are added to the last item of lists/objects. Compact=false required.
	) =>
		Serialize_Conv(Obj, Compact:Compact, TabIndent:TabIndent, UnixNewLine:UnixNewLine, Sorted:Sorted, TrailingCommas:TrailingCommas);

	private static readonly Regex AddTrailingCommasRegEx=new(@"([^,{\[])(\r?\n[ \t]*)(?=[}\]])", RegexOptions.Compiled);
	public static string Serialize_Conv(
		object Obj, bool Compact=false, bool TabIndent=true, bool UnixNewLine=true, bool Sorted=false, bool TrailingCommas=false,	//See Serialize()
		params System.Collections.Generic.List<JsonConverter> Converters															//Extra converters to use
	) {
		using StringWriter	 SW								=new();
		using JsonTextWriter JTW							=new(SW);
		if(!Compact)		 JTW.Formatting					=Formatting.Indented;
		if(TabIndent)		(JTW.IndentChar,JTW.Indentation)=('\t', 1);
		if(UnixNewLine)		 SW.NewLine						=Misc.NewLine.ToString();
		if(Sorted)			 Converters.Add					(new SortedConverter(true));

		JsonSerializer.CreateDefault(
			new JsonSerializerSettings { Converters=Converters }
		).Serialize(JTW, Obj);

		return !Compact && TrailingCommas
			? AddTrailingCommasRegEx.Replace(SW.ToString(), "$1,$2")
			: SW.ToString();
	}

	public static T Deserialize<T>(string Data) =>
		JsonConvert.DeserializeObject<T>(Data)!;
	public static void Deserialize<T>(string Data, out T RetVar) =>
		RetVar=JsonConvert.DeserializeObject<T>(Data)!;

	//Runs specified classes through FieldPropConverter
	public static string Serialize_FPC<T>(object Obj, bool OutputNulls=true, bool Compact=false, bool TabIndent=true, bool UnixNewLine=true, bool Sorted=false, bool TrailingCommas=false) where T: class =>
		Serialize_Conv(Obj, Compact:Compact, TabIndent:TabIndent, UnixNewLine:UnixNewLine, Sorted:Sorted, TrailingCommas:TrailingCommas, Converters:new FieldPropConverter<T>(OutputNulls));

	public static T Deserialize_FPC<T, T2>(string Data) where T2: class =>
		JsonConvert.DeserializeObject<T>(Data, new FieldPropConverter<T2>())!;
}