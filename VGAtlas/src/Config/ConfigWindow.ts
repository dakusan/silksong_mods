import type Translations from '../Util/Translations';
import { Window } from '../Util/WindowManager';
import { Config } from './Abstract/ConfigItem';

export default class ConfigWindow extends Window
{
	constructor(
		public readonly Config:Config,
		public readonly Tr?:Translations,
	) {
		super({SaveID:'Config'+Config.Prefix, Width:750, Height:550});
	}

	public override OnClosing()
	{
		return false;
	}
}