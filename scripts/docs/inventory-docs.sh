#!/bin/bash
# Documentation Inventory Script
# Generates comprehensive inventory of all markdown files in the repository

set -e

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
OUTPUT_FILE="$REPO_ROOT/docs/Documentation_Inventory.yml"

cd "$REPO_ROOT"

echo "# Documentation Inventory" > "$OUTPUT_FILE"
echo "# Generated: $(date -u +"%Y-%m-%d %H:%M:%S UTC")" >> "$OUTPUT_FILE"
echo "# Total files found: $(find . -name "*.md" -type f | grep -v node_modules | grep -v ".git" | wc -l)" >> "$OUTPUT_FILE"
echo "" >> "$OUTPUT_FILE"
echo "files:" >> "$OUTPUT_FILE"

# Find all markdown files, excluding node_modules and .git
find . -name "*.md" -type f | grep -v node_modules | grep -v ".git" | sort | while read -r file; do
    # Get file stats
    rel_path="${file#./}"
    
    # Get first line (title)
    first_line=$(head -n 1 "$file" | sed 's/^# //' | sed 's/^## //' | sed 's/^### //')
    
    # Get last modified date
    if [[ "$OSTYPE" == "darwin"* ]]; then
        mod_date=$(stat -f "%Sm" -t "%Y-%m-%d" "$file")
    else
        mod_date=$(stat -c "%y" "$file" | cut -d' ' -f1)
    fi
    
    # Classify by location and naming pattern
    category="Misc"
    if [[ "$rel_path" == PR*_IMPLEMENTATION_SUMMARY.md ]] || [[ "$rel_path" == PR*_SUMMARY.md ]]; then
        category="PR Summary"
    elif [[ "$rel_path" == *_IMPLEMENTATION*.md ]] && [[ "$rel_path" != docs/* ]]; then
        category="Implementation Summary"
    elif [[ "$rel_path" == *_SUMMARY.md ]] && [[ "$rel_path" != docs/* ]]; then
        category="Summary"
    elif [[ "$rel_path" == *_AUDIT*.md ]]; then
        category="Audit"
    elif [[ "$rel_path" == *_FIX*.md ]]; then
        category="Fix Summary"
    elif [[ "$rel_path" == *_GUIDE.md ]] || [[ "$rel_path" == *GUIDE.md ]]; then
        category="Guide"
    elif [[ "$rel_path" == README.md ]]; then
        category="README"
    elif [[ "$rel_path" == CONTRIBUTING.md ]] || [[ "$rel_path" == SECURITY.md ]] || [[ "$rel_path" == LICENSE.md ]]; then
        category="Meta"
    elif [[ "$rel_path" == docs/archive/* ]]; then
        category="Archive"
    elif [[ "$rel_path" == docs/getting-started/* ]]; then
        category="Getting Started"
    elif [[ "$rel_path" == docs/features/* ]]; then
        category="Features"
    elif [[ "$rel_path" == docs/workflows/* ]]; then
        category="Workflows"
    elif [[ "$rel_path" == docs/user-guide/* ]]; then
        category="User Guide"
    elif [[ "$rel_path" == docs/developer/* ]]; then
        category="Developer"
    elif [[ "$rel_path" == docs/api/* ]]; then
        category="API"
    elif [[ "$rel_path" == docs/security/* ]]; then
        category="Security"
    elif [[ "$rel_path" == docs/troubleshooting/* ]]; then
        category="Troubleshooting"
    elif [[ "$rel_path" == docs/architecture/* ]]; then
        category="Architecture"
    elif [[ "$rel_path" == docs/* ]]; then
        category="Docs (Other)"
    fi
    
    # Determine action based on category and location
    action="Keep"
    if [[ "$category" == "PR Summary" ]] || [[ "$category" == "Implementation Summary" ]] || [[ "$category" == "Fix Summary" ]]; then
        if [[ "$rel_path" != docs/archive/* ]]; then
            action="Archive"
        fi
    elif [[ "$category" == "Archive" ]]; then
        action="Already Archived"
    fi
    
    echo "  - path: \"$rel_path\"" >> "$OUTPUT_FILE"
    echo "    title: \"$first_line\"" >> "$OUTPUT_FILE"
    echo "    modified: \"$mod_date\"" >> "$OUTPUT_FILE"
    echo "    category: \"$category\"" >> "$OUTPUT_FILE"
    echo "    action: \"$action\"" >> "$OUTPUT_FILE"
    echo "" >> "$OUTPUT_FILE"
done

echo "Inventory complete: $OUTPUT_FILE"
