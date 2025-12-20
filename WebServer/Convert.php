<?php
require_once(__DIR__.'/Shared.php');
$NumErrors=0;
$StopOnError=true;
$PrintProgress=true;

function ErrDie($Str)
{
	global $NumErrors, $StopOnError;
	global $Conn;
	$NumErrors++;
	if(!$StopOnError) {
		print "$Str\n";
		return (object)['Where'=>null, 'SetID'=>null];
	}
	$Conn->query('ROLLBACK');
	ErrAndDie($Str);
}

$Conn->query('UPDATE Items SET ReqsSetID=NULL, NeedsSetID=NULL, RewardsSetID=NULL, StoreSetID=NULL, WhereAt=NULL');
$Conn->query('DELETE FROM ItemLinkDefs');
$Conn->query('ALTER TABLE ItemLinkDefs AUTO_INCREMENT=1');

$StaticLinks=[];
foreach($Conn->query('SELECT ID, Name FROM StaticLinks') as $Row)
{
	$Row=(object)$Row;
	$StaticLinks[$Row->Name]=$Row->ID;
}

$AllItemIDs=[];
foreach($Conn->query('SELECT ID FROM Items') as $Row)
	$AllItemIDs[$Row['ID']]=1;

$Conn->query('START TRANSACTION');

function RunQuery($Query, ...$Vars)
{
	global $Conn, $PrintProgress;
	$QuerySections=explode('?', $Query);
	$EndQuery=array_shift($QuerySections);
	try {
		if(count($QuerySections)!=count($Vars))
			throw new Exception('Invalid number of vars');
		foreach($QuerySections as $Index => $QueryPart)
			$EndQuery.=FormatQueryItem($Conn, $Vars[$Index]).$QueryPart;
		$Conn->query($EndQuery);
	} catch(Exception $e) {
		ErrAndDie("Query error:\n$Query\n$EndQuery\n$e\n".var_export($Vars, true));
	}
	if($PrintProgress)
		print "Ran Query [$Conn->insert_id]: $EndQuery\n";

	return $Conn->insert_id;
}
function FormatQueryItem($Conn, $Item)
{
	if($Item===null)
		return 'null';
	else if(is_int($Item))
		return (int)$Item;
	else if(is_bool($Item))
		return $Item ? 'true' : 'false';
	else
		return '"'.$Conn->escape_string($Item).'"';
}

function ProcessField($ID, $FieldName, $Field, $AllowWhere=false, $AllowExtra=true, $AllowMultiGroup=false, $AllowOptional=true)
{
	global $StaticLinks, $Conn, $AllItemIDs;
	$Ret=(object)['SetID'=>null, 'Where'=>null];
	$ID=(int)$ID;
	$Extra=null;
	$Err=fn($Str) => ErrDie("ERROR [$ID=$FieldName]: $Str\n$Field");

	if(!strlen($Field))
		return $Err('Blank field found');
	if(($At=strpos($Field, '@'))!==false) {
		if(!$AllowWhere)
			return $Err('Where not allowed');
		$Ret->Where=substr($Field, $At+1);
		$Field=substr($Field, 0, $At);
	}
	if(($At=strpos($Field, '^'))!==false) {
		if(!$AllowExtra)
			return $Err('Extra not allowed');
		$Extra=substr($Field, $At+1);
		$Field=substr($Field, 0, $At);
	}

	$SetID=($Conn->query('SELECT MAX(SetID) FROM ItemLinkDefs')->fetch_row()[0] ?? 0)+1;

	if($Extra)
		RunQuery('INSERT INTO ItemLinkDefs (Name, SetID, GroupNum, OrderNum) VALUES (?, ?, ?, ?)', $Extra, $Ret->SetID=$SetID, 255, 0);

	if(!strlen($Field)) {
		if(!$Extra && !$Ret->Where)
			return $Err('Everything is blank?');
		return $Ret;
	}

	$AllowedChars='[\w ’()\.\/,-]';
	$ItemMatchRegex='([~!?]|\*\d+)*(?:'.$AllowedChars.'+|\['.$AllowedChars.'+\|\d+\])(?:\+|\||$)';
	if(!preg_match('/^('.$ItemMatchRegex.')+$/', $Field))
		return $Err('Regex mismatch: '.$Field);

	//Split into group parts
	if(!preg_match_all("/$ItemMatchRegex/", $Field, $Matches))
		return $Err('Regex split error');
	$Matches=$Matches[0];

	$Err=fn($Str) => ErrDie("ERROR [$ID=$FieldName]: $Str\n$Field\n".var_export($Matches, true));

	if(implode('', $Matches)!==$Field)
		return $Err("Regex recompile failed: “{$Field}” != “".implode('', $Matches).'”');
	if(in_array(substr($Matches[count($Matches)-1], -1), ['|', '+']))
		return $Err('Ended on invalid character');

	//Move into groups
	$CurGroup=0;
	$Groups=[[]];
	$Matches[count($Matches)-1].='+'; //Set last symbol as plus for grouping
	$Ret->SetID=$SetID;
	foreach($Matches as $Match) {
		$Groups[$CurGroup][]=substr($Match, 0, -1);
		if(substr($Match, -1)=='|')
			$Groups[++$CurGroup]=[];
		else if(substr($Match, -1)!='+')
			return $Err('Somehow not a plus or pipe at end of: '.$Match);
	}

	foreach($Groups as $SetGroupNum => $GroupInfo) {
		if($SetGroupNum>0 && !$AllowMultiGroup)
			return $Err('MultiGroup not allowed');
		foreach($GroupInfo as $ItemNum => $ItemInfo) {
			$StartItemInfo=$ItemInfo;
			$FlagOptional=$FlagNot=$FlagStarted=false;
			$FlagAmount=1;
			while(strlen($ItemInfo)>0) {
				switch($ItemInfo[0])
				{
					case '~': $FlagStarted=true; break;
					case '!': $FlagNot=true; break;
					case '?': $FlagOptional=true; break;
					case '*':
						if(!preg_match('/^\d+/', substr($ItemInfo, 1), $Matches))
							return $Err("Invalid amount $StartItemInfo");
						$FlagAmount=(int)$Matches[0];
						$ItemInfo=substr($ItemInfo, strlen($Matches[0]));
						break;
					default:
						break 2;
				}
				$ItemInfo=substr($ItemInfo, 1);
			}

			$InsertFunc=fn($Fields, ...$Vals) => RunQuery(
				"INSERT INTO ItemLinkDefs (FlagAmount, FlagOptional, FlagNot, FlagStarted, SetID, GroupNum, OrderNum, $Fields) VALUES (".implode(', ', array_fill(0, 7+count($Vals), '?')).')',
				$FlagAmount, $FlagOptional, $FlagNot, $FlagStarted, $SetID, $SetGroupNum, $ItemNum, ...$Vals
			);

			if(isset($StaticLinks[$ItemInfo]))
				$InsertFunc('StaticLinkID', (int)$StaticLinks[$ItemInfo]);
			else if(strlen($ItemInfo)<1)
				return $Err("Invalid name (No value set): $StartItemInfo");
			else if($ItemInfo[0]!=='[')
				if($AllowOptional && $FlagOptional)
					$InsertFunc('Name', $ItemInfo);
				else
					return $Err("Invalid static name: $StartItemInfo");
			else if(!preg_match('/^\[('.$AllowedChars.'+)\|(\d+)\]$/', $ItemInfo, $Matches))
				return $Err("Invalid value: $StartItemInfo");
			else if(!isset($AllItemIDs[$Matches[2]]))
				return $Err("Invalid Item ID: $StartItemInfo");
			else
				$InsertFunc('ItemID, Name', $Matches[2], $Matches[1]);
		}
	}

	return $Ret;
}

foreach($Conn->query('SELECT ID, Reqs, Needs, Rewards FROM Items') as $Index => $Row) {
	$Row=(object)$Row;
	$Where=$ReqsSetID=$NeedsSetID=$RewardsSetID=$StoreSetID=null;
	if($Row->Reqs!==null) {
		$Info=ProcessField($Row->ID, 'Reqs', $Row->Reqs, true, true, true, false);
		$Where=$Info->Where ?? null;
		$ReqsSetID=$Info->SetID;
	}
	if($Row->Needs!==null)
		$NeedsSetID=ProcessField($Row->ID, 'Needs', $Row->Needs)->SetID;
	if($Row->Rewards!==null)
		$RewardsSetID=ProcessField($Row->ID, 'Rewards', $Row->Rewards)->SetID;
	RunQuery('UPDATE Items SET WhereAt=?, ReqsSetID=?, NeedsSetID=?, RewardsSetID=?, StoreSetID=? WHERE ID=?', $Where, $ReqsSetID, $NeedsSetID, $RewardsSetID, $StoreSetID, (int)$Row->ID);
	if($PrintProgress)
		print "Processed: $Index\n";
}

$Conn->query('COMMIT');

print 'Complete! Number of errors: '.$NumErrors;
?>