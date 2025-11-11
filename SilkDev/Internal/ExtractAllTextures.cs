using SilkDev.Configs;
using System;
using System.Collections;
using System.Security.Cryptography;
using UnityEngine;

namespace SilkDev.Internal;

//Extract all textures. Called via the config
internal class ExtractAllTextures
{
	//Static members
	internal const string TextureDirectory="Textures";
	private static bool CurrentlyRunning;

	//Instance members
	private readonly PopupMessageNoClose PM=new("Initializing process");
	private readonly MD5 PngMd5=MD5.Create();
	private readonly string DirName=FileOps.PathCombine(Misc.GetPluginPath, TextureDirectory);
	private int NumWritten=0, NumFailed=0, NumAlreadyExisted=0;
	private string CurName="?";
	private Exception? LastError;

	//Do not allow the popup to close until complete
	private class PopupMessageNoClose(string Message) : PopupMessage(Message)
	{
		protected override bool BlockClose => CurrentlyRunning;
		protected override string PressAnyKeyString => CurrentlyRunning ? "<color=red><size=20>This window will stay open until complete.</size></color>" : base.PressAnyKeyString;
	}

	//Setup the config switch
	internal static void Init(ConfigEntryT<bool> ExtractAllTextures) =>
		ExtractAllTextures.SettingChanged += (_, _) => {
			if(!ExtractAllTextures)
				return;
			ExtractAllTextures.V=false;
			_=Execute();
		};

	//Initialize the run if not already running
	private static ExtractAllTextures? Execute()
	{
		//Only allow 1 instance to run at once
		if(CurrentlyRunning) {
			Log.Error("Already running");
			return null;
		}
		CurrentlyRunning=true;
		return new ExtractAllTextures();
	}

	//Initialize the process
	private ExtractAllTextures()
	{
		//Make sure the directory exists
		try {
			if(!FileOps.DirectoryExists(DirName))
				_=FileOps.CreateDirectory(DirName);
		} catch(Exception e) {
			PM.Message=$"Directory creation failed: {e.Message}";
			Log.Error(PM.Message);
			Close();
			return;
		}

		//Initialize the process
		_=Catcher.ExecCoroutine("Extract textures", CoroutineFunc());
	}

	//Process textures in a coroutine
	private IEnumerator CoroutineFunc()
	{
		//Process all the textures
		Texture2D[] AllTextures=Resources.FindObjectsOfTypeAll<Texture2D>();
		int Cur=0, Total=AllTextures.Length;
		foreach(Texture2D CurTex in AllTextures) {
			PM.Message=$"Processing #{++Cur}/{Total}: {CurTex?.name}";
			yield return ProcessTextureWrapper(CurTex!);
		}

		//Mark as complete and inform the user of the success
		PM.Message=$"Finished processing {Total} textures\nWritten: {NumWritten}\nAlready Existed: {NumAlreadyExisted}\nFailed: {NumFailed}";
		Close();
	}

	//Watch for errors on a texture and add some delays for stability
	private IEnumerator ProcessTextureWrapper(Texture2D CurTex)
	{
		yield return ProcessTexture(CurTex);
		yield return new WaitForSeconds(0.01f); //Allow breathing
		if(LastError==null)
			yield break;
		NumFailed++;
		Catcher.OutputException($"Failed to write {CurName}", LastError);
		LastError=null;
	}

	//Process a texture
	private IEnumerator ProcessTexture(Texture2D CurTex)
	{
		//Check for null texture
		if(CurTex==null) {
			CurName="NULL TEXTURE";
			LastError=new ArgumentNullException(nameof(CurTex));
			yield break;
		}

		//Get the PNG bytes and filename
		string TexName=CurTex.name.Length==0 ? "NO-NAME" : CurTex.name;
		TexName=string.Join("_", TexName.Split(FileOps.InvalidNameChars()));
		CurName=$"{TexName}-{0:D32}.png";
		byte[] PNGBytes;
		string NewFileName;
		try {
			//Get the bytes
			using TypedDisposer<Texture2D> TempTex=new(
				CurTex.ToReadable(),
				Target => Target.TDestroy()
			);
			PNGBytes=TempTex.Target.EncodeToPNG();

			//Get the filename
			string FileMD5=BitConverter.ToString(PngMd5.ComputeHash(PNGBytes)).Replace("-", "").ToLowerInvariant();
			NewFileName=FileOps.PathCombine(DirName, CurName=$"{TexName}-{FileMD5}.png");
		} catch(Exception e) {
			LastError=e;
			yield break;
		}

		//If file already exists, nothing to do
		bool AlreadyExists=FileOps.FileExists(NewFileName);
		if(AlreadyExists) {
			NumAlreadyExisted++;
			Log.Info($"Already exists: {CurName}");
			yield break;
		}

		//Write the file
		Misc.Ref<Exception?> RE=new(null);
		yield return FileOps.WriteFileAsync(NewFileName, PNGBytes).AsCoroutine(RE);
		if(RE.Value!=null) {
			LastError=RE.Value;
			yield break;
		}
		Log.Info($"Wrote: {CurName}");
		NumWritten++;
	}

	//Clean up resources
	private void Close()
	{
		PngMd5.Dispose();
		CurrentlyRunning=false;
	}
}