using Newtonsoft.Json;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using DirInfo=System.IO.DirectoryInfo;

namespace SilkDev;

public static class FileOps
{
	public static string SerializeToJSONSorted(object Obj) =>
		JsonConvert.SerializeObject(
			Obj,
			new JsonSerializerSettings {
				Converters={ new JSON.SortedConverter(true) },
				Formatting=Formatting.Indented
			}
		);

	public static T			DeserializeJson	<T>(string Data						) => JsonConvert.DeserializeObject<T>(Data)!;
	public static void		WriteFile		(string FileName, byte[] Data		) => File.WriteAllBytes(FileName, Data);
	public static void		WriteFile		(string FileName, string Data		) => File.WriteAllText(FileName, Data);
	public static Task		WriteFileAsync	(string FileName, byte[] Data		) => File.WriteAllBytesAsync(FileName, Data);
	public static Task		WriteFileAsync	(string FileName, string Data		) => File.WriteAllTextAsync(FileName, Data);
	public static void		AppendFile		(string Path, string Data			) => File.AppendAllText(Path, Misc.NewLine+Data);
	public static string	ReadFile		(string FileName					) => File.ReadAllText(FileName);
	public static byte[]	ReadFileBytes	(string FileName					) => File.ReadAllBytes(FileName);
	public static string	PathCombine		(string P1, string P2				) => Path.Combine(P1, P2);
	public static string	PathCombine		(params string[] Parts				) => Parts.Aggregate(Path.Combine);
	public static char[]	InvalidNameChars(									) => Path.GetInvalidFileNameChars();
	public static string	GetFileName		(string P							) => Path.GetFileName(P);
	public static string	GetDirectoryName(string P							) => Path.GetDirectoryName(P);
	public static string[]	GetDirFiles		(string Dir, string Pattern="*"		) => Directory.GetFiles(Dir, Pattern);
	public static bool		FileExists		(string P							) => File.Exists(P);
	public static bool		DirectoryExists	(string P							) => Directory.Exists(P);
	public static DirInfo	CreateDirectory	(string P							) => Directory.CreateDirectory(P);
	public static void		FileCopy		(string Source, string Destination	) => File.Copy(Source, Destination);
	public static void		FileMove		(string Source, string Destination	) => File.Move(Source, Destination);
	public static void		FileDelete		(string Source						) => File.Delete(Source);
	public static string	SerializeToJSON	(object Obj, bool Compact=false		) => JsonConvert.SerializeObject(Obj, Compact ? Formatting.None : Formatting.Indented).Replace("\r\n", "\n");
	public static T			DeserializeJson	<T, T2>(string Data) where T2: class=> JsonConvert.DeserializeObject<T>(Data, new JSON.FieldPropConverter<T2>())!; //Runs specified class through FieldPropConverter
	public static string	SerializeToJSON	<T>(object Obj, bool Compact=false, bool OutputNulls=true) where T: class => //Runs specified class through FieldPropConverter
		JsonConvert.SerializeObject(Obj, Compact ? Formatting.None : Formatting.Indented, new JSON.FieldPropConverter<T>(OutputNulls)).Replace("\r\n", "\n");

	//Shorthands to use during debugging
	public static string Ser(object Obj) => SerializeToJSON(Obj);
	public static void LogSer(object Obj) => Log.Info(SerializeToJSON(Obj));

	//Load resources
	public static Stream LoadEmbeddedResource(string Name) =>
		LoadEmbeddedResource(Name, Assembly.GetCallingAssembly());
	public static Stream LoadEmbeddedResource(string Name, Assembly Assembly) =>
		Assembly.GetManifestResourceStream(Name) ??
			throw new FileNotFoundException($"Resource '{Name}' not found. Available: {string.Join(", ", Assembly.GetManifestResourceNames())}");
	public static Stream LoadLocalFileOrResource(string Name)
	{
		Assembly Assembly=Assembly.GetCallingAssembly();
		string FileName=PathCombine(GetDirectoryName(Assembly.Location), Name);
		return FileExists(FileName) ? File.OpenRead(FileName) : LoadEmbeddedResource(Name, Assembly);
	}
	public static string[] GetResources() => Assembly.GetCallingAssembly().GetManifestResourceNames();
}