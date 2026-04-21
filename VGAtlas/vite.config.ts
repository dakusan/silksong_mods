import { defineConfig } from "vite"

//noinspection JSUnusedGlobalSymbols
export default defineConfig({
	base: './',
	appType: 'mpa',
	build: {
		sourcemap: true,
		rollupOptions: {
			output: {
				manualChunks(id)
				{
					if(id.includes('crypto-js'))	return 'crypto-js';
					if(id.includes('color'))		return 'color';
					 								return undefined;
				}
			}
		}
	},
	server: {
		fs: { strict: true },
		host:"0.0.0.0",
	},
});