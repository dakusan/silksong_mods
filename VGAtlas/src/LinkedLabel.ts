import $ from 'jquery';
import { Util, StatStr } from './Util/SharedClasses';
import { Share } from './Share';

const LTChar='\u0084', RTChar='\u0086';
const RegEx_LinkID=/<LinkID=(?:\\")?([^ ">]+)(?:\\")?>(.*?)<\/LinkID>/isg;
const RegEx_Color=/<color=(?:--VarColor-(\w+)-)?(#?\w+)>((?:(?!<color\b)[\s\S])*?)<\/color>/ig;
const RegEx_Size=/<size=([-+]?)([1-5])>((?:(?!<size\b)[\s\S])*?)<\/size>/ig;
const RegEx_Attr=/<ATTR=([-.\w]+)>(.*?)<\/ATTR>/ig;
const RegEx_SafeTags=/<\/?(?:b|i|u|s|strong|em|ins|del)>/ig;
const RegEx_LTGT=/[<>]/g;
const RegEx_LTGTChar=new RegExp(`[${LTChar}${RTChar}]`, 'g');
const ClassWhitelist=/^(?:ItemIcon|I\d{1,4}|FontSize|F[NP]?[1-5]|WinButton|VC_(?:SepOR|SepAND|CollectedCounts)|Strike(?:Found|Started)|Flag(?:Recommended|Started|Not))$/;

export default class LinkedLabel
{
	public readonly RenderedContents:string;
	public readonly $Content=$('<div>');
	private HasInitialized=false;

	constructor(
		public readonly StartContents:string,
	) {
		let Ret=StartContents.replace(RegEx_LTGTChar, StatStr.PrivateChar); //If used, change our private range escape characters to the character at the start of the private range.
		Ret=LinkedLabel.UnityRichTextToHTML(Ret);
		Ret=LinkedLabel.KeepSafeHTMLTags(Ret);
		Ret=LinkedLabel.FixTags(Ret);
		this.RenderedContents=Ret;
	}
	public Init()
	{
		if(this.HasInitialized)
			return this.$Content;
		this.HasInitialized=true;

		this.$Content.html(this.RenderedContents);
		this.$Content.find('a').each((_, El) => {
			if(!El.hasAttribute('href'))
				El.setAttribute('href', '#');
			else if(!El.getAttribute('href')!.startsWith('#'))
				return void $(El).attr({target:'_blank', rel:'noopener'});

			$(El).on('click', this.AnchorSelected.bind(this));
		});

		return this.$Content;
	}

	private AnchorSelected(Ev:JQuery.ClickEvent)
	{
		const Anchor=Ev.currentTarget as HTMLAnchorElement;
		const ItemID=Util.GetInt($(Anchor).attr('data-ItemID'));
		if(ItemID===null) {
			Ev.preventDefault();
			Ev.stopImmediatePropagation();
			return;
		}

		//Only intercept a plain left-click. Let middle-click / ctrl+click / cmd+click / etc. do normal navigation
		if(Ev.which!==1 || Ev.ctrlKey || Ev.metaKey || Ev.shiftKey || Ev.altKey)
			return;
		Ev.preventDefault();
		Ev.stopImmediatePropagation();

		Share.DS.LinkSelected(ItemID);
	}

	//Revert our escapes and change < and > to their HTML escaped equivalents
	private static FixTags(Str:string)
	{
		return Str
			.replaceAll('&', '&amp;')
			.replace(RegEx_LTGT		, F => F==='<'		? '&lt;': '&gt;')
			.replace(RegEx_LTGTChar	, F => F===LTChar	? '<'	: '>'	);
	}

	//Temporarily escape our allowed html tags
	private static KeepSafeHTMLTags(Str:string)
	{
		return Str.replace(RegEx_SafeTags, F => LTChar+F.slice(1, -1)+RTChar);
	}

	//Convert LinkIDs to anchors and <color> w/ span+color
	private static UnityRichTextToHTML(Str:string)
	{
		//Replace LinkID with anchors
		Str=Str.replace(RegEx_LinkID, this.ParseLinkID);

		//Replace <color> w/ span+color
		for(let LastStr:string|undefined=undefined; Str!==LastStr; )
			Str=(LastStr=Str).replace(RegEx_Color, (_, VC, Color, Text) => `${LTChar}span style='color:${Color}'${VC ? ' class=VC_'+VC : ''}${RTChar}${Text}${LTChar}/span${RTChar}`);
		//Replace <size> w/ span+FontSize
		for(let LastStr:string|undefined=undefined; Str!==LastStr; )
			Str=(LastStr=Str).replace(RegEx_Size, (_, Sign, Num, Text) => `${LTChar}span class='FontSize F${Sign==='+' ? 'P' : Sign==='-' ? 'N' : ''}${Num}'${RTChar}${Text}${LTChar}/span${RTChar}`);

		return Str;
	}

	//Parse a link and change it to an anchor
	private static ParseLinkID(_Full:string, LinkID:string, Inner:string)
	{
		const Attrs=new Map<string, string>();
		Inner=Inner.replace(
			RegEx_Attr,
			(_Full, Name:string, Value:string) => {
				Attrs.set(Name, Value);
				return StatStr.Empty;
			}
		);

		//Gather attributes, styles, and classes
		const NewEl=$(document.createElement('a'));
		let V:string|undefined;
		const ClearAttr	=(Name:string, _:JQuery			) => Attrs.delete(Name);
		const AddAttr	=(Name:string,NoDataPrefix=false) => (V=Attrs.get(Name))===undefined ? false : ClearAttr(Name, NewEl.attr		((NoDataPrefix ? StatStr.Empty : 'data-')+Name, V));
		const AddStyle	=(Name:string, CSSName:string	) => (V=Attrs.get(Name))===undefined ? false : ClearAttr(Name, NewEl.css		(CSSName, V));
		const AddClass	=(Name:string					) => (V=Attrs.get(Name))===undefined ? false : ClearAttr(Name, NewEl.addClass	(Name));

		//Static attributes that are handled differently
		let ItemIcon:string=StatStr.Empty;
		NewEl.attr('data-LinkID', LinkID);
		if(AddAttr('ItemID')) {
			NewEl.attr('href', '#'+V);
			const Item=Share.DS.Items.get(Util.GetNumber(V, true)!);
			const ItemIconID=Item?.IconID;
			const FinalIconID=((ItemIconID ?? -1)!==-1 ? ItemIconID : Share.DS.Categories.get(Item?.CategoryID ?? -1)?.IconID);
			if(FinalIconID!==undefined)
				ItemIcon=`${LTChar}span class='ItemIcon I${FinalIconID}'${RTChar}${LTChar}/span${RTChar}`;
			if(Item?.IsFound || Item?.IsStarted)
				NewEl.addClass(Item.IsFound ? 'StrikeFound' : 'StrikeStarted');
		} else if(AddAttr('href', true))
			NewEl.attr('href', (_, Href) => (/^(?:#|https?:\/\/)/i.test(Href) ? StatStr.Empty : '#')+Href); //Guard against HREFs that don’t start with # or http/https
		AddStyle('NormalColor', 'color');
		if(AddClass('Important'))
			NewEl.css('--phase', Math.floor(Math.random()*1000)/1000);

		//Any remaining attributes are turned into named attributes (with a value) or classes (no value)
		for(const [Name, Value] of Attrs.entries())
			if(Value)
				NewEl.attr('data-'+Name, Value);
			else if(ClassWhitelist.test(Name))
				AddClass(Name);

		//Finish combining attributes and return
		return `${ItemIcon}${LTChar}${NewEl[0].outerHTML.slice(1).replace(/>.*/, StatStr.Empty)}${RTChar}${Inner}${LTChar}/a${RTChar}`;
	}
}