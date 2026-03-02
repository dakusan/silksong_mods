<? /** @noinspection PhpShortOpenTagInspection */
$Projects=(array)json_decode(
	preg_replace(['/,(\s+[}\]])/', '~^//.*$~m'], ['$1', ''], file_get_contents('IndexAssets/Projects.json'))
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
<link rel=stylesheet href="IndexAssets/Index.css" />
<link rel=stylesheet href="IndexAssets/glightbox.css" />
</head><body>
<div role="tablist" class="Tabs TopBoxes" aria-label="Projects" id=RootTabs>
<? foreach($Projects as $ProjectName => $PData) { ?>
	<button role=tab class=Tab id=tab-<?=$PData->Slug?> data-title="<?=htmlentities($PData->ShortName)?>" aria-controls=panel-<?=$PData->Slug?>>
		<div class=TabLogo><img src="https://static.castledragmire.com/silksong/<?=$ProjectName?>/<?=$ProjectName?>LogoThumb.png" alt="<?=$PData->Name?> Logo" /></div>
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
<? } if(isset($PData->Videos)) { ?>
		<button role=tab class=Tab id=tab-<?=$PData->Slug?>-Videos aria-controls=panel-<?=$PData->Slug?>-Videos>Videos</button>
<? } if(isset($PData->Articles)) { ?>
		<div class="Tab MenuContainer" role=menu>
			Articles
			<div class=MenuPopup>
				<? foreach($PData->Articles as $Article) { $ArticleSlug=CreateSlug($Article->File); ?>
					<a role=tab class="Tab Article" id=tab-<?=$PData->Slug?>-Article_<?=$ArticleSlug?> aria-controls=panel-<?=$PData->Slug?>-Article_<?=$ArticleSlug?>>
						<div class=Title><?=htmlentities($Article->Title)?></div>
						<div class=Date><?=htmlentities($Article->Date)?></div>
					</a>
				<? } ?>
			</div>
		</div>
<? } ?>
		<a class="Tab Disabled"><span>Coming<br>soon</span>Github</a>
		<a class="Tab Disabled"><span>Coming<br>soon</span>Download</a>
	</div>
<? } ?>
</div>
<div id=ContentsFrame><div id=Contents>
<? $PrintAfter='</style></details></div>'; foreach($Projects as $ProjectName => $PData) { ?>
	<div role=tabpanel class=TabContents id=panel-<?=$PData->Slug?>-Description aria-labelledby=tab-<?=$PData->Slug?>-Description>
		<?=preg_replace('/<img(\s+)/i', '<img loading=lazy decoding=async$1', substr($PData->HTML, strpos($PData->HTML, $PrintAfter)+strlen($PrintAfter)))?>
	</div>
	<? if(isset($PData->Pictures)) { ?>
	<div role=tabpanel class="TabContents Pictures" id=panel-<?=$PData->Slug?>-Pictures aria-labelledby=tab-<?=$PData->Slug?>-Pictures>
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
	<div role=tabpanel class="TabContents Videos" id=panel-<?=$PData->Slug?>-Videos aria-labelledby=tab-<?=$PData->Slug?>-Videos>
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
	<? } foreach(($PData->Articles ?? []) as $Article) { $ArticleSlug=CreateSlug($Article->File); ?>
		<div role=tabpanel class="TabContents Article" id=panel-<?=$PData->Slug?>-Article_<?=$ArticleSlug?> aria-labelledby=tab-<?=$PData->Slug?>-Article_<?=$ArticleSlug?>>
			<?=$Article->File?> :: <?=$Article->Title?>
		</div>
	<? } ?>
<? } ?>
</div></div>
</body>
<script type=importmap>
{
	"imports": {
		"@floating-ui/core": "./IndexAssets/floating-ui.core.browser.min.mjs",
		"@floating-ui/dom": "./IndexAssets/floating-ui.dom.browser.min.mjs"
	}
}
</script>
<script type="module" src="./IndexAssets/Index.js"></script>
</html>