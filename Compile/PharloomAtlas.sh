#!/bin/bash
extra_links()
{
	compile_json "categories.json" "Categories"
	compile_json "items.json" "Items"
	compile_json "Misc.json" "Misc"
}
source ./shared
MakeZip PharloomAtlas.zip $(AddProject PharloomAtlas) $(AddProject SilkDev) "$plugin_dir/categories.json" "$plugin_dir/ItemFinder.json" "$plugin_dir/items.json" "$plugin_dir/Misc.json"