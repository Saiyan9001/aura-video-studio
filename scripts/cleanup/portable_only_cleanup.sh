#!/bin/bash
# Portable-Only Cleanup Script
# Removes all MSIX/EXE packaging files and artifacts from the repository

set -e

DRY_RUN=false
VERBOSE=false

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --dry-run)
            DRY_RUN=true
            shift
            ;;
        --verbose)
            VERBOSE=true
            shift
            ;;
        *)
            echo "Unknown option: $1"
            echo "Usage: $0 [--dry-run] [--verbose]"
            exit 1
            ;;
    esac
done

echo ""
echo "========================================"
echo " Aura Video Studio - Portable Only Cleanup"
echo "========================================"
echo ""

if [ "$DRY_RUN" = true ]; then
    echo "DRY RUN MODE - No files will be deleted"
    echo ""
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"
DELETED_COUNT=0
ERROR_COUNT=0

remove_item_safely() {
    local path="$1"
    local description="$2"
    
    if [ -e "$path" ]; then
        if [ "$DRY_RUN" = true ]; then
            echo "  [DRY RUN] Would delete: $description"
            if [ "$VERBOSE" = true ]; then
                echo "            Path: $path"
            fi
        else
            if rm -rf "$path" 2>/dev/null; then
                echo "  ✓ Deleted: $description"
                if [ "$VERBOSE" = true ]; then
                    echo "            Path: $path"
                fi
                ((DELETED_COUNT++))
            else
                echo "  ✗ Failed to delete: $description"
                echo "    Error: Permission denied or file in use"
                ((ERROR_COUNT++))
            fi
        fi
    else
        if [ "$VERBOSE" = true ]; then
            echo "  ⊘ Not found: $description"
            echo "            Path: $path"
        fi
    fi
}

echo "[1/4] Cleaning up packaging scripts..."

# Remove Inno Setup installer script
remove_item_safely "$ROOT_DIR/scripts/packaging/setup.iss" \
    "Inno Setup installer script (setup.iss)"

# Remove SBOM generation script
remove_item_safely "$ROOT_DIR/scripts/packaging/generate-sbom.ps1" \
    "SBOM generation script (generate-sbom.ps1)"

# Check for any remaining MSIX/installer related files in scripts/packaging
if [ -d "$ROOT_DIR/scripts/packaging" ]; then
    while IFS= read -r -d '' file; do
        remove_item_safely "$file" "MSIX-related file: $(basename "$file")"
    done < <(find "$ROOT_DIR/scripts/packaging" -maxdepth 1 -type f -name "*msix*" -print0 2>/dev/null)
    
    while IFS= read -r -d '' file; do
        remove_item_safely "$file" "Inno Setup file: $(basename "$file")"
    done < <(find "$ROOT_DIR/scripts/packaging" -maxdepth 1 -type f -name "*inno*" -print0 2>/dev/null)
    
    while IFS= read -r -d '' file; do
        filename=$(basename "$file")
        if [ "$filename" != "setup.iss" ]; then
            remove_item_safely "$file" "Setup-related file: $filename"
        fi
    done < <(find "$ROOT_DIR/scripts/packaging" -maxdepth 1 -type f -name "*setup*" -print0 2>/dev/null)
    
    while IFS= read -r -d '' file; do
        remove_item_safely "$file" "Installer-related file: $(basename "$file")"
    done < <(find "$ROOT_DIR/scripts/packaging" -maxdepth 1 -type f -name "*installer*" -print0 2>/dev/null)
fi

echo ""
echo "[2/4] Cleaning up MSIX artifacts..."

# Remove Package.appxmanifest from Aura.App
remove_item_safely "$ROOT_DIR/Aura.App/Package.appxmanifest" \
    "WinUI 3 package manifest (Package.appxmanifest)"

# Remove any .cer files
while IFS= read -r -d '' file; do
    remove_item_safely "$file" "Certificate file: $(basename "$file")"
done < <(find "$ROOT_DIR" -maxdepth 1 -type f -name "*.cer" -print0 2>/dev/null)

# Remove any .appx* files
while IFS= read -r -d '' file; do
    remove_item_safely "$file" "APPX file: $(basename "$file")"
done < <(find "$ROOT_DIR" -maxdepth 1 -type f -name "*.appx*" -print0 2>/dev/null)

# Remove any .msix* files
while IFS= read -r -d '' file; do
    remove_item_safely "$file" "MSIX file: $(basename "$file")"
done < <(find "$ROOT_DIR" -maxdepth 1 -type f -name "*.msix*" -print0 2>/dev/null)

# Remove any .msixbundle files
while IFS= read -r -d '' file; do
    remove_item_safely "$file" "MSIX bundle file: $(basename "$file")"
done < <(find "$ROOT_DIR" -maxdepth 1 -type f -name "*.msixbundle" -print0 2>/dev/null)

echo ""
echo "[3/4] Cleaning up GitHub workflows..."

# Remove ci-windows.yml (builds MSIX)
remove_item_safely "$ROOT_DIR/.github/workflows/ci-windows.yml" \
    "Windows CI workflow (ci-windows.yml)"

echo ""
echo "[4/4] Cleaning up artifacts directories..."

# Remove artifacts directories
if [ -d "$ROOT_DIR/artifacts" ]; then
    remove_item_safely "$ROOT_DIR/artifacts/windows/msix" \
        "MSIX artifacts directory"
    remove_item_safely "$ROOT_DIR/artifacts/windows/exe" \
        "EXE artifacts directory"
fi

echo ""
echo "========================================"
echo " Cleanup Complete"
echo "========================================"
echo ""

if [ "$DRY_RUN" = true ]; then
    echo "DRY RUN SUMMARY:"
    echo "  No files were actually deleted"
else
    echo "SUMMARY:"
    echo "  Files deleted: $DELETED_COUNT"
    if [ $ERROR_COUNT -gt 0 ]; then
        echo "  Errors encountered: $ERROR_COUNT"
    fi
fi
echo ""
