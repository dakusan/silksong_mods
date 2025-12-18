#!/bin/bash
source ./shared
MakeZip PharloomAtlas.zip $(AddProject PharloomAtlas) $(AddProject SilkDev) "$plugin_dir/categories.json" "$plugin_dir/ItemFinder.json" "$plugin_dir/items.json" "$plugin_dir/Misc.json"