<?php
use JetBrains\PhpStorm\NoReturn;
#[NoReturn]
function ErrAndDie(string $User, ?string $Log=null, int $ResponseCode=500): int
{
	global $argv;
	file_put_contents(__DIR__.'/errors.log', date('Y-m-d H:i:s ').($Log!=null ? "$User: $Log" : $User)."\n", FILE_APPEND|LOCK_EX);
	http_response_code($ResponseCode);
	if(isset($argv))
		fwrite(STDERR, "$User\n");
	else
		print $User;
	exit(1);
}

function GetVariable(string $Name, string $Pattern, int $MaxFieldLen=100): string
{
	return
		 	!is_string($Val=($_REQUEST[$Name] ?? null))	? ErrAndDie("Missing variable: $Name", null, 400)
		: (	!mb_check_encoding($Val, 'UTF-8')			? ErrAndDie("Improperly encoded variable: $Name", null, 400)
		: ( preg_match('/[\x{0080}-\x{009F}]/u', $Val)  ? ErrAndDie("Variable has an invalid control character: $Name", null, 400)
		: (	mb_strlen($Val)>$MaxFieldLen				? ErrAndDie("Variable is too long: $Name", null, 400)
		: (	!preg_match($Pattern, $Val)					? ErrAndDie("Variable is not in the proper format or is too long: $Name", null, 400)
		:	$Val
		))));
}

function GetSteamUsername(string $FieldName='Username'): string //SteamName=3-32 characters
{
	$Username=GetVariable($FieldName, '/^[^\x00-\x1F\s][^\x00-\x1F]{2,49}$/uD');
	return
		   $Username!='*SILKDEV NO NAME*' ? $Username
		: 'IP='.substr($_SERVER['HTTP_CF_CONNECTING_IP'] ?? $_SERVER['HTTP_X_REAL_IP'] ?? $_SERVER['REMOTE_ADDR'] ?? 'No IP address', 0, 47);
}

//Writes text to a generated file only when cached metadata (file hash+mtime+size) or current file state indicates it is missing or different.
//Checks cheap metadata first, only hashing the existing file if the only change is its mtime, and saves the metadata cache at shutdown only when updated.
function UpdateFile($FileName, $Text): void
{
	static $FilesData=null;
	static $HasDataFileUpdated=false;
	if(!isset($FilesData)) {
		$DataFileName=__DIR__.'/FilesData.json';
		$FilesData=file_exists($DataFileName) ? (array)json_decode(file_get_contents($DataFileName), false) : [];
		register_shutdown_function(function() use (&$FilesData, &$HasDataFileUpdated, $DataFileName) {
			if($HasDataFileUpdated)
				file_put_contents($DataFileName, json_encode($FilesData, JSON_UNESCAPED_SLASHES|JSON_UNESCAPED_UNICODE|JSON_PRETTY_PRINT));
		});
	}

	$NewSha1=sha1($Text);
	$FD=$FilesData[$FileName] ?? null;
	$FailedBeforeFinalStat=true;
	$Stat=null;
	if(
		   !isset($FD, $FD->Hash, $FD->Size, $FD->MTime)
		|| $FD->Hash!==$NewSha1
		|| !file_exists($FileName)
		|| ($Stat=stat($FileName))['size']!==$FD->Size
		|| ($Stat['mtime']!==$FD->MTime && !($FailedBeforeFinalStat=false))
	) {
		if($FailedBeforeFinalStat || sha1_file($FileName)!==$NewSha1) {
			file_put_contents($FileName, $Text);
			clearstatcache(true, $FileName);
			$Stat=stat($FileName);
		}
		$FilesData[$FileName]=(object)['Hash'=>$NewSha1, 'Size'=>strlen($Text), 'MTime'=>$Stat['mtime']];
		$HasDataFileUpdated=true;
	}
}
?>