import { defineConfig, IndexHtmlTransformContext, Plugin } from "vite"
import { minify } from "html-minifier-terser"
import fs from "node:fs/promises"

const AddVisualizer=false;
const EmitSourceMaps=false;

let UtilsAndConfigsRegEx:RegExp;
{
	const UtilFiles='SharedClasses|Translations|WindowManager|GetExtraAssets|AlignText|JSON';
	const RootFiles='AtlasConfig|SaveData|LinkedLabel';
	UtilsAndConfigsRegEx=new RegExp(`/src/Util/(?:${UtilFiles})\\.ts|/src/(${RootFiles})\\.ts|/src/Config/`);
}
function IsUtilsAndConfigsChunk(id:string): boolean
{
	return UtilsAndConfigsRegEx.test(id.replaceAll("\\", "/"));
}

//noinspection JSUnusedGlobalSymbols
export default defineConfig({
	base: './',
	appType: 'mpa', //Return 404 for invalid routes
	build: {
		sourcemap: EmitSourceMaps,
		assetsInlineLimit(filePath) {
			if(/\/Assets\/.*\.(json|png)$/i.test(filePath.replaceAll("\\", "/"))) //JSON and PNG files in /Assets/*
				return false; //Never inline these
			return undefined; //Default behavior for everything else
		},
		rollupOptions: {
			input: {
				index: "index.html",
				LoadExtraAssets: "src/LoadExtraAssets.ts", //Keep assets and code separate
			},
			output: {
				entryFileNames: "assets/[name].[hash].js", //Have file hashes use hash.ext format for better no-cache regex
				chunkFileNames: "assets/[name].[hash].js",
				assetFileNames: "assets/[name].[hash][extname]",
				manualChunks(id) //Output the following modules into their own chunk
				{
					if(id.includes('crypto-js'))		return 'crypto-js';
					if(id.includes('jquery'))			return 'jquery';
					if(IsUtilsAndConfigsChunk(id))		return 'UtilsAndConfigs';
					 									return undefined;
				},
			},
		},
		minify: 'terser',
		terserOptions: {
			compress: {
				passes: 3,
				reduce_vars: true,
				reduce_funcs: true,
				inline: true,
				evaluate: true,
			},
			mangle: true,
		},
	},
	resolve: {
		preserveSymlinks: true, //Don’t resolve symlinks (which breaks assetsInlineLimit paths)
		alias: [
			{ find: /^jquery$/, replacement: "jquery/slim" }, //Force jquery to use slim
		],
	},
	server: {
		fs: { strict: true },
		host:"0.0.0.0",
	},
	plugins: [
		InjectLoadExtraAssets() as Plugin, //Inject ExtraAssets into index.html without connecting it into the graph
		...(!AddVisualizer ? [] : [
			(await import("rollup-plugin-visualizer")).visualizer({
				open: true,
				gzipSize: true,
				brotliSize: true,
				filename: "/tmp/vite-rollup.html",
			})
		]),
		MinifyHtmlRaw(),
		HTMLMinifyIndex(),
	],
});

function HTMLMinifyIndex(): Plugin
{
	return {
		name: "html-minify",
		enforce: "post",
		transformIndexHtml: {
			order: "post",
			handler: CallMinify,
		},
	};
}

async function CallMinify(html:string): Promise<string>
{
	return await minify(html, {
		collapseWhitespace: true,
		removeComments: true,
		removeRedundantAttributes: true,
		minifyCSS: true,
		minifyJS: true,
		removeAttributeQuotes: true,
	})
}

function MinifyHtmlRaw(): Plugin
{
	return {
		name: "minify-html-raw",
		enforce: "pre",

		async load(ID)
		{
			if(!ID.endsWith(".html?minraw"))
				return null;

			const File=ID.slice(0, -"?minraw".length);
			const Html=await fs.readFile(File, "utf8");
			const Minified=await CallMinify(Html);
			return `export default ${JSON.stringify(Minified)};`;
		},
	};
}

function InjectLoadExtraAssets(): Plugin
{
	function GetPath(Path:string, ctx:IndexHtmlTransformContext): string
	{
		if(ctx.server)
			return Path;

		for(const Item of Object.values(ctx.bundle ?? {}))
			if(
				   Item.type==="chunk"
				&& Item.isEntry
				&& Item.facadeModuleId?.endsWith(Path)
			)
				return "/"+Item.fileName;

		throw new Error("Could not find build entry for "+Path);
	}

	return {
		name: "inject-load-extra-assets",
		transformIndexHtml: {
			order: "post",
			handler(html:string, ctx:IndexHtmlTransformContext)
			{
				return {html, tags:[{
					tag:"script",
					attrs:{type:"module", src:GetPath("/src/LoadExtraAssets.ts", ctx)},
					injectTo: "head-prepend",
				}]};
			},
		},
	};
}