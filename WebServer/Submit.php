<?php
require_once(__DIR__.'/Shared.php');
require_once(__DIR__.'/SimpleSQL.php');

//Note on maximum lengths: PlayerData member=40, SceneName=32, Scene.ItemName=43, SteamName=3-32
try {
	Query(
		'INSERT INTO SilkSongItems (ItemID, ItemName, Username) VALUES (?, ?, ?)',
		(int)GetVariable('ItemID'  , '/^[1-9]\d{0,6}$/D'),
		GetVariable('ItemName', '/^(?:PlayerData\.\w{1,45}|(?!PlayerData)[\w ]{1,40}\.[\w ()-]{1,45})$/D'),
		GetSteamUsername(),
	);
} catch(Exception $e) {
	ErrAndDie('Insert query failed', $e->getMessage());
}

print 'SUCCESS';
?>