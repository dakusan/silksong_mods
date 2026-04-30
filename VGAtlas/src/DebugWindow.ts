import { Window } from './Util/WindowManager';
import { Share } from './Share';

export default class DebugWindow extends Window
{
	constructor()
	{
		//Base HTML+translations setup
		super({SaveID:'Debug', Type:'Debug', Width:400, Height:250});
		this.LanguageChanged();
	}

	public override LanguageChanged()
	{
		Share.Tr.OnLanguageLoadedOnce(() => {
			this.Title=Share.Tr.TDef('Title', 'DebugWindow', "Debug");
		});
	}
}