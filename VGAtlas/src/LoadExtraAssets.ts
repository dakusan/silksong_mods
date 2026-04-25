export { }
const GTExtraAssets=(globalThis as typeof globalThis & {
	ExtraAssets?:Record<string, string>,
	ResolveExtraAssetsPromise?(): void,
});

GTExtraAssets.ExtraAssets=Object.fromEntries(Object.entries(import.meta.glob(
	[
		'../Assets/*.json',
		'../Assets/*.png',
		'../Assets/Translations/**/??.tr.json',
		'../Assets/Translations/**/Languages.json',
	],
	{
		eager : true,
		query : '?url',
		import: 'default',
	}
) as Record<string, string>).map(([k, v]) => [k.slice(3), v])); //Remove the ../

GTExtraAssets.ResolveExtraAssetsPromise?.();
delete GTExtraAssets.ResolveExtraAssetsPromise;