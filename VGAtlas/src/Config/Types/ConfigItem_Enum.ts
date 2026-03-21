import $ from 'jquery';
import ConfigItem, { Options } from '../Abstract/ConfigItem';

export default class ConfigItem_Enum extends ConfigItem<string>
{
	protected $SelectBox=$(document.createElement('select')).addClass('Enum').appendTo(this.$DOMHolder);
	protected EnumValues=new Map<string, string>();
	constructor(Section:string, Key:string, Default:string, List:Map<string, string>|Record<string, string>, Opts?:Partial<Options>)
	{
		super(Section, Key, Default, Opts);
		this.$SelectBox
			.append($('<option class=NoVal selected disabled>').text("Select A Value"))
			.on('change', () =>
				this.SetVal(this.$SelectBox.val() as string, true)
			);
		this.AddList(List);
	}
	public AddList(List:Map<string, string>|Record<string, string>)
	{
		for(const [Key, Value] of (List instanceof Map ? List : Object.entries(List)))
			this.Add(Key, Value);
	}
	public Add(Key:string, Value:string)
	{
		if(this.EnumValues.has(Key))
			throw new Error("Key already used: "+Key);
		this.$SelectBox.append($('<option>').val(Key).text(Value));
		this.EnumValues.set(Key, Value);
	}
	protected override ValueSet() { this.$SelectBox.val(this.V); }
}