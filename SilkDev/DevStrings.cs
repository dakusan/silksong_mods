using System;

namespace SilkDev;

public static class DevStrings
{
	//Ensure richText markup is treated as literal text
	public static string SafeRich(string Message) =>
		Message.IndexOf('<')==-1 ? Message :  Message.Replace("<", "<<i></i>"); //Yes, this is really the best way

	//Get steam username
	public const string UsernameErrorString="*SILKDEV NO NAME*"; //Tells the server the user’s username couldn’t be looked up
	public static string SteamUsername { get {
		try {
			return
				!Steamworks.SteamAPI.IsSteamRunning() ? throw new Exception("Steam not running") :
				Steamworks.SteamFriends.GetPersonaName() ?? throw new Exception("Lookup failed");
		} catch {
			return UsernameErrorString;
		}
	} }

	//If string byte contents is longer than MaxByteSize then shorten it so that when appended with AppendOnOverrun it fits within the maximum byte size
	private static readonly System.Text.Encoding UTF8=System.Text.Encoding.UTF8;
	public static string UTF8Cut(string Str, int MaxByteSize, string AppendOnOverrun) =>
		UTF8.GetByteCount(Str)<=MaxByteSize ? Str : (string)UTF8CutReal(UTF8.GetBytes(Str), MaxByteSize, AppendOnOverrun, true);
	public static string UTF8Cut(byte[] UTF8Bytes, int MaxByteSize, string AppendOnOverrun) =>
		UTF8Bytes.Length<=MaxByteSize ? UTF8.GetString(UTF8Bytes) : (string)UTF8CutReal(UTF8Bytes, MaxByteSize, AppendOnOverrun, true);
	public static byte[] UTF8CutB(string Str, int MaxByteSize, string AppendOnOverrun) =>
		UTF8CutB(UTF8.GetBytes(Str), MaxByteSize, AppendOnOverrun);
	public static byte[] UTF8CutB(byte[] UTF8Bytes, int MaxByteSize, string AppendOnOverrun) =>
		UTF8Bytes.Length<=MaxByteSize ? UTF8Bytes : (byte[])UTF8CutReal(UTF8Bytes, MaxByteSize, AppendOnOverrun, false);
	private static object UTF8CutReal(byte[] Bytes, int MaxByteSize, string AppendOnOverrun, bool AsString) //Will return as byte[] or string depending on AsString
	{
		//This case is taken care of by the 4 callers of this function
		//if(Bytes.Length<=MaxByteSize)
		//	return AsString ? Encoding.UTF8.GetString(Bytes) : Bytes;

		//Edge case if user passes invalid MaxBytesSize
		if(MaxByteSize<=0)
			return AsString ? string.Empty : (byte[])[];

		//Determine if there is not enough space to display anything more than [all or part of] AppendOnOverrun
		byte[] AppendBytes=AsString ? null! : UTF8.GetBytes(AppendOnOverrun);
		int AppendBytesSize=(AsString ? UTF8.GetByteCount(AppendOnOverrun) : AppendBytes.Length);
		int End=MaxByteSize-AppendBytesSize;
		if(End<=0)
			return UTF8CutReal(
				AsString ? UTF8.GetBytes(AppendOnOverrun) : AppendBytes,
				MaxByteSize, string.Empty, AsString
			);

		//Creates a byte[] or string with minimal amounts of copies
		object CreateReturn(int GetLength)
		{
			if(!AsString) {
				byte[] Ret=new byte[GetLength+AppendBytesSize];
				Bytes.AsSpan(0, GetLength).CopyTo(Ret);
				AppendBytes.CopyTo(Ret.AsSpan(GetLength));
				return Ret;
			}

			return string.Create(
				UTF8.GetCharCount(Bytes, 0, GetLength)+AppendOnOverrun.Length,
				(Bytes, AppendOnOverrun, GetLength), static (Dest, V) => {
					int Written=UTF8.GetChars(V.Bytes.AsSpan(0, V.GetLength), Dest);
					V.AppendOnOverrun.AsSpan().CopyTo(Dest[Written..]);
				}
			);
		}

		//If we are on a 1 byte character, then nothing to chop off the end
		if(Bytes[End-1]<=0x7F)
			return CreateReturn(End);

		//Determine the number of bytes we need to chop off the end to get to a safe character
		int i=End-1;
		for(; i>0 && (Bytes[i]&0xC0)==0x80; i--) ; //Skip continuation bytes
		int Need=Bytes[i] switch { //Determine how many bytes are needed
			< 0xC0 => 1, < 0xE0 => 2,
			< 0xF0 => 3, < 0xF8 => 4,
			_ => 0
		};
		return CreateReturn(Need!=0 && End-i>=Need ? End : i);
	}

	public const string Empty=""; //Used when a const is needed (string.Empty is a static readonly)
	public const char NewLine='\n';
}