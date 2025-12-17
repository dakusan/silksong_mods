<?php
require_once(__DIR__.'/Shared.php');

const MAX_FIELD_LEN=100;
$GetEscapedVariable=fn($Name, $Pattern) =>
		!is_string($Val=($_REQUEST[$Name] ?? null)) ? ErrAndDie("Missing variable: $Name")
		: ( !mb_check_encoding($Val, 'UTF-8') ? ErrAndDie("Improperly encoded variable: $Name")
		: ( mb_strlen($Val)>MAX_FIELD_LEN ? ErrAndDie("Variable is too long: $Name")
		: ( !preg_match($Pattern, $Val) ? ErrAndDie("Variable is not in the proper format or is too long: $Name")
		:   $Escape($Val)
		)));

//Note on maximum lengths: PlayerData member=40, SceneName=32, Scene.ItemName=43, SteamName=3-32
try {
	$Conn->query(sprintf(
		"INSERT INTO SilkSongItems (ItemID, ItemName, Username) VALUES (%s, %s, %s)",
		$GetEscapedVariable('ItemID', '/^[1-9]\d{0,6}$/D'),
		$GetEscapedVariable('ItemName', '/^(?:PlayerData\.\w{1,45}|(?!PlayerData)[\w ]{1,40}\.[\w ()-]{1,45})$/D'),
		($UserName=$GetEscapedVariable('Username', '/^[^\x00-\x1F\s][^\x00-\x1F\r\n]{2,49}$/uD'))
			!="'*SILKDEV NO NAME*'" ? $UserName
			: $Escape('IP='.substr($_SERVER['HTTP_CF_CONNECTING_IP'] ?? $_SERVER['HTTP_X_REAL_IP'] ?? $_SERVER['REMOTE_ADDR'] ?? 'No IP address', 0, 47))
	));
} catch(Exception $e) {
	ErrAndDie('Insert query failed', $e->getMessage());
}

print 'SUCCESS';
?>