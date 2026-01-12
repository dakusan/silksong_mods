<?php
require_once(__DIR__.'/Shared.php');

//Check for request variables
$CompactJSON=false;
$Vars=$Vars ?? $_GET;
if(isset($argv) && count($argv)>1)
	parse_str($argv[1], $Vars);
if(isset($Vars['CompactJSON']))
	$CompactJSON=($Vars['CompactJSON']==1);
if(isset($Vars['Build'])) {
	if(!in_array($Vars['Build'], ['Categories', 'Items', 'Misc', 'ItemFinder']))
		return ErrAndDie('Invalid Build parameter', null, 400);
	header('Content-Type: application/json');
	try {
		print ('Generate'.$Vars['Build'])();
	} catch(Exception $e) {
		return ErrAndDie('An exception occurred', $e->getMessage());
	}
	exit(0);
}

//If not running the script from the command line, throw an error
if(!isset($argv) || !str_ends_with($_SERVER['SCRIPT_NAME'], pathinfo(__FILE__, PATHINFO_BASENAME)))
	return ErrAndDie('Missing Build parameter', null, 400);

//Output all files to local folder (which are probably symlinked elsewhere)
file_put_contents('./categories.json', GenerateCategories());
file_put_contents('./items.json', GenerateItems());
file_put_contents('./Misc.json', GenerateMisc());
file_put_contents('./ItemFinder.json', GenerateItemFinder());
print "Generated all files\n";
exit(0);

function GenerateJson($Data)
{
	global $CompactJSON;
	if($CompactJSON)
		return json_encode($Data, JSON_UNESCAPED_SLASHES|JSON_UNESCAPED_UNICODE);

	$Data=json_encode($Data, JSON_PRETTY_PRINT|JSON_UNESCAPED_SLASHES|JSON_UNESCAPED_UNICODE);
	for($Count=1; $Count!=0; $Data=preg_replace('/^(\t*)    /m', "\\1\t", $Data, -1, $Count)); //Replace 4 space indents to tabs
	return preg_replace('/([^{[,])\n/', "\$1,\n", $Data); //Add trailing commas
}

function GenerateCategories()
{
	//Get the category groups from the Category’s table CategoryGroup enum
	if(!preg_match('/CategoryGroup.*enum\(\'(.*?)\'\)/', Query('SHOW CREATE TABLE Categories')->current()->{'Create Table'}, $Matches))
		ErrAndDie('Could not find category groups');
	$CatGroups=array_fill_keys(explode("','", $Matches[1]), []);

	//Fill in the categories
	$CatOrder=['Order', 'IconID', 'Title'];
	foreach(Query('SELECT ID, CategoryGroup, OrderNum, IconID, Title FROM Categories ORDER BY ID ASC') as $Row) {
		$NewItem=(object)[
			'Order'=>(int)$Row->OrderNum,
			'IconID'=>(int)$Row->IconID,
			'Title'=>$Row->Title,
		];
		$CatGroups[$Row->CategoryGroup][$Row->ID]=$NewItem;
	}

	return GenerateJson($CatGroups);
}

function GenerateItems()
{
	//Get the static links
	$StaticLinks=[];
	foreach(Query('SELECT ID, Name FROM StaticLinks') as $Row)
		$StaticLinks[$Row->ID]=$Row->Name;

	//Put together the structured item links by set, group, and order
	$Links=[];
	foreach(Query('SELECT * FROM ItemLinkDefs ORDER BY SetID ASC, GroupNum ASC, OrderNum ASC') as $Row)
		$Links[$Row->SetID][$Row->GroupNum][$Row->OrderNum]=$Row;

	//Put together the structured stores
	$Stores=[];
	foreach(Query('SELECT VendorItemID, ReqsSetID AS Reqs, NeedsSetID AS Needs, RewardsSetID AS Rewards FROM Stores ORDER BY VendorItemID ASC, OrderNum ASC') as $Row) {
		$Stores[$Row->VendorItemID][]=$Row;
		unset($Row->VendorItemID);
	}
	$StoreSetIDs=array_values(array_filter(
		array_merge(...array_map(fn($S) => [$S->Reqs, $S->Needs, $S->Rewards], array_merge(...$Stores))),
		fn($ID) => $ID!==null
	));
	$StoreSets=[];
	foreach(Query('SELECT FlagAmount, FlagUnlinked, FlagNot, FlagStarted, "0" AS FlagRecommend, ItemID, StaticLinkID, Name, SetID, GroupNum FROM ItemLinkDefs WHERE SetID IN ('.implode(', ', $StoreSetIDs).') ORDER BY SetID ASC, GroupNum ASC, OrderNum ASC') as $Row)
		$StoreSets[$Row->SetID][]=$Row;
	if(count($StoreSets)!=count($StoreSetIDs))
		ErrAndDie('count($StoreSets)!=count($StoreSetIDs) ['.count($StoreSets).'!='.count($StoreSetIDs).']');
	unset($StoreSetIDs);
	$UnsetAndReturn=function(&$Arr, $ID) { $Val=$Arr[$ID]; unset($Arr[$ID]); return $Val; };
	foreach($Stores as $VendorID => $StoreItems)
		foreach($StoreItems as $StoreItem)
			foreach(['Reqs', 'Needs', 'Rewards'] as $FieldName)
				if(($SetID=$StoreItem->$FieldName)===null)
					unset($StoreItem->$FieldName);
				else
					try {
						$Set=[$UnsetAndReturn($StoreSets, $SetID)];
						if(end($Set[0])->GroupNum==255)
							$Set[255]=[array_pop($Set[0])];
						$StoreItem->$FieldName=CompileSet($Set, $StaticLinks, $FieldName);
					} catch(Exception $e) {
						ErrAndDie("Error compiling set #$SetID. Store#$VendorID.$FieldName ".$e->getMessage());
					}
	if(count($StoreSets))
		ErrAndDie('Store sets not used: '.count($StoreSets));
	unset($StoreSets, $UnsetAndReturn);

	//Get other table data to be integrated below
	$ImageURLs=[];
	foreach(Query('SELECT ItemID, URL FROM ImageURLs ORDER BY ItemID ASC, OrderNum ASC') as $Row)
		$ImageURLs[$Row->ItemID][]=$Row->URL;

	//Fill in the items
	global $CompactJSON;
	$Items=[];
	$Required=['C'=>'CategoryID', 'T'=>'Title', 'x'=>'x', 'y'=>'y'];
	$Optional=['I'=>'IconID', 'R'=>'!Reqs', 'A'=>'WhereAt', 'N'=>'!Needs', 'W'=>'!Rewards', 'E'=>'Effect', 'P'=>'Tip', 'O'=>'Notes', 'IGN'=>'IgnPageName', 'S'=>'!Store', 'U'=>'!ImageURLs'];
	$Combined=[...array_flip($Required), ...array_flip(array_map(fn($Str) => substr($Str, $Str[0]==='!' ? 1 : 0), $Optional))];
	foreach(Query('SELECT * FROM Items ORDER BY ID ASC') as $Row) {
		//Create item, only adding required and non-null optional members
		$Items[$Row->ID]=$NewItem=(object)[];
		$RowArr=(array)$Row;
		foreach($Required as $Key)
			$NewItem->{$Key}=$RowArr[$Key];
		foreach($Optional as $Key)
			if($Key[0]==='!')
				$NewItem->{substr($Key, 1)}=null;
			else if($RowArr[$Key]!==null)
				$NewItem->{$Key}=$RowArr[$Key];

		//Gather structured requirements, notes, and reward
		foreach(['Reqs', 'Needs', 'Rewards'] as $FieldName) {
			if(($SetID=$RowArr[$FieldName.'SetID'] ?? null)!==null)
				try {
					$NewItem->$FieldName=CompileSet($Links[$SetID], $StaticLinks, $FieldName);
				} catch(Exception $e) {
					ErrAndDie("Error compiling set #$SetID. $Row->ID.$FieldName ".$e->getMessage());
				}
			if($NewItem->$FieldName===null)
				unset($NewItem->$FieldName);
		}

		//Format non-string fields
		$NewItem->CategoryID=(int)$NewItem->CategoryID;
		$NewItem->x=GetDouble($NewItem->x);
		$NewItem->y=GetDouble($NewItem->y);
		if(isset($NewItem->IconID))
			$NewItem->IconID=(int)$NewItem->IconID;
		if(isset($Stores[$Row->ID]))
			$NewItem->Store=$Stores[$Row->ID];
		else
			unset($NewItem->Store);
		if(isset($ImageURLs[$Row->ID]))
			$NewItem->ImageURLs=$ImageURLs[$Row->ID];
		else
			unset($NewItem->ImageURLs);

		//Compact JSON
		if(!$CompactJSON)
			continue;
		$Items[$Row->ID]=$SmallItem=(object)[];
		foreach($NewItem as $Key => $Val)
			$SmallItem->{$Combined[$Key]}=$Val;
	}

	return ($CompactJSON ? '//Unminified JSON file at https://www.castledragmire.com/silksong/items.json' : '//See Items.ebnf for requirements')."\n"
		.GenerateJson($Items);
}

//Compile structured requirements, notes, and reward
function CompileSet($Set, $StaticLinks, $FieldName)
{
	//Add and check flags
	$AddFlag=function($Flag) use (&$ItemFlags, $FieldName) {
		$ItemFlags[]=$Flag;
		if(in_array($FieldName, ['Needs', 'Rewards']) && $Flag=='@')
			throw new Exception('cannot have flag: '.$Flag);
		if($FieldName=='Rewards' && in_array($Flag, ['!', '~']))
			throw new Exception('cannot have flag: '.$Flag);
	};

	//Compile items into groups
	$Groups=[];
	$ExtraEnd='';
	foreach($Set as $GroupIndex => $GroupList) {
		$Items=[];
		foreach($GroupList as $Item) {
			//Get flags
			$ItemFlags=$ItemVals=[];
			if($Item->FlagAmount!=1)$AddFlag('*'.$Item->FlagAmount);
			if($Item->FlagStarted)	$AddFlag('~');
			if($Item->FlagNot)		$AddFlag('!');
			if($Item->FlagRecommend)$AddFlag('@');
			if($Item->FlagUnlinked) $AddFlag('?');
			if($Item->FlagUnlinked)
				if($Item->Name===null)
					throw new Exception('name is required when unlinked flag is set');
				else if($GroupIndex==255)
					throw new Exception('cannot use GroupID=255 with unlinked flag');

			//Get the value
			if($Item->StaticLinkID!==null) {
				$ItemVals[]=$Item->StaticLinkID;
				$Name=$StaticLinks[$Item->StaticLinkID];
				if(in_array($Name, ['North', 'South', 'East', 'West']) && in_array($FieldName, ['Needs', 'Rewards']))
					throw new Exception('cannot use directions');
			}
			if($Item->ItemID!==null)
				$ItemVals[]=$Item->ItemID.'';
			if($Item->Name!==null) {
				$ItemVals[]=$Item->Name;
				if(trim($Item->Name)==='')
					throw new Exception('name cannot be blank');
				else if($GroupIndex==255)
					if($ExtraEnd!='')
						throw new Exception('cannot have multiple extra strings');
					else if(count($ItemFlags))
						throw new Exception('cannot have flags on extra strings');
					else
						$ExtraEnd='^'.$Item->Name;
				else if(!$Item->FlagUnlinked)
					throw new Exception('name can only be used with unlinked flag or GroupID=255');
				else if(strpbrk($Item->Name, '`|^')!==false)
					throw new Exception('name cannot use characters: ` | ^');
			}
			if(count($ItemVals)!==1)
				throw new Exception('static links, items, and names are mutually exclusive and exactly one must be set');

			//If the extra text field then nothing to do
			if($GroupIndex==255)
				if($Item->Name===null)
					throw new Exception('GroupNum=255 must only use the name field');
				else
					continue;

				//Make sure first character of item will not interfeer with the flags
				$ItemFlags=implode('', $ItemFlags);
				$ItemFlagChar=ord($ItemFlags[-1] ?? chr(0));
				$ItemValChar=ord($ItemVals[0]);
				$ExtraChar=
					   $ItemFlagChar>=ord('0') && $ItemFlagChar<=ord('9')
					&& $ItemValChar >=ord('0') && $ItemValChar <=ord('9')
					? '*' : '';
				$Items[]=$ItemFlags.$ExtraChar.$ItemVals[0];
		}
		if($GroupIndex!=255)
			$Groups[$GroupIndex]=implode('`', $Items);
	}

	if(count($Groups)>1 &&  in_array($FieldName, ['Needs', 'Rewards']))
		throw new Exception('cannot have multiple set groups');

	return implode('|', $Groups).$ExtraEnd;
}

//Compact a double from a string
function GetDouble($Str)
{
	global $CompactJSON;
	return (double)(!$CompactJSON ? $Str : preg_replace('/(\.\d{5})\d+/', '\1', $Str));
}

function GenerateMisc()
{
	//Gather the StaticLinks and StaticLinkItems
	$StaticLinkNames=[];
	$StaticLinks=[];
	foreach(Query('SELECT SL.*, SLI.ID AS SLIID, SLI.ItemID, SLI.CategoryID FROM StaticLinks AS SL LEFT JOIN StaticLinkItems AS SLI ON SLI.StaticLinkID=SL.ID ORDER BY SL.Name ASC, SLI.OrderNum') as $Row) {
		//Handle special rows
		if($Row->Special!==null) {
			if($Row->SLIID!==null)
				ErrAndDie("StaticLink [#$Row->ID] special row cannot contain items");
			$StaticLinks[$Row->ID]=[$Row->Special];
			$StaticLinkNames[$Row->ID]=$Row->Name;
			continue;
		}

		//Set the name if not already set
		$StaticLinkNames[$Row->ID] ??= $Row->Name;

		//Add the category or item to the row
		if($Row->SLIID===null)
			ErrAndDie("StaticLink [#$Row->ID] must contain at least 1 item");
		else if($Row->ItemID!==null && $Row->CategoryID!==null)
			ErrAndDie("StaticLink [#$Row->ID] cannot contain both an item and category");
		else if($Row->ItemID!==null)
			if(!is_array($StaticLinks[$Row->ID] ?? []))
				ErrAndDie("StaticLink [#$Row->ID] cannot contain more than 1 item if it has a category");
			else
				$StaticLinks[$Row->ID][]=(int)$Row->ItemID;
		else if($Row->CategoryID===null)
			ErrAndDie("StaticLinkItem [#$Row->SLIID] for Static Link [#$Row->ID] must contain either a category or item");
		else if(isset($StaticLinks[$Row->ID]))
			ErrAndDie("StaticLink [#$Row->ID] cannot contain more than 1 item if it has a category");
		else
			$StaticLinks[$Row->ID]=(int)$Row->CategoryID;
	}

	//Combine into output object
	$StaticLinksFinal=[];
	foreach($StaticLinkNames as $SLID => $SLName)
		$StaticLinksFinal[$SLID]=[$SLName, ...(is_array($StaticLinks[$SLID]) ? $StaticLinks[$SLID] : [$StaticLinks[$SLID]])];

	//Create JSON
	$Out=GenerateJson(['StaticLinks'=>$StaticLinksFinal]);
	$Replacements=[
		'/,(\s+\])/'=> '\1', //Remove trailing commas on item list
		'/\n\t\t\t/'=> ' ' , //Combine item lists into 1 row
		'/\n\t\t]/' => ']' , //Move item list end bracket onto item row
		'/\[ "/'	=> '["', //Remove extra space at beginning of item lists
		'/(\n\t\"StaticLinks)/' => "\n\t//Lists coorespond to IDs from items.json. Single numbers are from categories.json\\1",
	];
	$Out=preg_replace(array_keys($Replacements), array_values($Replacements), $Out);
	return $Out;
}

//Generate Matched Icons
function GenerateItemFinder()
{
	$IgnorePlayerNamedValues=$MatchedIcons=[];
	foreach(Query('SELECT Name FROM IgnorePlayerNamedValues ID ORDER BY Name') as $Row)
		$IgnorePlayerNamedValues[]=$Row->Name;
	foreach(Query('SELECT ItemID, ForStarting, Parent, ValueName FROM MatchedIcons ORDER BY ItemID ASC, ForStarting ASC') as $Row)
		$MatchedIcons[$Row->ItemID.($Row->ForStarting==1 ? '~' : '')]="$Row->Parent.$Row->ValueName";
	return GenerateJson(compact('IgnorePlayerNamedValues', 'MatchedIcons'));
}
?>