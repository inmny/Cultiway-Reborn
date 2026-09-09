#!/usr/bin/env bash
set -euo pipefail

MODE="${1:-all}"
CULTIWAY_ROOT="Source"
VANILLA_ROOT=".GameSource/Assets/Scripts/Assembly-CSharp"
TMP_DIR=$(mktemp -d)
trap 'rm -rf "$TMP_DIR"' EXIT

count_files() {
    awk 'NF { count++ } END { print count + 0 }' "$1"
}

count_lines() {
    if [[ ! -s "$1" ]]; then
        printf '0\n'
        return
    fi

    tr '\n' '\0' < "$1" \
        | xargs -0 wc -l \
        | awk '!/ total$/ { lines += $1 } END { print lines + 0 }'
}

percentage() {
    awk -v part="$1" -v total="$2" 'BEGIN { if (total == 0) print "0.00"; else printf "%.2f", part * 100 / total }'
}

print_header() {
    printf 'project\tlayer\tfiles\tlines\tpct_full\n'
}

print_layers() {
    local project="$1"
    local full_list="$2"
    local bulk_list="$3"
    local bulk_label="$4"
    local full_files full_lines bulk_files bulk_lines logic_files logic_lines

    full_files=$(count_files "$full_list")
    full_lines=$(count_lines "$full_list")
    bulk_files=$(count_files "$bulk_list")
    bulk_lines=$(count_lines "$bulk_list")
    logic_files=$((full_files - bulk_files))
    logic_lines=$((full_lines - bulk_lines))

    printf '%s\tFULL\t%s\t%s\t100.00\n' "$project" "$full_files" "$full_lines"
    printf '%s\t%s\t%s\t%s\t%s\n' "$project" "$bulk_label" "$bulk_files" "$bulk_lines" "$(percentage "$bulk_lines" "$full_lines")"
    printf '%s\tLOGIC\t%s\t%s\t%s\n' "$project" "$logic_files" "$logic_lines" "$(percentage "$logic_lines" "$full_lines")"
}

build_cultiway_lists() {
    local full_list="$TMP_DIR/cultiway-full.txt"
    local main_list="$TMP_DIR/cultiway-main.txt"
    local partial_list="$TMP_DIR/cultiway-partial.txt"
    local bulk_list="$TMP_DIR/cultiway-bulk.txt"
    local names pattern

    find "$CULTIWAY_ROOT" -type f -name '*.cs' -print | sort -u > "$full_list"

    rg -l 'class\s+\w+[^{]*:\s*[^{]*\bExtendLibrary\b' "$CULTIWAY_ROOT" -g '*.cs' 2>/dev/null \
        | tr '\\' '/' | tr -d '\r' | sort -u > "$main_list" || true

    names=$(rg -n 'class\s+\w+[^{]*:\s*[^{]*\bExtendLibrary\b' "$CULTIWAY_ROOT" -g '*.cs' 2>/dev/null \
        | sed -E 's/.*class[[:space:]]+([A-Za-z0-9_]+).*/\1/' | sort -u || true)

    : > "$partial_list"
    if [[ -n "$names" ]]; then
        pattern=$(printf '%s\n' "$names" | paste -sd'|' -)
        rg -l "partial\s+class\s+($pattern)\b" "$CULTIWAY_ROOT" -g '*.cs' 2>/dev/null \
            | tr '\\' '/' | tr -d '\r' | sort -u > "$partial_list" || true
    fi

    cat "$main_list" "$partial_list" | sort -u > "$bulk_list"
    printf '%s\n%s\n' "$full_list" "$bulk_list"
}

build_vanilla_lists() {
    local full_list="$TMP_DIR/vanilla-full.txt"
    local bulk_list="$TMP_DIR/vanilla-bulk.txt"

    find "$VANILLA_ROOT" -type f -name '*.cs' -print | sort -u > "$full_list"
    find "$VANILLA_ROOT" -type f -name '*Library.cs' -print | sort -u > "$bulk_list"
    printf '%s\n%s\n' "$full_list" "$bulk_list"
}

print_cultiway() {
    if [[ ! -d "$CULTIWAY_ROOT" ]]; then
        printf 'Cultiway source not found: %s\n' "$CULTIWAY_ROOT" >&2
        return 1
    fi

    local lists full_list bulk_list
    lists=$(build_cultiway_lists)
    full_list=$(printf '%s\n' "$lists" | sed -n '1p')
    bulk_list=$(printf '%s\n' "$lists" | sed -n '2p')
    print_layers 'Cultiway' "$full_list" "$bulk_list" 'EXTEND_LIBRARY'
}

print_vanilla() {
    if [[ ! -d "$VANILLA_ROOT" ]]; then
        printf 'Vanilla source not found: %s\n' "$VANILLA_ROOT" >&2
        return 1
    fi

    local lists full_list bulk_list
    lists=$(build_vanilla_lists)
    full_list=$(printf '%s\n' "$lists" | sed -n '1p')
    bulk_list=$(printf '%s\n' "$lists" | sed -n '2p')
    print_layers 'WorldBox' "$full_list" "$bulk_list" 'LIBRARY'
}

print_dirs() {
    if [[ ! -d "$CULTIWAY_ROOT" ]]; then
        printf 'Cultiway source not found: %s\n' "$CULTIWAY_ROOT" >&2
        return 1
    fi

    printf 'directory\tfiles\tlines\n'
    find "$CULTIWAY_ROOT" -mindepth 1 -maxdepth 1 -print | sort | while IFS= read -r entry; do
        local list="$TMP_DIR/dir-list.txt"
        if [[ -d "$entry" ]]; then
            find "$entry" -type f -name '*.cs' -print | sort -u > "$list"
        elif [[ "$entry" == *.cs ]]; then
            printf '%s\n' "$entry" > "$list"
        else
            continue
        fi

        printf '%s\t%s\t%s\n' "${entry#Source/}" "$(count_files "$list")" "$(count_lines "$list")"
    done
}

case "$MODE" in
    all)
        print_header
        print_cultiway
        print_vanilla
        ;;
    cultiway)
        print_header
        print_cultiway
        ;;
    vanilla)
        print_header
        print_vanilla
        ;;
    dirs)
        print_dirs
        ;;
    *)
        printf 'Usage: %s [all|cultiway|vanilla|dirs]\n' "$0" >&2
        exit 2
        ;;
esac
