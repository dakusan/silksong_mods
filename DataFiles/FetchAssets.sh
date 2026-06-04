#!/usr/bin/env bash
#This script is mostly AI generated
set -euo pipefail

#------------------------------------Defaults------------------------------------
REPO_URL="https://github.com/dakusan/silksong_mods_assets.git"
BRANCH="master"
WEB_URL="https://silksong.dakusan.com"
ASSETS_DIR="../Assets"
DRY_RUN=0
UPDATE_JSON=0
MIN_JSON=0
FAILED=0

#---------------------------------Colored output---------------------------------
warn() {
	if [[ -t 2 ]]; then
		printf '\033[31m%s\033[0m\n' "$*" >&2
	else
		printf '%s\n' "$*" >&2
	fi
}

err() {
	warn "$@"
	FAILED=1
}

msg() {
	if [[ -t 1 ]]; then
		printf '\033[32m%s\033[0m\n' "$*"
	else
		printf '%s\n' "$*"
	fi
}

#--------------------------------Command line args-------------------------------
usage() {
	echo "Usage: $0 [-n] [-b branch] [-r repo_url] [-u] [-m]"
	echo
	echo "Options:"
	echo "  -n            Dry run: print filesystem-changing commands without running them"
	echo "  -b branch     Git branch to pull from (default: $BRANCH)"
	echo "  -r repo_url   Git repo URL (default: $REPO_URL)"
	echo "  -u            Download latest JSON data files from $WEB_URL after fetching assets; use -m for minified JSON"
	echo "  -m            Download latest minified JSON data files from $WEB_URL after fetching assets; implies -u"
}

while getopts ":nb:r:umh" opt; do
	case "$opt" in
		n)
			DRY_RUN=1
			;;
		b)
			BRANCH="$OPTARG"
			;;
		r)
			REPO_URL="$OPTARG"
			;;
		u)
			UPDATE_JSON=1
			;;
		m)
			UPDATE_JSON=1
			MIN_JSON=1
			;;
		h)
			usage
			exit 0
			;;
		\?)
			err "Unknown option: -$OPTARG"
			usage >&2
			exit 1
			;;
		:)
			err "Option -$OPTARG requires an argument"
			usage >&2
			exit 1
			;;
	esac
done

#---------------------------------Handle Dry Run---------------------------------
run() {
	if [[ "$DRY_RUN" == "1" ]]; then
		printf '+ %s\n' "$(printf '%q ' "$@")"
	else
		"$@"
	fi
}

#---------------------------------Initialization---------------------------------
BASE_DIR="$(pwd)"
TMP_DIR="$(mktemp -d)"
ASSETS_PATH="$BASE_DIR/$ASSETS_DIR"
ASSETS_CREATED=0

case "$REPO_URL" in
	/*|file://*|*://*|*@*:*)
		;;
	*)
		REPO_URL="$(realpath -m -- "$BASE_DIR/$REPO_URL")"
		;;
esac

if [[ ! -e "$ASSETS_PATH" && ! -L "$ASSETS_PATH" ]]; then
	run mkdir -p "$ASSETS_PATH"
	ASSETS_CREATED=1
fi

cleanup() {
	run rm -rf "$TMP_DIR"
	rmdir "$TMP_DIR" 2>/dev/null || true #Remove the temp directory, even on dry run
}

interrupt() {
	trap - INT TERM
	cleanup
	exit 130
}

trap cleanup EXIT
trap interrupt INT TERM

#---------------------Get list of symlinks and their targets---------------------
declare -a SOURCE_PATHS=()
declare -a DEST_PATHS=()

while IFS= read -r link; do
	target="$(readlink "$link")"

	case "$target" in
		"$ASSETS_DIR"/*)
			source_path="${target#"$ASSETS_DIR"/}"
			source_path="${source_path%/}"

			SOURCE_PATHS+=("$source_path")
			DEST_PATHS+=("$target")
			;;
		*)
			warn "Skipping $link -> $target"
			;;
	esac
done < <(find . -maxdepth 1 -type l -printf '%P\n' | sort)

if [[ "${#SOURCE_PATHS[@]}" == "0" ]]; then
	err "No matching symlinks found."
	exit "$FAILED"
fi

msg "$(printf 'Pulling files:\n'; printf '\t%s\n' "${SOURCE_PATHS[@]}")"
run mkdir -p "$TMP_DIR/repo"

#-------------------------Fetch the files via git archive------------------------
if git archive --remote="$REPO_URL" "$BRANCH" -- "${SOURCE_PATHS[@]}" >/dev/null 2>&1; then
	msg "Using git archive"

	if [[ "$DRY_RUN" == "1" ]]; then
		printf '+ git archive --remote=%q %q -- %s | tar -x -C %q\n' \
			"$REPO_URL" \
			"$BRANCH" \
			"$(printf '%q ' "${SOURCE_PATHS[@]}")" \
			"$TMP_DIR/repo"
	else
		git archive --remote="$REPO_URL" "$BRANCH" -- "${SOURCE_PATHS[@]}" | run tar -x -C "$TMP_DIR/repo"
	fi
else
#----------------Create a temporary sparse repo to fetch the files---------------
	warn "git archive failed; falling back to sparse checkout."

	run git -C "$TMP_DIR/repo" init --initial-branch="$BRANCH"
	run git -C "$TMP_DIR/repo" remote add origin "$REPO_URL"
	run git -C "$TMP_DIR/repo" sparse-checkout init --no-cone
	run git -C "$TMP_DIR/repo" sparse-checkout set --no-cone "${SOURCE_PATHS[@]}"
	run git -C "$TMP_DIR/repo" fetch --depth 1 --filter=blob:none origin "$BRANCH"
	run git -C "$TMP_DIR/repo" -c advice.detachedHead=false checkout FETCH_HEAD
fi

#------------------------Copy files to the local directory-----------------------
for i in "${!SOURCE_PATHS[@]}"; do
	source_path="${SOURCE_PATHS[$i]}"
	dest_path="$BASE_DIR/${DEST_PATHS[$i]}"
	local_source="$TMP_DIR/repo/$source_path"

	msg "Copying $source_path -> $dest_path"

	if [[ "$dest_path" == */ ]]; then
		run mkdir -p "$dest_path" || { err "Failed creating directory: $dest_path"; continue; }
		run cp -a "$local_source/." "$dest_path/" || { err "Failed copying: $source_path"; continue; }
	else
		run mkdir -p "$(dirname "$dest_path")" || { err "Failed creating directory: $(dirname "$dest_path")"; continue; }
		run cp -a "$local_source" "$dest_path" || { err "Failed copying: $source_path"; continue; }
	fi
done

#--------------------Attempt to grab the latest JSON versions--------------------
if [[ "$UPDATE_JSON" == "1" ]]; then
	mapfile -t JSON_FILES < <(find . -maxdepth 1 -type l -printf '%P\n' | while IFS= read -r link; do target="$(readlink "$link")"; [[ "$target" == "$ASSETS_DIR/GeneratedData/json/"*.json ]] && printf '%s\n' "$link"; done | sort)

	for filename in "${JSON_FILES[@]}"; do
		url="$WEB_URL/$filename"
		dest_file="$BASE_DIR/$(readlink "$filename")"

		if [[ "$MIN_JSON" == "1" ]]; then
			url="$url?CompactJSON=1"
		fi

		msg "Downloading latest $filename -> $dest_file"
		tmp_json="$(mktemp)"
		if run wget -nv -O "$tmp_json" "$url"; then
			run mv "$tmp_json" "$dest_file"
			[[ "$DRY_RUN" == "1" ]] && rm -f "$tmp_json"
		else
			rm -f "$tmp_json"
			err "Failed downloading: $filename"
			continue
		fi
	done
else
	warn "Warning: JSON data files downloaded from the assets repository may be out of date. See help with -h for flags -u and -m."
fi

#------------------------------Final warnings------------------------------------
if [[ "$ASSETS_CREATED" == "1" ]]; then
	if [[ "$DRY_RUN" == "1" ]]; then
		WARN_EXTRA="does not exist and would be created"
	else
		WARN_EXTRA="was created"
	fi
	warn "Warning: \"$ASSETS_DIR\" $WARN_EXTRA to hold fetched assets. You can also symlink \"REPO_ROOT/Assets\" to a local checkout of $REPO_URL."
fi

exit "$FAILED"