#!/bin/bash
# Archive Historical Documentation Script
# Moves PR summaries, implementation docs, and fix summaries to docs/archive/

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
ARCHIVE_DIR="$REPO_ROOT/docs/archive"

cd "$REPO_ROOT"

echo "Archiving historical documentation..."
echo "Target directory: $ARCHIVE_DIR"

# Create archive directory if it doesn't exist
mkdir -p "$ARCHIVE_DIR"

# Count total files to archive
total_files=0

# Function to archive a file
archive_file() {
    local file="$1"
    local basename=$(basename "$file")
    
    # Skip if already in archive
    if [[ "$file" == *"/archive/"* ]]; then
        return 0
    fi
    
    # Skip if target already exists in archive
    if [ -f "$ARCHIVE_DIR/$basename" ]; then
        echo "  SKIP: $basename (already in archive)"
        return 0
    fi
    
    echo "  Moving: $basename"
    
    # Add archived banner to the file
    temp_file=$(mktemp)
    {
        echo "> **⚠️ ARCHIVED DOCUMENT**"
        echo ">"
        echo "> This document is archived for historical reference only."
        echo "> It may contain outdated information. See the [Documentation Index](../DocsIndex.md) for current documentation."
        echo ""
        cat "$file"
    } > "$temp_file"
    
    # Move to archive with banner
    cp "$temp_file" "$ARCHIVE_DIR/$basename" && rm "$temp_file"
    
    # Remove original
    [ -f "$file" ] && rm "$file"
    
    total_files=$((total_files + 1))
    return 0
}

echo ""
echo "=== Phase 1: PR Implementation Summaries ==="
for file in PR*_IMPLEMENTATION_SUMMARY.md; do
    if [ -f "$file" ]; then
        archive_file "$file"
    fi
done

echo ""
echo "=== Phase 2: PR Summaries ==="
for file in PR*_SUMMARY.md PR*_COMPLETION_SUMMARY.md PR_*.md; do
    if [ -f "$file" ]; then
        archive_file "$file"
    fi
done

echo ""
echo "=== Phase 3: Implementation Documents ==="
for file in *_IMPLEMENTATION.md *_IMPLEMENTATION_*.md IMPLEMENTATION_COMPLETE.md; do
    if [ -f "$file" ]; then
        # Skip LLM_IMPLEMENTATION_GUIDE.md - it's a guide, not an implementation summary
        if [[ "$file" == "LLM_IMPLEMENTATION_GUIDE.md" ]]; then
            continue
        fi
        archive_file "$file"
    fi
done

echo ""
echo "=== Phase 4: Fix Summaries ==="
for file in *_FIX_SUMMARY.md *_FIX.md FIX_SUMMARY.md; do
    if [ -f "$file" ]; then
        archive_file "$file"
    fi
done

echo ""
echo "=== Phase 5: Other Summaries ==="
for file in *_SUMMARY.md; do
    if [ -f "$file" ]; then
        archive_file "$file"
    fi
done

echo ""
echo "=== Phase 6: Audit Reports ==="
for file in *_AUDIT*.md; do
    if [ -f "$file" ]; then
        archive_file "$file"
    fi
done

echo ""
echo "=== Phase 7: Visual Guides (Implementation-Specific) ==="
for file in *_VISUAL*.md BUILD_OPTIMIZATION_BEFORE_AFTER.md SKIP_BUG_VISUAL.md; do
    if [ -f "$file" ]; then
        # Keep user-facing visual guides
        if [[ "$file" == "PATH_SELECTOR_VISUAL_GUIDE.md" ]] || [[ "$file" == "LOADING_STATES_VISUAL_GUIDE.md" ]] || [[ "$file" == "VISUAL_IMPACT_EXAMPLES.md" ]]; then
            echo "  KEEP: $file (user-facing guide)"
            continue
        fi
        archive_file "$file"
    fi
done

echo ""
echo "=== Summary ==="
echo "Total files archived: $total_files"
echo ""
echo "Files remaining in root (will be reviewed):"
find . -maxdepth 1 -name "*.md" -type f | wc -l
