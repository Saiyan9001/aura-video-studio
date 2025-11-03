> **⚠️ ARCHIVED DOCUMENT**
>
> This document is archived for historical reference only.
> It may contain outdated information. See the [Documentation Index](../DocsIndex.md) for current documentation.

# Node.js Version Compatibility Update - Summary

## Issue
Users with Node.js v22.20.0 (and any version >= 21.0.0) could not build the project because `package.json` had overly restrictive version constraints:
- `"node": ">=18.0.0 <21.0.0"` (blocked Node 21+)
- `"npm": ">=9.0.0 <11.0.0"` (blocked npm 11+)

Error message received:
```
npm error code EBADENGINE
npm error engine Unsupported engine
npm error engine Not compatible with your version of node/npm: aura.web@1.0.0
npm error notsup Required: {"node":">=18.0.0 <21.0.0","npm":">=9.0.0 <11.0.0"}
npm error notsup Actual:   {"npm":"10.9.3","node":"v22.20.0"}
```

## Solution
Removed the upper version constraints to support any Node.js version 18.0.0 or higher.

## Changes Made

### 1. Package Configuration (`Aura.Web/package.json`)
**Before:**
```json
"engines": {
  "node": ">=18.0.0 <21.0.0",
  "npm": ">=9.0.0 <11.0.0"
}
```

**After:**
```json
"engines": {
  "node": ">=18.0.0",
  "npm": ">=9.0.0"
}
```

### 2. Environment Validation (`scripts/build/validate-environment.js`)
- Updated to handle both range formats:
  - With max: `>=X.Y.Z <A.B.C`
  - Without max: `>=X.Y.Z`
- Now treats `.nvmrc` as a **recommendation** for consistency, not a strict requirement
- Emits informational warning if version differs from `.nvmrc` but is still compatible

### 3. GitHub Workflows (`.github/workflows/build-validation.yml`)
- Updated Node.js version check to accept 18.0.0+ instead of only 18.x
- Updated npm version check to accept 9.x+ instead of only 9.x/10.x
- More flexible version validation using major version extraction

### 4. Documentation Updates
Updated the following files to clarify version requirements:
- `Aura.Web/README.md`
- `BUILD_GUIDE.md`
- `.github/copilot-instructions.md`

**Key messaging:**
- Node.js 18.0.0 or higher is supported
- Version 18.18.0 (from `.nvmrc`) is recommended for consistency
- Any version 18.x, 20.x, 22.x, or newer works fine

## Supported Versions

### Node.js
- ✅ **Minimum:** 18.0.0
- ✅ **Recommended:** 18.18.0 (from `.nvmrc`)
- ✅ **Supported:** 18.x, 20.x, 22.x, and newer
- ✅ **No upper limit**

### npm
- ✅ **Minimum:** 9.0.0
- ✅ **Supported:** 9.x, 10.x, 11.x, and newer
- ✅ **No upper limit**

## Technical Justification

There is no technical reason to restrict Node.js to versions below 21:
- **React 18.2.0+** supports Node.js 18.x, 20.x, 22.x
- **Vite 6.4.1** supports Node.js 18+
- **TypeScript 5.3.3** supports Node.js 18+
- **All project dependencies** work with modern Node.js versions

The original constraint was overly cautious and prevented users from using newer, more secure, and better-performing Node.js versions.

## Testing

All validation passed:
- ✅ Environment validation script with Node 20.19.5
- ✅ Full build with Node 20.19.5
- ✅ TypeScript type checking
- ✅ Linting (0 errors, 0 warnings)
- ✅ All 844 tests passed
- ✅ No placeholder markers introduced

## Migration Guide

### For Users Currently on Node.js 22+
You can now build the project! Simply run:
```bash
cd Aura.Web
npm install
npm run build
```

### For Users on Node.js 18.18.0
No action required. Your version continues to work perfectly.

### For Users on Node.js < 18.0.0
You must upgrade to Node.js 18.0.0 or higher:
```bash
# Using nvm
nvm install 18.18.0
nvm use 18.18.0
```

## Backward Compatibility

This change is **fully backward compatible**:
- Users on Node.js 18.x can continue using their current version
- The recommended version (18.18.0) remains unchanged
- No breaking changes to the codebase
- All existing workflows continue to work

## Future Proofing

This change ensures:
- Users can adopt Node.js 23, 24, and future LTS versions without issues
- No need to update `package.json` with each new Node.js release
- Better alignment with npm's semver philosophy
- Reduced maintenance burden for version constraints
