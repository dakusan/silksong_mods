<? /** @noinspection PhpShortOpenTagInspection */
//Note: Project names cannot contain spaces
$Projects=[
	'PharloomAtlas'	=>(object)['NexusID'=>755, 'Name'=>'Pharloom Atlas (Map overhaul)'],
	'SilkDev'		=>(object)['NexusID'=>510, 'Name'=>'Plugin Developer Tools'],
	'NoClip'		=>(object)['NexusID'=>478, 'Name'=>'No Clip'],
	'PinFinder'		=>(object)['NexusID'=>772, 'Name'=>'Pin Finder'],
];

foreach($Projects as $ProjectName => $PData) {
	$PData->HTML=file_get_contents(__DIR__."/Assets/$ProjectName.AboutMe.html");
	$PData->Slug=strtolower(preg_replace('/(?<!^)([A-Z])/', '-$1', $ProjectName));
}
?>
<!DOCTYPE html>
<html xmlns="http://www.w3.org/1999/xhtml" lang="en">
<head>
	<title>Silksong Mods by Dakusan</title>
	<meta charset="UTF-8">
	<style>
<?
	preg_match_all('/<style\b[^>]*>(.*?)<\/style>/is', $Projects['PharloomAtlas']->HTML, $Matches);
	foreach($Matches[1] as $Match)
		print $Match;
?>
	.TabContents:not(.Selected), .TopBoxes.Boxes:not(.Selected) { display:none; }
	.TopBoxes {
		display:flex; flex-wrap:wrap; gap:10px; margin:0 0 14px 0; padding:10px; border:1px solid rgba(255, 255, 255, 0.10); border-radius:12px;
		background:rgba(0, 0, 0, 0.25); backdrop-filter:blur(6px); overflow-x:auto;
	}
	.Tab {
		min-height:40px; padding:12px 13px; border-radius:12px; border:1px solid rgba(255, 255, 255, 0.12);
		background:rgba(255, 255, 255, 0.06); color:rgba(255, 255, 255, 0.85);
		font-size:16px; font-weight:600; line-height:1; letter-spacing:0.2px; white-space:nowrap;
		transition:transform 120ms ease, background 120ms ease, border-color 120ms ease, color 120ms ease;
		user-select:none; cursor:pointer;
	}
	.Tab:hover		{ background:rgba(255, 255, 255, 0.10); border-color:rgba(255, 255, 255, 0.18); }
	.Tab.Selected	{ background:rgba(255, 255, 255, 0.16); border-color:rgba(255, 255, 255, 0.28); color:rgba(255, 255, 255, 0.98); }
	.Tab:active		{ transform:translateY(1px); }
	a.Tab { min-height:inherit; }

	.Contents {
		padding:14px 16px; border:1px solid rgba(255, 255, 255, 0.10); border-radius:14px;
		background:rgba(0, 0, 0, 0.20);
	}
	.TabContents.Selected { animation:TabFade 120ms ease-out; }
	@keyframes TabFade {
		from{ opacity:0; transform:translateY(2px); }
		to	{ opacity:1; transform:translateY(0); }
	}

	.Tab.Disabled, .Tab[aria-disabled="true"] {
		position:relative;
		opacity:0.55; filter:saturate(0.6);
		cursor:not-allowed; pointer-events:none;
	}
	.Tab.Disabled::after, .Tab[aria-disabled="true"]::after {
		position:absolute; inset:0; border-radius:inherit;
		content:""; background:rgba(0, 0, 0, 0.35);
	}
	.Tab.Disabled::before, .Tab[aria-disabled="true"]::before {
		position:absolute; inset:0; border-radius:inherit;
		content:""; background:linear-gradient(160deg, transparent 47%, rgba(255, 60, 60, 0.95) 48%, rgba(255, 60, 60, 0.95) 52%, transparent 53%);
		pointer-events:none;
	}
	.Tab.Disabled>*, .Tab[aria-disabled="true"]>* { position:relative; z-index:1; }
</style>
</head><body>
<div role="tablist" class="Tabs TopBoxes" aria-label="Projects">
<? foreach($Projects as $ProjectName => $PData) { ?>
	<button role=tab class=Tab id=tab-<?=$PData->Slug?> data-ProjectName=<?=$ProjectName?> aria-controls=panel-<?=$PData->Slug?>><?=$PData->Name?></button>
<? } ?>
</div>
<div class="Links" aria-label="Links Sections">
<? foreach($Projects as $ProjectName => $PData) { ?>
	<div class='Boxes TopBoxes' data-ProjectName=<?=$ProjectName?> aria-label=Links>
		<a class=Tab href="https://www.nexusmods.com/hollowknightsilksong/mods/<?=$PData->NexusID?>">Nexus Mods Page</a>
		<a class="Tab Disabled"><span>Download</span></a>
	</div>
<? } ?>
</div>
<div class=Contents>
<? $PrintAfter='</style></details></div>'; foreach($Projects as $ProjectName => $PData) { ?>
	<div role=tabpanel class=TabContents id=panel-<?=$PData->Slug?> aria-labelledby=tab-<?=$PData->Slug?> data-ProjectName=<?=$ProjectName?>>
		<?=substr($PData->HTML, strpos($PData->HTML, $PrintAfter)+strlen($PrintAfter))?>
	</div>
<? } ?>
</div>
</body>
<script>
function InitIndex()
{
	const StartTitle=document.title;
	const Tabs		=[...document.querySelectorAll('.Tabs .Tab')];
	const Contents	=[...document.querySelectorAll('.Contents .TabContents, .Links .Boxes')];

	function FromHash()
	{
		const H=decodeURIComponent((location.hash || '').slice(1)).trim();
		return H && Tabs.find(t => (t.dataset.projectname || '')===H) ? H : null;
	}
	function SetHash(ProjectName)
	{
		const Target=`#${encodeURIComponent(ProjectName)}`;
		if(location.hash!==Target)
			history.pushState(null, '', Target);
	}

	function SelectTab(ProjectName, Opt={})
	{
		const { UpdateHash=true }=Opt;

		for(const T of Tabs) {
			const IsSelected=(T.dataset.projectname===ProjectName);
			T.classList.toggle('Selected'	, IsSelected);
			T.setAttribute('aria-selected'	, IsSelected ? 'true' : 'false');
			T.tabIndex=IsSelected ? 0 : -1;
		}
		for(const C of Contents)
			C.classList.toggle('Selected', C.dataset.projectname===ProjectName);

		document.title=`${ProjectName}: ${StartTitle}`;
		if(UpdateHash)
			SetHash(ProjectName);
	}

	for(const T of Tabs) {
		T.addEventListener('click', () => SelectTab(T.dataset.projectname));
		T.addEventListener('keydown', e => {
			if(e.key==='Enter' || e.key===' ') {
				e.preventDefault();
				SelectTab(T.dataset.projectname);
			}
		});
	}

	//Hash changes
	window.addEventListener('hashchange', () => {
		const H=FromHash();
		if(H)
			SelectTab(H, { UpdateHash:false });
	});

	//Default
	const Pre=
		   FromHash()
		|| Tabs.find(t => t.classList.contains('Selected'))?.dataset.projectname
		|| Tabs[0]?.dataset.projectname;
	if(Pre)
		SelectTab(Pre, { UpdateHash:true });
}
InitIndex();
</script>
</html>