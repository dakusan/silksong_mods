<? /** @noinspection PhpShortOpenTagInspection */
$Projects=(array)json_decode(
	preg_replace(['/,(\s+[}\]])/', '~^//.*$~m'], ['$1', ''], file_get_contents('./IndexAssets/HTMLRenders/Projects.json'))
);

function ClearWS(): string { ob_start(); return ''; }
function EndClearWS(): string { return preg_replace('/\s+/', ' ', ob_get_clean()); }

//Pull the rendered HTML for the projects and create the project name slugs
function CreateSlug($Name): string
{
	$Name=trim($Name);
	if(class_exists('Normalizer'))
		$Name=Normalizer::normalize($Name, Normalizer::FORM_KD);
	$Name=preg_replace('/[^A-Za-z0-9]+/', '_', $Name); //Replace non-alphanumeric characters with underscore
	$Name=preg_replace('/_+/', '_', $Name); //Collapse multiple underscores
	return trim($Name, '_'); //Trim leading/trailing underscores
}

function ProcessHTMLFile($HTML, $SectionID=null): string
{
	$HTMLSections=explode('</style></details></div>', $HTML, 2); //Split around end of header section
	$Ret=preg_replace('/<img(\s+)/i', '<img loading=lazy decoding=async$1', $HTMLSections[1]); //Add lazy loading onto all images

	//Include custom styles
	if($SectionID!==null && count($CustomStylesParts=explode('/* Custom Styles */', $HTMLSections[0], 2))>1)
		$Ret='<style>'.preg_replace_callback(
			'/^([^{}]+)\{/m',
			fn($Matches) => implode(', ', array_map(
				fn($Str) => "#$SectionID ".trim($Str),
				preg_split('/, */', $Matches[1])
			)).' {',
			trim($CustomStylesParts[1])
		)."</style>\n$Ret";

	return $Ret;
}

foreach($Projects as $ProjectName => $PData) {
	$PData->HTML=file_get_contents("./IndexAssets/HTMLRenders/$ProjectName.AboutMe.html");
	$PData->Slug=CreateSlug($ProjectName);
}
?>
<!DOCTYPE html>
<html xmlns="http://www.w3.org/1999/xhtml" lang="en">
<head>
	<title>Silksong Mods by Dakusan</title>
	<meta charset="UTF-8">
	<meta name="viewport" content="width=device-width, initial-scale=1">
<?
//Output the stylesheet from Pharloom Atlas
preg_match_all('/<style\b[^>]*>.*?<\/style>/is', $Projects['PharloomAtlas']->HTML, $Matches);
foreach($Matches[0] as $Match)
	print $Match;
?>
<link rel=stylesheet href="IndexAssets/Index.css" />
<script type=importmap>
{
	"imports": {
		"@floating-ui/core": "./IndexAssets/floating-ui.core.browser.min.mjs",
		"@floating-ui/dom": "./IndexAssets/floating-ui.dom.browser.min.mjs"
	}
}
</script>
<script type="module" src="./IndexAssets/Index.js"></script>
</head><body>
<div role="tablist" class="Tabs TopBoxes HasMaxWidth" aria-label="Projects" id=RootTabs>
<? foreach($Projects as $ProjectName => $PData) { ?>
	<button role=tab class=Tab id=tab-<?=$PData->Slug?> data-title="<?=htmlentities($PData->ShortName)?>" aria-controls=panel-<?=$PData->Slug?>>
		<div class=TabLogo><img src="https://static.castledragmire.com/silksong/<?=$ProjectName?>/<?=$ProjectName?>LogoThumb.png" alt="<?=$PData->Name?> Logo" /></div>
		<div class="TabText"><?=str_replace("\n", '<br>', $PData->Name)?></div>
	</button>
<? } ?>
</div>
<div class="Links" aria-label="Links Sections">
<? foreach($Projects as $ProjectName => $PData) { ?>
	<div class="Boxes TopBoxes HasMaxWidth" id=panel-<?=$PData->Slug?> aria-labelledby=tab-<?=$PData->Slug?>>
		<a class=Tab href="https://www.nexusmods.com/hollowknightsilksong/mods/<?=$PData->NexusID?>">Nexus Mods: <?=$PData->ShortName?></a>
		<button role=tab class=Tab id=tab-<?=$PData->Slug?>-Description aria-controls=panel-<?=$PData->Slug?>-Description>Description</button>
<? if(isset($PData->Pictures)) { ?>
		<button role=tab class=Tab id=tab-<?=$PData->Slug?>-Pictures aria-controls=panel-<?=$PData->Slug?>-Pictures>Pictures</button>
<? } if(isset($PData->Videos)) { ?>
		<button role=tab class=Tab id=tab-<?=$PData->Slug?>-Videos aria-controls=panel-<?=$PData->Slug?>-Videos>Videos</button>
<? } if(isset($PData->Articles)) { ?>
		<div class="Tab MenuContainer" role=menu>
			Articles ☰
			<div class=MenuPopup>
				<? foreach(array_reverse($PData->Articles) as $Article) { $ArticleSlug=CreateSlug($Article->FileName); ?>
					<a role=tab class="Tab Article" id=tab-<?=$PData->Slug?>-Article_<?=$ArticleSlug?> aria-controls=panel-<?=$PData->Slug?>-Article_<?=$ArticleSlug?>>
						<div class=Title><?=htmlentities($Article->Title)?></div>
						<div class=Date><?=htmlentities($Article->Date)?></div>
					</a>
				<? } ?>
			</div>
		</div>
<? } foreach($PData->Links ?? [] as $LinkName => $LinkLocation) {
	if($LinkLocation==='') { ?>
		<a class="Tab Disabled"><span>Coming<br>soon</span><?=htmlentities($LinkName)?></a>
	<? } else { ?>
		<a class=Tab href="<?=htmlentities($LinkLocation)?>"><?=htmlentities($LinkName)?></a>
	<? } ?>
<? } ?>
	</div>
<? } ?>
</div>
<div id=Contents>
<? foreach($Projects as $ProjectName => $PData) { ?>
	<div role=tabpanel class="TabContents Description HasMaxWidth" id=panel-<?=$PData->Slug?>-Description aria-labelledby=tab-<?=$PData->Slug?>-Description>
		<?=ProcessHTMLFile($PData->HTML)?>
	</div>
	<? if(isset($PData->Pictures)) { ?>
	<div role=tabpanel class="TabContents Pictures HasMaxWidth" id=panel-<?=$PData->Slug?>-Pictures aria-labelledby=tab-<?=$PData->Slug?>-Pictures>
		<img src="https://static.castledragmire.com/silksong/<?=$ProjectName?>/Thumbs/Background.jpg" loading="lazy" decoding="async" alt="<?=$PData->ShortName?> Background" class="BackgroundImage DisplayImage" data-image-index=<?=count((array)$PData->Pictures)?>>
		<div class=LogoGrid>
			<? $i=0; foreach($PData->Pictures as $FileName => $Description) { ?>
			<div class=LogoCard>
				<?=ClearWS()?><img
					src="https://static.castledragmire.com/silksong/<?=$ProjectName?>/Thumbs/<?=$FileName?>"
					loading="lazy" decoding="async" class="DisplayImage"
					alt="<?=htmlentities($Description)?>"
					data-image-index=<?=$i++?>
				><?=EndClearWS()?>
				<span><?=htmlentities($Description)?></span>
			</div>
			<? } ?>
		</div>
	</div>
	<? } if(isset($PData->Videos)) { ?>
	<div role=tabpanel class="TabContents Videos HasMaxWidth" id=panel-<?=$PData->Slug?>-Videos aria-labelledby=tab-<?=$PData->Slug?>-Videos>
		<div class=LogoGrid>
			<? $i=0; foreach($PData->Videos as $FileName => $VData) { ?>
			<div class=LogoCard>
				<?=ClearWS()?><img
					src="https://static.castledragmire.com/silksong/<?=$ProjectName?>/Thumbs/<?=$FileName?>.jpg"
					loading="lazy" decoding="async" class="DisplayImage"
					alt="<?=htmlentities($VData->Description)?>"
					data-image-index=<?=$i++?>
					<?=!isset($VData->Source) ? '' : 'data-video-src="'.htmlentities($VData->Source).'">'?>
				><?=EndClearWS()?>
				<span><?=htmlentities($VData->Description)?></span>
			</div>
			<? } ?>
		</div>
	</div>
	<? } foreach(($PData->Articles ?? []) as $Article) { $ArticleSlug=$PData->Slug.'-Article_'.CreateSlug($Article->FileName); ?>
		<div role=tabpanel class="TabContents Article HasMaxWidth" id=panel-<?=$ArticleSlug?> aria-labelledby=tab-<?=$ArticleSlug?>>
			<div class=Title><?=htmlentities($Article->Title)?></div>
			<div class=Date><?=htmlentities($Article->Date)?></div>
			<div class=ArticleContents><?=ProcessHTMLFile(file_get_contents("IndexAssets/HTMLRenders/$ProjectName.Article.$Article->FileName.html"), 'panel-'.$ArticleSlug)?></div>
		</div>
	<? } ?>
<? } ?>
</div>
</body>
</html>