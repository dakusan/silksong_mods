<? /** @noinspection PhpShortOpenTagInspection, CssUnusedSymbol */
$Projects=(array)json_decode(
	preg_replace(['/,(\s+[}\]])/', '~^//.*$~m'], ['$1', ''], file_get_contents('Projects.json'))
);

//Pull the rendered HTML for the projects and create the project name slugs
function CreateSlug($Name)
{
	$Name=trim($Name);
	if(class_exists('Normalizer'))
		/** @noinspection PhpParamsInspection */
		$Name=Normalizer::normalize($Name, Normalizer::FORM_KD);
	$Name=preg_replace('/[^A-Za-z0-9]+/', '_', $Name); //Replace non-alphanumeric characters with underscore
	$Name=preg_replace('/_+/', '_', $Name); //Collapse multiple underscores
	return trim($Name, '_'); //Trim leading/trailing underscores
}

foreach($Projects as $ProjectName => $PData) {
	$PData->HTML=file_get_contents(__DIR__."/Assets/$ProjectName.AboutMe.html");
	$PData->Slug=CreateSlug($ProjectName);
}
?>
<!DOCTYPE html>
<html xmlns="http://www.w3.org/1999/xhtml" lang="en">
<head>
	<title>Silksong Mods by Dakusan</title>
	<meta charset="UTF-8">
	<meta name="viewport" content="width=device-width, initial-scale=1">
	<style>
<?
//Output the stylesheet from Pharloom Atlas
preg_match_all('/<style\b[^>]*>(.*?)<\/style>/is', $Projects['PharloomAtlas']->HTML, $Matches);
foreach($Matches[1] as $Match)
	print $Match;
?>
</style>
<link rel=stylesheet href="./index.css" />

</head><body>
<div role="tablist" class="Tabs TopBoxes" aria-label="Projects" id=RootTabs>
<? foreach($Projects as $ProjectName => $PData) { ?>
	<button role=tab class=Tab id=tab-<?=$PData->Slug?> data-title="<?=htmlentities($PData->ShortName)?>" aria-controls=panel-<?=$PData->Slug?>>
		<div class=TabLogo><img src="https://static.castledragmire.com/silksong/<?=$ProjectName?>LogoSmall.jpg" alt="<?=$PData->Name?> Logo" /></div>
		<div class="TabText"><?=str_replace("\n", '<br>', $PData->Name)?></div>
	</button>
<? } ?>
</div>
<div class="Links" aria-label="Links Sections">
<? foreach($Projects as $ProjectName => $PData) { ?>
	<div class='Boxes TopBoxes' id=panel-<?=$PData->Slug?> aria-labelledby=tab-<?=$PData->Slug?>>
		<a class=Tab href="https://www.nexusmods.com/hollowknightsilksong/mods/<?=$PData->NexusID?>">Nexus Mods: <?=$PData->ShortName?></a>
		<button role=tab class=Tab id=tab-<?=$PData->Slug?>-Description aria-controls=panel-<?=$PData->Slug?>-Description>Description</button>
<? if(isset($PData->Pictures)) { ?>
		<button role=tab class=Tab id=tab-<?=$PData->Slug?>-Pictures aria-controls=panel-<?=$PData->Slug?>-Pictures>Pictures</button>
<? } ?>
		<a class="Tab Disabled"><span>Coming<br>soon</span>Github</a>
		<a class="Tab Disabled"><span>Coming<br>soon</span>Download</a>
	</div>
<? } ?>
</div>
<div id=ContentsFrame><div id=Contents>
<? $PrintAfter='</style></details></div>'; foreach($Projects as $ProjectName => $PData) { ?>
	<div role=tabpanel class=TabContents id=panel-<?=$PData->Slug?>-Description aria-labelledby=tab-<?=$PData->Slug?>-Description>
		<?=substr($PData->HTML, strpos($PData->HTML, $PrintAfter)+strlen($PrintAfter))?>
	</div>
	<? if(isset($PData->Pictures)) { ?>
	<div role=tabpanel class="TabContents Pictures" id=panel-<?=$PData->Slug?>-Pictures aria-labelledby=tab-<?=$PData->Slug?>-Pictures>
		<img class=BackgroundImage src="https://static.castledragmire.com/silksong/<?=$ProjectName?>/Background-Small.jpg" loading="lazy" decoding="async" alt="<?=$PData->ShortName?> Background">
		<div class=LogoGrid>
			<? foreach($PData->Pictures as $FileName => $Description) { ?>
			<div class=LogoCard>
				<img src="https://static.castledragmire.com/silksong/<?=$ProjectName?>/<?=$FileName?>" loading="lazy" decoding="async" alt="<?=htmlentities($Description)?>">
				<span><?=htmlentities($Description)?></span>
			</div>
			<? } ?>
		</div>
	</div>
	<? } ?>
<? } ?>
</div></div>
</body>
<script type="module">
import Tab from "./Tabs.js"
class TabOverride extends Tab
{
	constructor(Parent, Slug, Title, $Tab, $Contents) {
		super(Parent, Slug, Title, $Tab, $Contents);
	}
	Select(UpdateHashAndTitle=true, AutoSelectFirstChild=true)
	{
		super.Select(UpdateHashAndTitle, AutoSelectFirstChild);
		document.getElementById('ContentsFrame').classList.toggle("FullBleed", this.Title==="Pictures");
	}
}
Tab.InitRoot(/** @type {Tab} */ new TabOverride(null, null, document.title, null, null));
</script>
</html>