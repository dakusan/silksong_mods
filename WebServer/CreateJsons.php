<?php
require_once(__DIR__.'/Shared.php');

//Only output files if script is run directly. Otherwise, used as a library
if(str_ends_with($_SERVER['SCRIPT_NAME'], pathinfo(__FILE__, PATHINFO_BASENAME))) {
	file_put_contents('./categories.json', GenerateCategories());
	file_put_contents('./items.json', GenerateItems());
	file_put_contents('./Misc.json', GenerateMisc());
}

function GenerateJson($Data)
{
	$Data=json_encode($Data, JSON_PRETTY_PRINT|JSON_UNESCAPED_SLASHES|JSON_UNESCAPED_UNICODE);
	for($Count=1; $Count!=0; $Data=preg_replace('/^(\t*)    /m', "\\1\t", $Data, -1, $Count)); //Replace 4 space indents to tabs
	return preg_replace('/([^{[,])\n/', "\$1,\n", $Data); //Add trailing commas
}

function GenerateCategories()
{
	//Get the category groups from the Category’s table CategoryGroup enum
	global $Conn;
	if(!preg_match('/CategoryGroup.*enum\(\'(.*?)\'\)/', $Conn->query('SHOW CREATE TABLE Categories')->fetch_row()[1], $Matches))
		ErrAndDie('Could not find category groups');
	$CatGroups=array_fill_keys(explode("','", $Matches[1]), []);

	//Fill in the categories
	$CatOrder=['Order', 'IconID', 'Title', 'Info'];
	$Q=$Conn->query('SELECT ID, CategoryGroup, OrderNum, IconID, Title, Info FROM Categories ORDER BY ID ASC');
	while($Row=$Q->fetch_object()) {
		$NewItem=(object)[
			'Order'=>(int)$Row->OrderNum,
			'IconID'=>(int)$Row->IconID,
			'Title'=>$Row->Title,
		];
		if($Row->Info!==null)
			$NewItem->Info=$Row->Info;
		$CatGroups[$Row->CategoryGroup][$Row->ID]=$NewItem;
	}

	return GenerateJson($CatGroups);
}

function GenerateItems()
{
	global $Conn;
	$Items=[];

	//Get the static links
	$StaticLinks=[];
	foreach($Conn->query('SELECT ID, Name FROM StaticLinks') as $Row)
	{
		$Row=(object)$Row;
		$StaticLinks[$Row->ID]=$Row->Name;
	}

	//Put together the structured item links by set, group, and order
	$Links=[];
	foreach($Conn->query('SELECT * FROM ItemLinkDefs ORDER BY SetID ASC, GroupNum ASC, OrderNum ASC') as $Row) {
		$Row=(object)$Row;
		$Links[$Row->SetID][$Row->GroupNum][$Row->OrderNum]=$Row;
	}

	//Fill in the items
	$Required=['CategoryID', 'Title', 'x', 'y'];
	$Optional=['IconID', 'Reqs', 'Needs', 'Rewards', 'Effect', 'Tip', 'Notes', 'IgnPageName', 'Store', 'ImageURLs', "Description"];
	foreach($Conn->query('SELECT * FROM Items ORDER BY ID ASC') as $Row) {
		//Create item, only adding required and non-null optional members
		$Items[$Row['ID']]=$NewItem=(object)[];
		foreach($Required as $Key)
			$NewItem->{$Key}=$Row[$Key];
		foreach($Optional as $Key)
			if($Row[$Key]!==null)
				$NewItem->{$Key}=$Row[$Key];

		//Gather structured requirements, notes, and reward
		foreach(['Reqs', 'Needs', 'Rewards'] as $FieldName) {
			$NewVal='';
			if(isset($Row[$FieldName.'SetID']))
				$NewVal.=CompileSet($Links[$Row[$FieldName.'SetID']], $StaticLinks);
			if($FieldName=='Reqs' && isset($Row['WhereAt']))
				$NewVal.='@'.$Row['WhereAt'];

			if($NewVal!=='')
				$NewItem->$FieldName=$NewVal;
			else
				unset($NewItem->$FieldName);
		}

		//Format non-string fields
		$NewItem->CategoryID=(int)$NewItem->CategoryID;
		$NewItem->x=(double)$NewItem->x;
		$NewItem->y=(double)$NewItem->y;
		if(isset($NewItem->IconID))
			$NewItem->IconID=(int)$NewItem->IconID;
		if(isset($NewItem->Store))
			$NewItem->Store=($NewItem->Store[0]==='!' ? json_decode(substr($NewItem->Store, 1)) : [$NewItem->Store]);
		if(isset($NewItem->ImageURLs))
			$NewItem->ImageURLs=explode('!!!', $NewItem->ImageURLs);
	}

	return "//See Items.ebnf for requirements\n".GenerateJson($Items);
}

//Compile structured requirements, notes, and reward
function CompileSet($Set, $StaticLinks)
{
	$Groups=[];
	$ExtraEnd='';
	foreach($Set as $GroupIndex => $GroupList) {
		$Items=[];
		foreach($GroupList as $Item) {
			$ItemParts=[];
			if($Item->FlagStarted)	$ItemParts[]='~';
			if($Item->FlagNot)		$ItemParts[]='!';
			if($Item->FlagAmount!=1)$ItemParts[]='*'.$Item->FlagAmount;
			if($Item->FlagOptional)	$ItemParts[]='?';
			if($Item->StaticLinkID!==null)
				$ItemParts[]=$StaticLinks[$Item->StaticLinkID];
			else if($Item->ItemID!==null)
				$ItemParts[]="[$Item->Name|$Item->ItemID]";
			else if($GroupIndex!=255)
				$ItemParts[]=$Item->Name;
			else {
				$ExtraEnd='^'.$Item->Name;
				continue 2;
			}

			$Items[]=implode('', $ItemParts);
		}
		$Groups[$GroupIndex]=implode('+', $Items);
	}

	return implode('|', $Groups).$ExtraEnd;
}

function GenerateMisc()
{
	//Gather the StaticLinks and StaticLinkItems
	global $Conn;
	$StaticLinkNames=[];
	$StaticLinks=[];
	foreach($Conn->query('SELECT SL.*, SLI.ID AS SLIID, SLI.ItemID, SLI.CategoryID FROM StaticLinks AS SL LEFT JOIN StaticLinkItems AS SLI ON SLI.StaticLinkID=SL.ID ORDER BY SL.Name ASC, SLI.OrderNum') as $Row) {
		//Handle special rows
		$Row=(object)$Row;
		if((int)$Row->Special)
			if($Row->SLIID!==null)
				throw new Exception("StaticLink [#$Row->ID] special row cannot contain items");
			else
				continue;

		//Set the name if not already set
		$StaticLinkNames[$Row->ID] ??= $Row->Name;

		//Add the category or item to the row
		if($Row->SLIID===null)
			throw new Exception("StaticLink [#$Row->ID] must contain at least 1 item");
		else if($Row->ItemID!==null && $Row->CategoryID!==null)
			throw new Exception("StaticLink [#$Row->ID] cannot contain both an item and category");
		else if($Row->ItemID!==null)
			if(!is_array($StaticLinks[$Row->ID] ?? []))
				throw new Exception("StaticLink [#$Row->ID] cannot contain more than 1 item if it has a category");
			else
				$StaticLinks[$Row->ID][]=(int)$Row->ItemID;
		else if($Row->CategoryID===null)
			throw new Exception("StaticLinkItem [#$Row->SLIID] for Static Link [#$Row->ID] must contain either a category or item");
		else if(isset($StaticLinks[$Row->ID]))
			throw new Exception("StaticLink [#$Row->ID] cannot contain more than 1 item if it has a category");
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
?>