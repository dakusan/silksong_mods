<?php
require_once(__DIR__.'/Shared.php');
require_once(__DIR__.'/SimpleSQL.php');

$Langs=['en', 'ar', 'bn', 'de', 'es', 'fr', 'hi', 'id', 'it', 'ja', 'ko', 'mr', 'pt', 'ru', 'sw', 'ta', 'te', 'tr', 'ur', 'vi', 'zh'];
$PregReplaces=[
	'~^(\t".*":) \{$~m' => '$1{',
	'~(\n\t*[}\]])~m' => ',$1'
];

foreach([
	'PharloomAtlas' => '../PharloomAtlas/Assets/Translations/',
	'SilkDev' => '../SilkDev/Assets/Translations/',
	'PinFinder' => '../PinFinder/Assets/Translations/',
	'NoClip' => '../NoClip/Assets/Translations/',
	'VGAtlas' => '../VGAtlas/public/Assets/Translations/Atlas/Merge/',
	'VGAtlas_Utils' => '../VGAtlas/public/Assets/Translations/Default/',
] as $Module => $Dir)
	ProcessModule($Module, $Dir);

function ProcessModule($Module, $Dir): void
{
	//Gather the default data
	$Sections=QueryKVP('SELECT Section, Comment FROM TranslationSections WHERE Module=? ORDER BY `Order` ASC', $Module);
	$Translations=[];
	foreach(Query('SELECT Section, TKey, `Default`, PreComment, Comment, ID FROM TranslationKeys WHERE Module=? ORDER BY `Order` ASC', $Module) as $Data)
		$Translations[$Data->Section][$Data->TKey]=$Data;

	$TranslationIDs=[];
	$Output=['//NOTE: Do not include this in the release. It is only here to help generate other translation files ✓', '{'];
	foreach($Sections as $SectionName => $SectionComment) {
		$Output[]="\t\"$SectionName\":{".($SectionComment ? " //$SectionComment" : '');
		foreach($Translations[$SectionName] as $KeyName => $Data) {
			if($Data->PreComment)
				array_push($Output, ...
					!strpos($Data->PreComment, "\n")
						? ["\t\t//".$Data->PreComment]
						: ["\t\t/*", ...array_map(fn($Str) => "\t\t$Str", explode("\n", $Data->PreComment)), "\t\t*/"]
				);
				$Output[]="\t\t".JSEnc($KeyName).': '.JSEnc($Data->Default).','.($Data->Comment ? " //$Data->Comment" : '');
				$TranslationIDs[]=$Data->ID;
		}
		$Output[]="\t},";
	}
	$Output[]='}';
	file_put_contents($Dir.'default.tr.json', implode("\n", $Output));

	global $Langs;
	foreach($Langs as $Lang)
		ProcessLang($Lang, $Dir, $Module, $Sections, $Translations, $TranslationIDs);
}

function ProcessLang($Lang, $Dir, $Module, $Sections, $Translations, $TranslationIDs): void
{
	$LangTrans=QueryKVP('SELECT TranslationKeyID, Translation FROM Translations WHERE Language=? AND TranslationKeyID IN ('.implode(', ', $TranslationIDs).')', $Lang);
	if(!count($LangTrans)) {
		if($Lang!=='ko')
			if($Lang==='en')
				file_put_contents($Dir.$Lang.'.tr.json', "{\n\t\"PLACE_HOLDER\":{\n\t\t\"PLACE_HOLDER\": \"✓\",\n\t},\n}");
			else
				fwrite(STDERR, "Missing language data for Module/Lang $Module/$Lang\n");
		return;
	}

	$Output=[];
	foreach($Sections as $SectionName => $_) {
		$SectionTrans=[];
		foreach($Translations[$SectionName] as $KeyName => $Data)
			if(($Trans=$LangTrans[$Data->ID] ?? null)!==null)
				$SectionTrans[$KeyName]=$LangTrans[$Data->ID] ?? $Trans;
			else if($Lang!=='en')
				fwrite(STDERR, "Missing translation for Module/Lang/SectionName/Key $Module/$Lang/$SectionName/$KeyName\n");
		if(count($SectionTrans))
			$Output[$SectionName]=$SectionTrans;
	}

	global $PregReplaces;
	$Text=json_encode($Output, JSON_PRETTY_PRINT|JSON_UNESCAPED_UNICODE|JSON_UNESCAPED_SLASHES);
	$Text=preg_replace_callback('/^( {4})+/m', fn($m) => str_repeat("\t", strlen($m[0])/4), $Text);
	$Text=preg_replace(array_keys($PregReplaces), array_values($PregReplaces), $Text);
	$Text='//See default.tr.json for line and section descriptions'."\n$Text";
	file_put_contents($Dir.$Lang.'.tr.json', $Text);
}

function JSEnc($Str): string { return json_encode($Str, JSON_UNESCAPED_SLASHES|JSON_UNESCAPED_UNICODE); }