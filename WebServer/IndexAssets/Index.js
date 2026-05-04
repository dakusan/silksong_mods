import Tab from "./Tabs.js"
import PopupUtil from "./PopupUtil.js"
import "./glightbox.js"

class TabOverride extends Tab
{
	static IgnorePreloadList=['SilksongAnimations.gif'];
	HasLoaded=false;
	/** @type {Set<string>} */ PreloadedImageSources=new Set();
	/** @type {WeakSet<HTMLImageElement>} */ PreloadedImages=new WeakSet();
	/** @type {import("glightbox").GlightboxInstance} */ LightBox=null;
	/** @type {PopupUtil[]} */ Popups=[];
	constructor(Parent, Slug, Title, $Tab, $Contents, Index) {
		super(Parent, Slug, Title, $Tab, $Contents, Index);
	}

	//Prep new tab and its contents when first viewable
	SetContentsVisibility(IsVisible)
	{
		//Only do so once
		super.SetContentsVisibility(IsVisible);
		if(!IsVisible || this.HasLoaded)
			return;
		this.HasLoaded=true;

		//Check for popups to hook up
		for(const El of this.$Contents.getElementsByClassName('MenuContainer')) {
			//Create the popup and set the tab to open/close it
			const NewPopup=new PopupUtil(El.getElementsByClassName('MenuPopup')[0], El);
			this.Popups.push(NewPopup);
			El.addEventListener("click", e => {
				if(e.target===El) //Only open for the tab itself being clicked
					NewPopup.Toggle()
			});

			//Clicking tab elements within the popup close it
			const BindHide=NewPopup.Hide.bind(NewPopup);
			for(const T of El.getElementsByClassName('Tab'))
				T.addEventListener('click', BindHide);
		}

		//Make sure all images in visible contents load immediately (ignore lazy)
		for(const CurImage of this.$Contents.querySelectorAll("img"))
			if(!CurImage.complete && !(CurImage.naturalWidth>0)) {
				const Src=CurImage.currentSrc || CurImage.getAttribute("src");
				if(TabOverride.IgnorePreloadList.indexOf(Src.substring(Src.lastIndexOf('/')+1))!==-1 || this.PreloadedImageSources.has(Src))
					continue;
				const Img=new Image();
				Img.decoding="async";
				Img.src=Src;
				this.PreloadedImageSources.add(Src);
				this.PreloadedImages.add(Img);
			}

		//Prep all images to load the glightbox
		/** @type { (HTMLImageElement|HTMLVideoElement)[] } */
		const Images=
			[...this.$Contents.getElementsByClassName('DisplayImage')]
			.sort((ImgA, ImgB) => Number(ImgA.dataset.imageIndex)-Number(ImgB.dataset.imageIndex));
		if(Images.length===0)
			return;
		//noinspection TypeScriptUMDGlobal
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
		//NOTE: glightbox currently has a bug where looping when swiping does not work. The .js file must be manually changed to fix this as everything is private within a scope.

		const ImageListener=this.OpenLightbox.bind(this);
		for(const CurImage of Images)
			CurImage.addEventListener('click', ImageListener);
	}
	OpenLightbox(e) { this.LightBox.openAt(Number(e.currentTarget.dataset.imageIndex)); }
}
document.addEventListener("DOMContentLoaded", () =>
	Tab.InitRoot(/** @type {Tab} */ window.RootTab=new TabOverride(null, null, document.title, null, null))
);