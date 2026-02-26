#!/bin/bash
extra_links()
{
	compile_json "Categories.json" "Categories"
	compile_json "Items.json" "Items"
	compile_json "Misc.json" "Misc"
	compile_json "ItemFinder.json" "ItemFinder"
}
source ./shared
MakeZip PharloomAtlas.zip $(AddProject PharloomAtlas) $(AddProject SilkDev) "$plugin_dir/Categories.json" "$plugin_dir/ItemFinder.json" "$plugin_dir/Items.json" "$plugin_dir/Misc.json"