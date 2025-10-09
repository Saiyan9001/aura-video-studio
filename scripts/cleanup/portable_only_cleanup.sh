#!/bin/bash
# Portable-Only Cleanup Script (Linux/macOS)
# Removes all MSIX/EXE packaging artifacts and related files
# Run this script to enforce portable-only distribution policy

set -e

WHATIF=false

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --whatif)
            WHATIF=true
            shift
            ;;
        *)
            echo "Unknown option: $1"
            echo "Usage: $0 [--whatif]"
            exit 1
            ;;
    esac
done

echo "=== Aura Video Studio - Portable-Only Cleanup ==="
echo ""

if [ "$WHATIF" = true ]; then
    echo "Running in WhatIf mode - no files will be deleted"
    echo ""
fi

SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
ROOT_DIR="$(dirname "$(dirname "$SCRIPT_DIR")")"
DELETED_COUNT=0
PATTERNS=()

# Define patterns to search and delete
SEARCH_PATTERNS=(
    "*msix*"
    "*inno*"
    "*setup.iss"
    "*installer*"
    "*.appx*"
    "*.msixbundle*"
    "*.cer"
)

# Search in scripts/packaging/
echo "Searching in scripts/packaging/..."
PACKAGING_DIR="$ROOT_DIR/scripts/packaging"
if [ -d "$PACKAGING_DIR" ]; then
    for pattern in "${SEARCH_PATTERNS[@]}"; do
        while IFS= read -r -d '' file; do
            RELATIVE_PATH="${file#$ROOT_DIR/}"
            echo "  Found: $RELATIVE_PATH"
            PATTERNS+=("$RELATIVE_PATH")
            
            if [ "$WHATIF" = false ]; then
                rm -f "$file"
                DELETED_COUNT=$((DELETED_COUNT + 1))
                echo "  Deleted: $RELATIVE_PATH"
            fi
        done < <(find "$PACKAGING_DIR" -type f -iname "$pattern" -print0 2>/dev/null)
    done
fi

# Search in root directory for artifacts
echo ""
echo "Searching in root directory..."
for pattern in "${SEARCH_PATTERNS[@]}"; do
    while IFS= read -r -d '' file; do
        RELATIVE_PATH="${file#$ROOT_DIR/}"
        echo "  Found: $RELATIVE_PATH"
        PATTERNS+=("$RELATIVE_PATH")
        
        if [ "$WHATIF" = false ]; then
            rm -f "$file"
            DELETED_COUNT=$((DELETED_COUNT + 1))
            echo "  Deleted: $RELATIVE_PATH"
        fi
    done < <(find "$ROOT_DIR" -maxdepth 1 -type f -iname "$pattern" -print0 2>/dev/null)
done

# Remove workflows that reference MSIX/EXE
echo ""
echo "Checking GitHub workflows..."
WORKFLOW_DIR="$ROOT_DIR/.github/workflows"
if [ -d "$WORKFLOW_DIR" ]; then
    WORKFLOWS_TO_CHECK=("ci.yml" "ci-windows.yml")
    
    for workflow in "${WORKFLOWS_TO_CHECK[@]}"; do
        WORKFLOW_PATH="$WORKFLOW_DIR/$workflow"
        if [ -f "$WORKFLOW_PATH" ]; then
            if grep -qi "msix\|MSIX\|AppxBundle\|UapAppx" "$WORKFLOW_PATH"; then
                RELATIVE_PATH="${WORKFLOW_PATH#$ROOT_DIR/}"
                echo "  Found MSIX/EXE references in: $RELATIVE_PATH"
                PATTERNS+=("$RELATIVE_PATH")
                
                if [ "$WHATIF" = false ]; then
                    rm -f "$WORKFLOW_PATH"
                    DELETED_COUNT=$((DELETED_COUNT + 1))
                    echo "  Deleted: $RELATIVE_PATH"
                fi
            fi
        fi
    done
fi

echo ""
if [ "$WHATIF" = true ]; then
    echo "=== WhatIf Mode: No files were deleted ==="
    echo "Found ${#PATTERNS[@]} files/patterns that would be deleted"
else
    echo "=== Cleanup Complete ==="
    echo "Deleted $DELETED_COUNT files"
fi

echo ""
echo "Files matching MSIX/EXE patterns:"
if [ ${#PATTERNS[@]} -eq 0 ]; then
    echo "  None found - repository is clean!"
else
    for pattern in "${PATTERNS[@]}"; do
        echo "  - $pattern"
    done
fi

echo ""
echo "Distribution Policy: Portable-only ZIP distribution"
echo "See scripts/packaging/build-portable.ps1 for building portable distribution"
echo ""
