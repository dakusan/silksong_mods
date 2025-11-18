using SilkDev.Windows;
using System;
using System.Collections;
using System.Security.Cryptography;
using UnityEngine;

namespace SilkDev.Textures;

//Extract all textures. Called via the config
internal class ExtractAllTextures : ProgressBarWithLogs
{
	//Static members
	internal const string TextureDirectory="Textures";
	private static bool CurrentlyRunning;

	//Instance members
	private readonly MD5 PngMd5=MD5.Create();
	private readonly string DirName=FileOps.PathCombine(Misc.GetPluginPath, TextureDirectory);
	private int NumWritten=0, NumFailed=0, NumAlreadyExisted=0;
	private string CurName="?";
	private Exception? LastError;

	//Setup the config switch
	internal static void Init(Configs.ConfigEntryT<bool> ExtractAllTextures) =>
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
			PopupMessage PM=new($"Directory creation failed: {e.Message}");
			Log.Error(PM.Message);
			CurrentlyRunning=false;
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
			if(IsClosed)
				break;

			PercentAmount=++Cur/(float)Total;
			PercentText=$"{Cur}/{Total} [{PercentAmount*100:0}%]";
			MessageText=$"Processing {CurTex?.name ?? "NULL TEXTURE"}";
			yield return ProcessTextureWrapper(CurTex!);
		}

		//Mark as complete and inform the user of the success
		string SuccessMessage=(IsClosed ? $"Processing cancelled {Cur}/{Total}" : $"Finished processing {Total}")+" textures";
		string NumbersOutput=$"Written: {NumWritten}, Existed: {NumAlreadyExisted}, Failed: {NumFailed}";
		Log.Info($"{SuccessMessage}: {NumbersOutput}");
		if(IsClosed)
			_=new PopupMessage($"<b>{SuccessMessage}</b>\n{NumbersOutput}");
		else
			MessageText=NumbersOutput;
		CurrentlyRunning=false;
	}

	protected override void OnUpdate()
	{
		if(!CurrentlyRunning && DevInput.Util.AnyKeyOrButtonPressed)
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
		string ErrorStr=$"Failed to write {CurName}";
		Catcher.OutputException(ErrorStr, LastError);
		AddErrorLine(ErrorStr);
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
		TexName=FileOps.FixFileName(TexName);
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

		//Add log line
		void AddLogLine(string Message)
		{
			Log.Info(Message);
			AddLogLines([Message]);
		}

		//If file already exists, nothing to do
		bool AlreadyExists=FileOps.FileExists(NewFileName);
		if(AlreadyExists) {
			NumAlreadyExisted++;
			AddLogLine($"Already exists: {CurName}");
			yield break;
		}

		//Write the file
		Misc.Ref<Exception?> RE=new(null);
		yield return FileOps.WriteFileAsync(NewFileName, PNGBytes).AsCoroutine(RE);
		if(RE.Value!=null) {
			LastError=RE.Value;
			yield break;
		}
		AddLogLine($"Wrote: {CurName}");
		NumWritten++;
	}

	//Clean up resources
	public override void Close()
	{
		PngMd5.Dispose();
		base.Close();
	}
}