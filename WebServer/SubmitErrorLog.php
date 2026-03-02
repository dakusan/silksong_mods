<?php
require_once(__DIR__.'/Shared.php');
require_once(__DIR__.'/SimpleSQL.php');

$MaxPayload=8*1024*1024;
$ErrorLog=GetVariable('ErrorLog', '/./', $MaxPayload);
if(strlen($ErrorLog)>$MaxPayload)
	ErrAndDie("Variable [bytes] is too long: MaxPayload", null, 400);
$SteamUsername=GetSteamUsername();

try {
	global $Conn;
	$Stmt=$Conn->prepare('INSERT INTO ErrorLogs (Username, ErrorLog) VALUES (?, ?)');
	$Stmt->bind_param('ss', $SteamUsername, $ErrorLog);
	$Stmt->execute();
	$Stmt->close();
} catch(Exception $e) {
	ErrAndDie('Insert query failed', $e->getMessage());
}

print 'SUCCESS';
?>