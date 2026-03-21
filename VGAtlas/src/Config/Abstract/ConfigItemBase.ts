import $ from "jquery";
import { Util, WillBeSet } from '../../Util/SharedClasses';
import type Config from '../Config';

export class Options
{
	public Description?:string;
	public IsAdvanced=false;
	public Hide=false;
}

export default abstract class ConfigItemBase
{
	protected constructor(
		public readonly Section:string,
		public readonly Key:string,
		Opts?:Partial<Options>,
	) {
		this.Options=Util.AssignProps(new Options(), Opts ?? {});
	}

	protected Parent:Config=WillBeSet;
	public readonly Options:Readonly<Options>;
	protected abstract Init(Parent:Config): void;

	public readonly $DOMHolder=$(document.createElement('div'));
	protected abstract ValueSet(): void;
}