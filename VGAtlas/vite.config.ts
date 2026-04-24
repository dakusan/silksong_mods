import { defineConfig } from "vite"

const AddVisualizer=false;

//noinspection JSUnusedGlobalSymbols
export default defineConfig({
	base: './',
	appType: 'mpa',
	build: {
//		sourcemap: true,
		rollupOptions: {
			output: {
				entryFileNames: "assets/[name].[hash].js",
				chunkFileNames: "assets/[name].[hash].js",
				assetFileNames: "assets/[name].[hash][extname]",
				manualChunks(id)
				{
					if(id.includes('crypto-js'))	return 'crypto-js';
					if(id.includes('jquery'))		return 'jquery';
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