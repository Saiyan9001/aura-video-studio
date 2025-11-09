# FFmpeg Status UI Changes - Visual Guide

## Before vs After Comparison

### Settings/Downloads Page - FFmpeg Card

#### ❌ BEFORE (Problematic Behavior)
```
┌─────────────────────────────────────────┐
│ FFmpeg  [Installed]                     │
│ Required for video rendering            │
├─────────────────────────────────────────┤
│ Path: C:\ffmpeg\bin\ffmpeg.exe         │
│ Version: unknown                        │  ⚠️ MISLEADING!
├─────────────────────────────────────────┤
│ [Rescan] [Open Folder] [Repair]        │
└─────────────────────────────────────────┘

Problem: Shows "Installed" even when:
- Version is unknown/null
- Binary might be corrupt
- Version is below 4.0 minimum
```

#### ✅ AFTER (Fixed Behavior)

**State 1: Not Installed**
```
┌─────────────────────────────────────────┐
│ FFmpeg  [Not Installed]                 │
│ Required for video rendering            │
├─────────────────────────────────────────┤
│ ⚠️ No path or version displayed         │
├─────────────────────────────────────────┤
│ [Install Managed FFmpeg]                │  ✅ Clear primary action
│ [Attach Existing...] [Manual Guide]    │
│ [Rescan]                                │
└─────────────────────────────────────────┘
```

**State 2: Installed and Valid (Managed)**
```
┌─────────────────────────────────────────┐
│ FFmpeg  [Installed ✓]                   │  ✅ Only shows when truly ready
│ Required for video rendering            │
├─────────────────────────────────────────┤
│ Path:                                   │
│ C:\Program Files\AuraVideoStudio\...    │
│                                         │
│ Version: 5.1.2 [✓ 4.0+]               │  ✅ With min-version validation
│ Source: [Managed Installation]         │  ✅ Shows source
├─────────────────────────────────────────┤
│ [Rescan] [Open Folder] [Repair]        │
└─────────────────────────────────────────┘
```

**State 3: Invalid/Corrupt**
```
┌─────────────────────────────────────────┐
│ FFmpeg  [Invalid ⚠️]                     │  ✅ Clear error state
│ Required for video rendering            │
├─────────────────────────────────────────┤
│ ⚠️ FFmpeg executable is corrupt or      │  ✅ Helpful error message
│    cannot be executed                   │
├─────────────────────────────────────────┤
│ [Install Managed FFmpeg]                │  ✅ Remediation CTA
│ [Attach Existing...] [Rescan]          │
└─────────────────────────────────────────┘
```

**State 4: Outdated Version**
```
┌─────────────────────────────────────────┐
│ FFmpeg  [Outdated ⚠️]                    │  ✅ Warning for old version
│ Required for video rendering            │
├─────────────────────────────────────────┤
│ Path: /opt/ffmpeg/bin/ffmpeg           │
│ Version: 3.4.0 [⚠️ Requires 4.0+]      │  ✅ Clear version requirement
│ Source: [User Configured]              │
├─────────────────────────────────────────┤
│ [Install Managed FFmpeg]                │  ✅ Offers upgrade path
│ [Rescan]                                │
└─────────────────────────────────────────┘
```

**State 5: Found in PATH**
```
┌─────────────────────────────────────────┐
│ FFmpeg  [Installed ✓]                   │
│ Required for video rendering            │
├─────────────────────────────────────────┤
│ Path: /usr/bin/ffmpeg                  │
│ Version: 4.4.2 [✓ 4.0+]               │
│ Source: [System PATH]                  │  ✅ Shows PATH as source
├─────────────────────────────────────────┤
│ [Rescan] [Open Folder] [Repair]        │
└─────────────────────────────────────────┘
```

**State 6: Attached Existing**
```
┌─────────────────────────────────────────┐
│ FFmpeg  [Installed ✓]                   │
│ Required for video rendering            │
├─────────────────────────────────────────┤
│ Path: D:\Tools\ffmpeg\ffmpeg.exe       │
│ Version: 6.0 [✓ 4.0+]                 │
│ Source: [User Configured]              │  ✅ Shows configured source
├─────────────────────────────────────────┤
│ [Rescan] [Open Folder] [Repair]        │
└─────────────────────────────────────────┘
```

### First-Run Wizard - Step 4 (System Requirements)

#### ❌ BEFORE (Broken)
```
┌─────────────────────────────────────────┐
│ Step 4: System Requirements             │
├─────────────────────────────────────────┤
│ FFmpeg Status                           │
│                                         │
│ ❌ Request failed with status code 428  │  ⚠️ CRYPTIC ERROR!
│                                         │
│ Status: Not Ready                       │
│                                         │
│ [< Back] [Skip >]                       │
└─────────────────────────────────────────┘

Problem: HTTP 428 (Precondition Required) because
/api/system/ffmpeg/status was blocked during wizard
```

#### ✅ AFTER (Fixed)

**When Not Installed**:
```
┌─────────────────────────────────────────┐
│ Step 4: System Requirements             │
├─────────────────────────────────────────┤
│ FFmpeg Status                           │
│                                         │
│ Installation: [Not Installed 🔴]        │  ✅ Clear status
│                                         │
│ ℹ️ About FFmpeg                         │
│ FFmpeg is required for video rendering  │
│ and must be version 4.0 or higher.     │
│                                         │
│ [Install FFmpeg] [Refresh Status]      │
└─────────────────────────────────────────┘
```

**After Installation**:
```
┌─────────────────────────────────────────┐
│ Step 4: System Requirements             │
├─────────────────────────────────────────┤
│ FFmpeg Status                           │
│                                         │
│ Installation: [Installed ✓]            │  ✅ Success state
│ Version: 5.1.2 [✓ 4.0+]               │
│ Location: C:\Program Files\...         │
│ Source: [Managed Installation]         │
│                                         │
│ Hardware Acceleration:                  │
│ [NVIDIA NVENC] [AMD AMF]              │  ✅ Bonus: shows GPU support
│                                         │
│ ℹ️ Hardware acceleration detected!      │
│ Video rendering will be 5-10x faster   │
│                                         │
│ [< Back] [Next >]                       │
└─────────────────────────────────────────┘
```

**If Error Occurs**:
```
┌─────────────────────────────────────────┐
│ Step 4: System Requirements             │
├─────────────────────────────────────────┤
│ FFmpeg Status                           │
│                                         │
│ Installation: [Not Installed 🔴]        │
│                                         │
│ ❌ Error: Unable to check FFmpeg status │  ✅ Helpful error message
│ Backend server may not be running.     │
│                                         │
│ [Refresh Status]                        │
└─────────────────────────────────────────┘
```

## Status Badge Colors

| Badge | Color | Meaning |
|-------|-------|---------|
| `[Installed ✓]` | Green (success) | FFmpeg is installed, valid, and meets version requirement |
| `[Not Installed]` | Gray (outline) | FFmpeg not detected |
| `[Invalid ⚠️]` | Red (danger) | FFmpeg found but corrupt or invalid |
| `[Outdated ⚠️]` | Yellow (warning) | FFmpeg version below 4.0 minimum |
| `[Unknown]` | Gray (outline) | Status check failed or inconclusive |

## Source Badges

| Badge | Meaning |
|-------|---------|
| `[Managed Installation]` | Installed via app's "Install Managed FFmpeg" button |
| `[System PATH]` | Found in system PATH environment variable |
| `[User Configured]` | Manually attached via "Attach Existing..." |
| `[None]` | Not detected |

## Button Labels

### Settings/Downloads Page

**Primary Actions** (when FFmpeg not ready):
- `[Install Managed FFmpeg]` - Downloads and installs FFmpeg automatically
- `[Attach Existing...]` - Opens dialog to specify custom path
- `[Manual Install Guide]` - Shows manual installation instructions

**Secondary Actions**:
- `[Rescan]` - Re-checks system for FFmpeg in standard locations
- `[Open Folder]` - Opens FFmpeg installation folder in file explorer
- `[Repair]` - Reinstalls/repairs managed FFmpeg installation

### First-Run Wizard

**Actions**:
- `[Install FFmpeg]` - Downloads and installs FFmpeg
- `[Refresh Status]` - Re-checks FFmpeg status

## Key Visual Differences Summary

### What Changed
1. **Badge accuracy**: Only shows "Installed" when truly ready
2. **Version display**: Always shows version with min-version validation badge
3. **Source transparency**: Shows where FFmpeg came from (Managed/PATH/Configured)
4. **Error clarity**: Meaningful messages instead of HTTP codes
5. **Button clarity**: "Install Managed FFmpeg" vs generic "Install"

### What Stayed the Same
- Overall card layout and structure
- Dialog for "Attach Existing..." path input
- Manual installation guide modal
- Rescan, Open Folder, Repair button positions

### Benefits
- ✅ Users know exactly what state FFmpeg is in
- ✅ Clear guidance on next steps (install/attach/upgrade)
- ✅ No false "Installed" status
- ✅ Version requirements visible at a glance
- ✅ Source of FFmpeg installation is transparent
- ✅ Wizard no longer shows cryptic 428 errors
