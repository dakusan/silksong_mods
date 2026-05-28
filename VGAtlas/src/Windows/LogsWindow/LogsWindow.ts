import './LogsWindow.scss';
import $						  from 'jquery';
import { Log					} from '../../Util/SharedClasses';
import { TranslatePassthrough	} from '../../Util/Translations';
import { Window					} from '../../Util/WindowManager';
import { Share					} from '../../Share';

export default class LogsWindow extends Window
{
	private static WinIDCounter=0;
	private MyLogWinID=LogsWindow.WinIDCounter++;
	constructor() {
		super({
			SaveID:'Logs', Type:'Logs',
			TitleTranslator:new TranslatePassthrough("Logs", undefined, undefined, Share.Tr),
		});

		Log.OnLog.Add('LogWindow'+this.MyLogWinID, this.AddLogLine.bind(this));
		for(const LL of Log.AllLogLines)
			this.AddLogLine(LL);
	}
	public override OnClosing(): boolean
	{
		Log.OnLog.Remove('LogWindow'+this.MyLogWinID);
		return false;
	}

	private AddLogLine(LL:Readonly<(typeof Log.AllLogLines)[number]>): void
	{
		this.$Content.children().slice(Log.MaxStoredLogLines-1).remove();
		$('<div class=LogLine>').append([
			$('<span class=Time>').text(LL.Time.toLocaleTimeString([], {hour:'2-digit', minute:'2-digit', second:'2-digit'})),
			$('<span class=Contents>').text(String(LL.LogInfo[0])),
		])
			.toggleClass('IsError', LL.IsError)
			.prependTo(this.$Content);
	}
}