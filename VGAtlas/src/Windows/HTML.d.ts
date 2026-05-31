declare module "*.html?raw"
{
	const Content:string;
	export default Content;
}

declare module "*.html?minraw"
{
	const Content:string;
	export default Content;
}

//Fix for https://github.com/microsoft/TypeScript/issues/17002
//noinspection JSUnusedGlobalSymbols
interface ArrayConstructor {
	//eslint-disable-next-line @typescript-eslint/naming-convention
	isArray(arg: unknown): arg is readonly unknown[];
}