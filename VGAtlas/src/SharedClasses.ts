export class Vector2 { constructor(public x:number, public y:number) { } }

export class Util
{
	static async LoadImage(ImageURL:string): Promise<ImageBitmap>
	{
		const LoadImage=new Image();
		await new Promise<void>((Resolve, Reject) => {
			LoadImage.onload=() => Resolve();
			LoadImage.onerror=() => Reject(new Error("Image load failed for:\n"+ImageURL));
			LoadImage.src=ImageURL;
		});
		return await createImageBitmap(LoadImage);
	}

	public static GetErrorMessage(e:any): string { return (e instanceof Error ? e.message : String(e)); }
}