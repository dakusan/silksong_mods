<html>
<head>
	<title>Chain System: Dependency Graph</title>
	<link rel="stylesheet" href="DGraph.css">
</head>
<body>
<?php
require_once(__DIR__.'/Shared.php');
$Items=[];
foreach(Query('
SELECT
	I.ID, I.Title, IFNULL(I.IconID, C.IconID) AS IconID,
	GROUP_CONCAT(CONCAT(IF(ReI.FlagStarted, "~", ""), IF(ReI.FlagNot, "!", ""), IF(ReI.StaticLinkID IS NULL, ReI.ItemID, ReI.StaticLinkID)) SEPARATOR ", ") AS Reqs,
	GROUP_CONCAT(CONCAT(IF(NeI.FlagStarted, "~", ""), IF(NeI.FlagNot, "!", ""), IF(NeI.StaticLinkID IS NULL, NeI.ItemID, NeI.StaticLinkID)) SEPARATOR ", ") AS Needs,
	GROUP_CONCAT(CONCAT(IF(RwI.FlagStarted, "~", ""), IF(RwI.FlagNot, "!", ""), IF(RwI.StaticLinkID IS NULL, RwI.ItemID, RwI.StaticLinkID)) SEPARATOR ", ") AS Rewards
FROM Items AS I
INNER JOIN Categories AS C ON C.ID=I.CategoryID
LEFT JOIN ItemLinkDefs AS ReI ON ReI.SetID=I.ReqsSetID     AND ReI.Name IS NULL
LEFT JOIN ItemLinkDefs AS NeI ON NeI.SetID=I.NeedsSetID    AND NeI.Name IS NULL
LEFT JOIN ItemLinkDefs AS RwI ON RwI.SetID=I.RewardsSetID  AND RwI.Name IS NULL
GROUP BY I.ID, I.ReqsSetID, I.NeedsSetID, I.RewardsSetID
HAVING Reqs IS NOT NULL OR Needs IS NOT NULL OR Rewards IS NOT NULL
ORDER BY ID ASC') as $Row) {
	FixList($Row, 'Reqs');
	FixList($Row, 'Needs');
	FixList($Row, 'Rewards');
	$Items[$Row->ID]=(array)$Row;
}

$StaticLinks=[];
foreach(Query('SELECT ID, Name, Special FROM StaticLinks ORDER BY ID Asc') as $Row)
	$StaticLinks[$Row->ID]=(array)$Row;

function FixList($Row, $FieldName)
{
	if(!isset($Row->$FieldName))
		unset($Row->$FieldName);
	else
		$Row->$FieldName=explode(', ', $Row->$FieldName);
}

//***************************************************IMPORTANT NOTE: THE BELOW CODE IS 100% GROK GENERATED AND HAS NOT BEEN CHECKED. I JUST NEEDED IT FOR A SCREENSHOT***************************************************

// Unified nodes
$nodes = [];
foreach ($StaticLinks as $id => $data) {
	if ($data['Special']) continue;
	$nodes[$id] = ['Title' => $data['Name'], 'IconID' => null, 'Reqs' => [], 'Needs' => [], 'Rewards' => []];
}
foreach ($Items as $id => $item) {
	$nodes[$id] = ['Title' => $item['Title'], 'IconID' => $item['IconID'], 'Reqs' => $item['Reqs'] ?? [], 'Needs' => $item['Needs'] ?? [], 'Rewards' => $item['Rewards'] ?? []];
}

// Build graph
$graph = array_fill_keys(array_keys($nodes), []);
$reverse_graph = $graph;
$edge_info = [];
$extra_parents = $graph;
foreach ($nodes as $id => $data) {
	foreach (['Reqs' => 'req', 'Needs' => 'need'] as $field => $type) {
		foreach ($data[$field] as $link) {
			$orig_link = $link;
			$flag = '';
			if (strpos($link, '!') === 0) {
				$flag = '!';
				$link = substr($link, 1);
			} elseif (strpos($link, '~') === 0) {
				$flag = '~';
				$link = substr($link, 1);
			}
			$parent_id = (int)$link;
			if (!isset($nodes[$parent_id])) continue;
			if ($flag === '!') {
				$extra_parents[$id][] = ['id' => $parent_id, 'type' => $type, 'flag' => $flag];
			} else {
				if (!in_array($id, $graph[$parent_id])) $graph[$parent_id][] = $id;
				if (!in_array($parent_id, $reverse_graph[$id])) $reverse_graph[$id][] = $parent_id;
				$edge_info[$parent_id . '-' . $id] = ['type' => $type, 'flag' => $flag];
			}
		}
	}
	foreach ($data['Rewards'] ?? [] as $link) {
		$child_id = (int)$link;
		if (!isset($nodes[$child_id])) continue;
		if (!in_array($child_id, $graph[$id])) $graph[$id][] = $child_id;
		if (!in_array($id, $reverse_graph[$child_id])) $reverse_graph[$child_id][] = $id;
		$edge_info[$id . '-' . $child_id] = ['type' => 'reward', 'flag' => ''];
	}
}

// Indegree
$indegree = array_fill_keys(array_keys($nodes), 0);
foreach ($graph as $p => $cs) {
	foreach ($cs as $c) $indegree[$c]++;
}

// Roots
$roots = [];
foreach ($indegree as $n => $d) {
	if ($d == 0) $roots[] = $n;
}

// Max depth
$max_depth = array_fill_keys(array_keys($nodes), 0);
$queue = new SplQueue();
foreach ($roots as $r) $queue->enqueue($r);
$processed = 0;
$temp_indegree = $indegree; // copy for processing
while (!$queue->isEmpty()) {
	$node = $queue->dequeue();
	$processed++;
	foreach ($graph[$node] ?? [] as $child) {
		$max_depth[$child] = max($max_depth[$child], $max_depth[$node] + 1);
		$temp_indegree[$child]--;
		if ($temp_indegree[$child] == 0) $queue->enqueue($child);
	}
}
if ($processed < count($nodes)) {
	echo "Cycle detected, stopping tree at cycles.\n";
}

// Primary parents
$type_prior = ['req' => 2, 'need' => 1, 'reward' => 0];
$primary_parent = array_fill_keys(array_keys($nodes), null);
foreach ($reverse_graph as $node => $parents) {
	if (empty($parents)) continue;
	usort($parents, function($a, $b) use ($max_depth, $type_prior, $edge_info, $node) {
		$da = $max_depth[$a]; $db = $max_depth[$b];
		if ($da != $db) return $db <=> $da;
		$ta = $type_prior[$edge_info[$a . '-' . $node]['type']] ?? 0;
		$tb = $type_prior[$edge_info[$b . '-' . $node]['type']] ?? 0;
		if ($ta != $tb) return $tb <=> $ta;
		return $a <=> $b;
	});
	$primary_parent[$node] = $parents[0];
}

// Components with union-find
$parent_uf = [];
foreach ($nodes as $id => $_) $parent_uf[$id] = $id;
function find_uf(&$parent_uf, $x) {
	if ($parent_uf[$x] != $x) $parent_uf[$x] = find_uf($parent_uf, $parent_uf[$x]);
	return $parent_uf[$x];
}
function union_uf(&$parent_uf, $x, $y) {
	$px = find_uf($parent_uf, $x); $py = find_uf($parent_uf, $y);
	if ($px != $py) $parent_uf[$px] = $py;
}
foreach ($graph as $p => $cs) {
	foreach ($cs as $c) union_uf($parent_uf, $p, $c);
}
$components = [];
foreach ($nodes as $id => $_) {
	$r = find_uf($parent_uf, $id);
	$components[$r][] = $id;
}

// Comp list sorted by size desc
$comp_sizes = [];
foreach ($components as $r => $list) {
	$size = count($list);
	if ($size == 1 && empty($graph[$list[0]]) && empty($reverse_graph[$list[0]])) continue;
	$comp_sizes[] = ['comp_root' => $r, 'size' => $size];
}
usort($comp_sizes, function($a, $b) {
	return $b['size'] <=> $a['size'];
});

// Render
echo '<div class=OrgTag><pre>';
$subtree_shown = [];
function print_tree($node, &$subtree_shown, &$nodes, &$graph, &$max_depth, &$primary_parent, &$reverse_graph, &$extra_parents, &$edge_info, $prefix = '', $is_last = true, $current_parent = null) {
	if (isset($subtree_shown[$node])) {
		// Show minimal if already shown
		$branch = $is_last ? '└─ ' : '├─ ';
		if ($current_parent === null) $branch = '';
		$line_class = '';
		$link_icon = '';
		if ($current_parent !== null) {
			$ei = $edge_info[$current_parent . '-' . $node] ?? null;
			if ($ei) {
				$t = $ei['type'];
				$line_class = $t == 'req' ? 'RequiredLink' : ($t == 'need' ? 'NeedLink' : 'RewardLink');
				if ($ei['flag'] == '~') $link_icon = '<span class="StartedS"></span>';
			}
		}
		$line = $branch . $link_icon;
		if ($line_class) $line = '<span class="' . $line_class . '">' . $line . '</span>';
		$icon_div = '';
		if (isset($nodes[$node]['IconID']) && $nodes[$node]['IconID'] !== null) {
			$icon_div = '<div class="Icon I' . sprintf("%02d", $nodes[$node]['IconID']) . '"></div> ';
		}
		echo $prefix . $line . $icon_div . htmlspecialchars($nodes[$node]['Title']) . ' <a href="#node_' . $node . '" class="SubtreeLink"></a>' . "\n";
		return;
	}
	$subtree_shown[$node] = true;
	$branch = $is_last ? '└─ ' : '├─ ';
	if ($current_parent === null) $branch = '';
	$line_class = '';
	$link_icon = '';
	if ($current_parent !== null) {
		$ei = $edge_info[$current_parent . '-' . $node] ?? null;
		if ($ei) {
			$t = $ei['type'];
			$line_class = $t == 'req' ? 'RequiredLink' : ($t == 'need' ? 'NeedLink' : 'RewardLink');
			if ($ei['flag'] == '~') $link_icon = '<span class="StartedS"></span>';
		}
	}
	$line = $branch . $link_icon;
	if ($line_class) $line = '<span class="' . $line_class . '">' . $line . '</span>';
	$icon_div = '';
	if (isset($nodes[$node]['IconID']) && $nodes[$node]['IconID'] !== null) {
		$icon_div = '<div class="Icon I' . sprintf("%02d", $nodes[$node]['IconID']) . '"></div> ';
	}
	echo $prefix . $line . '<a id="node_' . $node . '"></a>' . $icon_div . htmlspecialchars($nodes[$node]['Title']);
	// Brackets for other acquirements
	$all_parents = $reverse_graph[$node] ?? [];
	$ex = $extra_parents[$node] ?? [];
	foreach ($ex as $e) {
		if (!in_array($e['id'], $all_parents)) $all_parents[] = $e['id'];
	}
	$other_p = [];
	foreach ($all_parents as $p) {
		if ($p != $current_parent) $other_p[] = $p;
	}
	if (!empty($other_p)) {
		sort($other_p);
		echo ' <span class="Bracket">[</span>';
		$first = true;
		foreach ($other_p as $p) {
			if (!$first) echo ', ';
			$first = false;
			$flag = '';
			$ei = $edge_info[$p . '-' . $node] ?? null;
			if ($ei) $flag = $ei['flag'];
			else {
				foreach ($ex as $e) {
					if ($e['id'] == $p) $flag = $e['flag'];
				}
			}
			$col_class = $flag == '!' ? 'NotFlag' : ($flag == '~' ? 'StartedFlag' : 'NormalFlag');
			$p_icon = $flag == '~' ? '<span class="StartedS"></span>' : '';
			echo $p_icon . '<span class="' . $col_class . '">' . htmlspecialchars($nodes[$p]['Title']) . '</span>';
		}
		echo '<span class="Bracket">]</span>';
	}
	echo "\n";
	$children = $graph[$node] ?? [];
	usort($children, function($a, $b) use ($node, $primary_parent, $max_depth) {
		$pa = ($primary_parent[$a] ?? null) == $node ? 1 : 0;
		$pb = ($primary_parent[$b] ?? null) == $node ? 1 : 0;
		if ($pa != $pb) return $pb <=> $pa;
		$da = $max_depth[$a]; $db = $max_depth[$b];
		if ($da != $db) return $db <=> $da;
		return $a <=> $b;
	});
	$num_child = count($children);
	for ($i = 0; $i < $num_child; $i++) {
		$child_last = ($i == $num_child - 1);
		$new_prefix = $prefix . ($current_parent === null ? '' : ($is_last ? '   ' : '│  '));
		print_tree($children[$i], $subtree_shown, $nodes, $graph, $max_depth, $primary_parent, $reverse_graph, $extra_parents, $edge_info, $new_prefix, $child_last, $node);
	}
}
// Render each component's trees
foreach ($comp_sizes as $cs) {
	$comp_root = $cs['comp_root'];
	$comp_nodes = $components[$comp_root];
	$comp_roots = [];
	foreach ($comp_nodes as $n) {
		if ($indegree[$n] == 0) $comp_roots[] = $n;
	}
	usort($comp_roots, function($a, $b) use ($max_depth) {
		$da = $max_depth[$a]; $db = $max_depth[$b];
		if ($da != $db) return $db <=> $da;
		return $a <=> $b;
	});
	foreach ($comp_roots as $r) {
		print_tree($r, $subtree_shown, $nodes, $graph, $max_depth, $primary_parent, $reverse_graph, $extra_parents, $edge_info);
	}
}
echo '</pre></div>';
?>
</body>
</html>