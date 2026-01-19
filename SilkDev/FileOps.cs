using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using DirInfo=System.IO.DirectoryInfo;

namespace SilkDev;

public static class FileOps
{
	public static void		WriteFile		(string FileName, byte[] Data		) => File.WriteAllBytes			(FileName, Data);
	public static void		WriteFile		(string FileName, string Data		) => File.WriteAllText			(FileName, Data);
	public static Task		WriteFileAsync	(string FileName, byte[] Data		) => File.WriteAllBytesAsync	(FileName, Data);
	public static Task		WriteFileAsync	(string FileName, string Data		) => File.WriteAllTextAsync		(FileName, Data);
	public static void		AppendFile		(string Path, string Data			) => File.AppendAllText			(Path, DevStrings.NewLine+Data);
	public static string	ReadFile		(string FileName					) => File.ReadAllText			(FileName);
	public static byte[]	ReadFileBytes	(string FileName, bool Shared=false	) => ReadAllBytes				(FileName, Shared);
	public static string	PathCombine		(string P1, string P2				) => Path.Combine				(P1, P2);
	public static string	PathCombine		(params string[] Parts				) => Parts.Aggregate			(Path.Combine);
	public static char[]	InvalidNameChars(									) => Path.GetInvalidFileNameChars();
	public static string	FixFileName		(string P							) => string.Join				('_', P.Split(InvalidNameChars()));
	public static string	GetFileName		(string P							) => Path.GetFileName			(P);
	public static string	GetDirectoryName(string P							) => Path.GetDirectoryName		(P);
	public static string[]	GetDirFiles		(string Dir, string Pattern="*"		) => Directory.GetFiles			(Dir, Pattern);
	public static bool		FileExists		(string P							) => File.Exists				(P);
	public static bool		DirectoryExists	(string P							) => Directory.Exists			(P);
	public static DirInfo	CreateDirectory	(string P							) => Directory.CreateDirectory	(P);
	public static void		FileCopy		(string Source, string Destination	) => File.Copy					(Source, Destination);
	public static void		FileMove		(string Source, string Destination	) => File.Move					(Source, Destination);
	public static void		FileDelete		(string Source						) => File.Delete				(Source);
	public static string	GetPluginPath										  => GetDirectoryName			(Assembly.GetCallingAssembly().Location);
	private static byte[]	ReadAllBytes	(string FileName, bool Shared		) {
		if(!Shared)
			return File.ReadAllBytes(FileName);
		using MemoryStream MS=new();
		new FileStream(FileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite).CopyTo(MS);
		byte[] Bytes=MS.ToArray();
		return Bytes;
	}

	//Shorthands to use during debugging
	public static string	Ser				(params object[] Obj				) => JSON.JsonUtils.Serialize(Obj.Length==1 ? Obj[0] : Obj);
	public static void		LogSer			(params object[] Obj				) => Log.Info(Obj);

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