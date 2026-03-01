<? /** @noinspection PhpShortOpenTagInspection, CssUnusedSymbol */
$Projects=(array)json_decode(
	preg_replace(['/,(\s+[}\]])/', '~^//.*$~m'], ['$1', ''], file_get_contents('IndexAssets/Projects.json'))
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
<link rel=stylesheet href="IndexAssets/index.css" />
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
				<img
					src="https://static.castledragmire.com/silksong/<?=$ProjectName?>/Thumbs/<?=$FileName?>"
					loading="lazy" decoding="async" class="DisplayImage"
					alt="<?=htmlentities($Description)?>"
					data-image-index=<?=$i++?>
				>
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
				<img
					src="https://static.castledragmire.com/silksong/<?=$ProjectName?>/Thumbs/<?=$FileName?>.jpg"
					loading="lazy" decoding="async" class="DisplayImage"
					alt="<?=htmlentities($VData->Description)?>"
					data-image-index=<?=$i++?>
					<?=!isset($VData->Source) ? '' : 'data-video-src="'.htmlentities($VData->Source).'">'?>
				>
				<span><?=htmlentities($VData->Description)?></span>
			</div>
			<? } ?>
		</div>
	</div>
	<? } ?>
<? } ?>
</div></div>
</body>
<script type="module">
import Tab from "./IndexAssets/Tabs.js"
import "./IndexAssets/glightbox.js"
class TabOverride extends Tab
{
	HasLoadedImages=false;
	PreloadedImages=new WeakSet();
	/** @type {import("glightbox").GlightboxInstance} */ LightBox=null;
	constructor(Parent, Slug, Title, $Tab, $Contents, Index) {
		super(Parent, Slug, Title, $Tab, $Contents, Index);
	}
	Select(UpdateHashAndTitle=true, AutoSelectFirstChild=true)
	{
		super.Select(UpdateHashAndTitle, AutoSelectFirstChild);
		document.getElementById('ContentsFrame').classList.toggle("FullBleed", this.Title==="Pictures" || this.Title==="Videos");
	}

	//Prep image content the first time pictures become visible
	SetContentsVisibility(IsVisible)
	{
		super.SetContentsVisibility(IsVisible);
		if(!IsVisible || this.HasLoadedImages)
			return;
		this.HasLoadedImages=true;

		//Make sure all images in visible contents load immediately (ignore lazy)
		for(const CurImage of this.$Contents.querySelectorAll("img"))
			if(!CurImage.complete && !(CurImage.naturalWidth>0)) {
				const Img=new Image();
				Img.decoding="async";
				Img.src=CurImage.currentSrc || CurImage.getAttribute("src");
				this.PreloadedImages.add(Img);
			}

		//Prep all images to load the glightbox
		/** @type { (HTMLImageElement|HTMLVideoElement)[] } */
		const Images=
			[...this.$Contents.getElementsByClassName('DisplayImage')]
			.sort((ImgA, ImgB) => Number(ImgA.dataset.imageIndex)-Number(ImgB.dataset.imageIndex));
		if(Images.length===0)
			return;
		this.LightBox=GLightbox({
			elements:Images.map(I => {
				let Src=I.getAttribute('src').replace('/Thumbs', '');
				const IsVideo=Src.match(/\.(mp4|webm|ogg)\.jpg$/i);
				if(IsVideo)
					Src=(I.dataset.videoSrc ?? Src.slice(0, '.jpg'.length*-1));

				return {
					href:Src,
					type:IsVideo ? "video" : "image",
					title:I.getAttribute('alt'),
				};
			}),
			loop:true, touchNavigation:true, keyboardNavigation:true,
		});

		const ImageListener=this.OpenLightbox.bind(this);
		for(const CurImage of Images)
			CurImage.addEventListener('click', ImageListener);
	}
	OpenLightbox(e) { this.LightBox.openAt(e.currentTarget.dataset.imageIndex); }
}
Tab.InitRoot(/** @type {Tab} */ new TabOverride(null, null, document.title, null, null));
</script>
</html>