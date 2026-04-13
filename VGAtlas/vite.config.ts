import { defineConfig } from "vite"

export default defineConfig({
	base: './',
	appType: 'mpa',
	build: {
		sourcemap: true,
	},
	server: {
		fs: { strict: true },
		host:"0.0.0.0",
	},
});