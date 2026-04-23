import { defineConfig } from "vite"

//noinspection JSUnusedGlobalSymbols
export default defineConfig({
	base: './',
	appType: 'mpa',
	build: {
//		sourcemap: true,
		rollupOptions: {
			output: {
				manualChunks(id)
				{
					if(id.includes('crypto-js'))	return 'crypto-js';
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
	server: {
		fs: { strict: true },
		host:"0.0.0.0",
	},
});