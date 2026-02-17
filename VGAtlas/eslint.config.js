import tseslint from "typescript-eslint"

export default [
	...tseslint.configs.strict, //Includes recommended
	{
		files: ["src/**/*.ts"],
		languageOptions: { //Parse type information
			parserOptions: {
				project: true,
				tsconfigRootDir: import.meta.dirname,
			},
		},
		rules: {
			"@typescript-eslint/consistent-generic-constructors": "error",	//stylistic
			"@typescript-eslint/array-type": "error",						//stylistic
			"@typescript-eslint/prefer-for-of": "error",					//stylistic
			"@typescript-eslint/prefer-optional-chain": "error",			//stylistic - requires type information
			"@typescript-eslint/promise-function-async": "error",			//requires type information

			"@typescript-eslint/no-non-null-assertion": "off",				//strict
			"@typescript-eslint/no-this-alias": "off",						//recommended
			"@typescript-eslint/no-namespace": "off",						//recommended

			"@typescript-eslint/no-unused-vars": [
				"error",
				{
					argsIgnorePattern: "^_",
					varsIgnorePattern: "^_",
					caughtErrorsIgnorePattern: "^_",
				},
			],
			"@typescript-eslint/explicit-member-accessibility": [
				"error",
				{
					accessibility: "explicit",
					overrides: {
						constructors: "off",
					},
				},
			],
			"@typescript-eslint/method-signature-style": [
				"error", "method",
			],
			"@typescript-eslint/naming-convention": [
				"error",
				{
					selector: "method",
					modifiers: ["override"],
					format: null,
				},
				{
					selector: "default",
					format:null,
					filter: {
						regex: "^(forEach|toArray)$",
						match: false,
					},
					custom: {
						regex: "^_?[a-z].*[A-Z].*$",
						match: false,
					},
				},
			],
		},
	},
];