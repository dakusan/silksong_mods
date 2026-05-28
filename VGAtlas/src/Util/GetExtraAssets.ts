import { Util } from './SharedClasses';
import { LoadJson as ExtLoadJson } from './JSON';

namespace GetExtraAssets
{
	const GTExtraAssets=(globalThis as typeof globalThis & {
		ExtraAssets?:Record<string, string>,
		ResolveExtraAssetsPromise?(): void,
	});
	let MyPromise:Promise<void>|null=null;

	async function WaitForEA(): Promise<Record<string, string>>
	{
		if(GTExtraAssets.ExtraAssets) //Race condition guard
			return GTExtraAssets.ExtraAssets;

		if(!MyPromise)
			MyPromise=new Promise<void>(Resolve => GTExtraAssets.ResolveExtraAssetsPromise=Resolve);
		await MyPromise;
		MyPromise=null;
		return GTExtraAssets.ExtraAssets!;
	}

	function TryPath(Path:string, Mappings:Readonly<Record<string, string>>): string
	{
		if(!Mappings[Path])
			throw new Error("Could not find asset mapping for path: "+Path);
		return Mappings[Path];
	}

	export async function LoadJson	(Path:string, ForceReload=false, PreProcessing?:(Str:string) => string)
		 										 : Promise<object		> { return await ExtLoadJson.FromURL(TryPath(Path, GTExtraAssets.ExtraAssets ?? await WaitForEA()), ForceReload, PreProcessing); }
	export async function LoadImage	(Path:string): Promise<ImageBitmap	> { return await Util.LoadImage		(TryPath(Path, GTExtraAssets.ExtraAssets ?? await WaitForEA())); }
	export async function GetPath	(Path:string): Promise<string		> { return							(TryPath(Path, GTExtraAssets.ExtraAssets ?? await WaitForEA())); }
}
export default GetExtraAssets;