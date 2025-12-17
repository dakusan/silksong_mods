<?php
require_once(__DIR__.'/Shared.php');

//Only output files if script is run directly. Otherwise, used as a library
if(str_ends_with($_SERVER['SCRIPT_NAME'], pathinfo(__FILE__, PATHINFO_BASENAME))) {
	file_put_contents('./categories.json', GenerateCategories());
	file_put_contents('./items.json', GenerateItems());
}

//Save json to a file
function GenerateJson($Data)
{
	$Data=json_encode($Data, JSON_PRETTY_PRINT|JSON_UNESCAPED_SLASHES|JSON_UNESCAPED_UNICODE);
	for($Count=1; $Count!=0; $Data=preg_replace('/^(\t*)    /m', "\\1\t", $Data, -1, $Count)); //Replace 4 space indents to tabs
	return preg_replace('/([^{[,])\n/', "\$1,\n", $Data); //Add trailing commas
}

//Create the category groups
function GenerateCategories()
{
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

//Fill in the items
function GenerateItems()
{
	global $Conn;
	$Items=[];

	$Required=['CategoryID', 'Title', 'x', 'y'];
	$Optional=['IconID', 'Reqs', 'Needs', 'Rewards', 'Effect', 'Tip', 'Notes', 'IgnPageName', 'Store', 'ImageURLs'];
	foreach($Conn->query('SELECT * FROM Items ORDER BY ID ASC') as $Row) {
		//Create item, only adding required and non-null optional members
		$Items[$Row['ID']]=$NewItem=(object)[];
		foreach($Required as $Key)
			$NewItem->{$Key}=$Row[$Key];
		foreach($Optional as $Key)
			if($Row[$Key]!==null)
				$NewItem->{$Key}=$Row[$Key];

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
?>