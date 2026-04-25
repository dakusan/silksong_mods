import { defineConfig } from "vite"

const AddVisualizer=false;
const EmitSourceMaps=false;

//noinspection JSUnusedGlobalSymbols
export default defineConfig({
	base: './',
	appType: 'mpa',
	build: {
		sourcemap: EmitSourceMaps,
		assetsInlineLimit(filePath) {
			if(/\/Assets\/.*\.(json|png)$/i.test(filePath.replaceAll("\\", "/"))) //json and png files in /Assets/*
				return false; //Never inline these
			return undefined; //Default behavior for everything else
		},
		rollupOptions: {
			output: {
				entryFileNames: "assets/[name].[hash].js",
				chunkFileNames: "assets/[name].[hash].js",
				assetFileNames: "assets/[name].[hash][extname]",
				manualChunks(id)
				{
					if(id.includes('crypto-js'))		return 'crypto-js';
					if(id.includes('jquery'))			return 'jquery';
					if(id.includes('LoadExtraAssets'))	return 'LoadExtraAssets';
					 									return undefined;
				}
			}
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
		alias: [
			{ find: /^jquery$/, replacement: "jquery/slim" }
		],
	},
	server: {
		fs: { strict: true },
		host:"0.0.0.0",
	},
	plugins: !AddVisualizer ? [] : [
		(await import("rollup-plugin-visualizer")).visualizer({
			open: true,
			gzipSize: true,
			brotliSize: true
		})
	]
});