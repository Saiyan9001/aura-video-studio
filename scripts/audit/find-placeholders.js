#!/usr/bin/env node

/**
 * Robust Placeholder Scanner
 * 
 * Searches for TODO, FIXME, HACK, XXX, WIP markers in source code comments.
 * 
 * Features:
 * - Ignores matches inside string and template literals (no false positives)
 * - Supports inline suppressions for rare, justified exceptions
 * - PR-aware: scans only changed files in GitHub Actions pull_request events
 * - Configurable via optional .placeholder-scan.json
 * 
 * Usage:
 *   node scripts/audit/find-placeholders.js [options]
 * 
 * Options:
 *   --warn-only       Exit 0 even when issues found (prints report)
 *   --changed-only    Scan only git-changed files (requires git repo)
 *   --full-scan       Force full repository scan (default in local mode)
 * 
 * Inline Suppressions:
 *   (Use these markers in your code for justified exceptions)
 *   - Line suppression: // placeholder-scan: ignore-line
 *   - Block start: slash-star placeholder-scan: ignore-start star-slash
 *   - Block end: slash-star placeholder-scan: ignore-end star-slash
 * 
 * Configuration (.placeholder-scan.json):
 *   {
 *     "allowedPaths": ["test/fixtures/", "examples/"],
 *     "extraForbidden": ["\\bNOTE\\b"],
 *     "warnOnly": false
 *   }
 */

import { readFileSync, readdirSync, statSync, existsSync } from 'fs';
import { join, relative } from 'path';
import { fileURLToPath } from 'url';
import { dirname } from 'path';
import { execSync } from 'child_process';

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);

const REPO_ROOT = join(__dirname, '..', '..');

// Default forbidden patterns (case-insensitive)
const DEFAULT_FORBIDDEN_PATTERNS = [
  /\/\/\s*TODO\b/i,           // // TODO
  /\/\/\s*FIXME\b/i,          // // FIXME
  /\/\/\s*HACK\b/i,           // // HACK
  /\/\/\s*XXX\b/,             // // XXX
  /\/\/\s*WIP\b/i,            // // WIP
  /\/\*\s*TODO\b/i,           // /* TODO
  /\/\*\s*FIXME\b/i,          // /* FIXME
  /\/\*\s*HACK\b/i,           // /* HACK
  /\/\*\s*XXX\b/,             // /* XXX
  /\/\*\s*WIP\b/i,            // /* WIP
];

// Suppression markers
const IGNORE_LINE_MARKER = /placeholder-scan:\s*ignore-line/i;
const IGNORE_START_MARKER = /placeholder-scan:\s*ignore-start/i;
const IGNORE_END_MARKER = /placeholder-scan:\s*ignore-end/i;

// File extensions to scan
const SCAN_EXTENSIONS = [
  '.ts', '.tsx', '.js', '.jsx',
  '.cs', '.csx',
  '.json', '.yml', '.yaml'
];

// Directories to exclude
const EXCLUDE_DIRS = [
  'node_modules',
  'dist',
  'build',
  'bin',
  'obj',
  '.git',
  '.vs',
  '.vscode',
  'coverage',
  'wwwroot',
  '.husky',
  'out'
];

// Files to exclude (by name)
const EXCLUDE_FILES = [
  'package-lock.json',
  'package.json.bak',
  '.eslintrc.cjs',
  'tsconfig.json'
];

// Special files that are allowed to have placeholders (documentation only)
const ALLOWED_FILES = [
  // All markdown files are allowed (they're documentation)
  '.md'
];

// Global state
let totalFiles = 0;
let scannedFiles = 0;
let issues = [];
let config = null;
let forbiddenPatterns = [];
let scanMode = 'full';
let cliOptions = {
  warnOnly: false,
  changedOnly: false,
  fullScan: false
};

/**
 * Load optional configuration from .placeholder-scan.json
 */
function loadConfig() {
  const configPath = join(REPO_ROOT, '.placeholder-scan.json');
  
  if (!existsSync(configPath)) {
    return {
      allowedPaths: [],
      extraForbidden: [],
      warnOnly: false
    };
  }
  
  try {
    const content = readFileSync(configPath, 'utf8');
    const parsed = JSON.parse(content);
    return {
      allowedPaths: parsed.allowedPaths || [],
      extraForbidden: parsed.extraForbidden || [],
      warnOnly: parsed.warnOnly || false
    };
  } catch (error) {
    console.warn(`Warning: Failed to parse .placeholder-scan.json: ${error.message}`);
    return {
      allowedPaths: [],
      extraForbidden: [],
      warnOnly: false
    };
  }
}

/**
 * Build forbidden patterns from defaults + config extras
 */
function buildForbiddenPatterns(config) {
  const patterns = [...DEFAULT_FORBIDDEN_PATTERNS];
  
  for (const extra of config.extraForbidden) {
    try {
      patterns.push(new RegExp(extra, 'i'));
    } catch (error) {
      console.warn(`Warning: Invalid regex in extraForbidden: ${extra}`);
    }
  }
  
  return patterns;
}

/**
 * Parse CLI arguments
 */
function parseCliArgs() {
  const args = process.argv.slice(2);
  const opts = {
    warnOnly: false,
    changedOnly: false,
    fullScan: false
  };
  
  for (const arg of args) {
    if (arg === '--warn-only') {
      opts.warnOnly = true;
    } else if (arg === '--changed-only') {
      opts.changedOnly = true;
    } else if (arg === '--full-scan') {
      opts.fullScan = true;
    } else if (arg === '--help' || arg === '-h') {
      printHelp();
      process.exit(0);
    }
  }
  
  return opts;
}

/**
 * Print help message
 */
function printHelp() {
  console.log(`
Placeholder Scanner - Find TODO/FIXME/HACK markers in code

Usage:
  node scripts/audit/find-placeholders.js [options]

Options:
  --warn-only       Exit 0 even when issues found (prints report)
  --changed-only    Scan only git-changed files (requires git repo)
  --full-scan       Force full repository scan (default in local mode)
  --help, -h        Show this help message

Inline Suppressions:
  // placeholder-scan: ignore-line       Ignore the current line
  /* placeholder-scan: ignore-start */   Start ignoring
  /* placeholder-scan: ignore-end */     Stop ignoring

Configuration:
  Create .placeholder-scan.json in repo root:
  {
    "allowedPaths": ["test/fixtures/", "examples/"],
    "extraForbidden": ["\\\\bNOTE\\\\b"],
    "warnOnly": false
  }
`);
}

/**
 * Strip string and template literals from a line to avoid false positives
 */
function stripStringLiterals(line) {
  let result = '';
  let i = 0;
  let inString = false;
  let stringChar = '';
  let inTemplate = false;
  
  while (i < line.length) {
    const char = line[i];
    const nextChar = line[i + 1];
    
    // Handle escape sequences
    if (inString && char === '\\' && nextChar) {
      i += 2;
      continue;
    }
    
    // Handle template literals
    if (!inString && char === '`') {
      inTemplate = !inTemplate;
      i++;
      continue;
    }
    
    if (inTemplate) {
      i++;
      continue;
    }
    
    // Handle string literals
    if (!inString && (char === '"' || char === "'")) {
      inString = true;
      stringChar = char;
      i++;
      continue;
    }
    
    if (inString && char === stringChar) {
      inString = false;
      stringChar = '';
      i++;
      continue;
    }
    
    if (!inString) {
      result += char;
    }
    
    i++;
  }
  
  return result;
}

/**
 * Check if a path is in the allowed list
 */
function isPathAllowed(filePath) {
  if (!config || !config.allowedPaths || config.allowedPaths.length === 0) {
    return false;
  }
  
  const relativePath = relative(REPO_ROOT, filePath);
  
  for (const allowedPath of config.allowedPaths) {
    if (relativePath.includes(allowedPath)) {
      return true;
    }
  }
  
  return false;
}

function shouldScanFile(filePath) {
  const fileName = filePath.split('/').pop() || '';
  
  // Check if path is in allowed list
  if (isPathAllowed(filePath)) {
    return false;
  }
  
  // Exclude specific files
  if (EXCLUDE_FILES.includes(fileName)) {
    return false;
  }
  
  // Allow certain file types
  for (const ext of ALLOWED_FILES) {
    if (fileName.endsWith(ext)) {
      return false;
    }
  }
  
  // Check if file has a scannable extension
  for (const ext of SCAN_EXTENSIONS) {
    if (fileName.endsWith(ext)) {
      return true;
    }
  }
  
  return false;
}

function shouldScanDirectory(dirPath, dirName) {
  // Exclude certain directories
  if (EXCLUDE_DIRS.includes(dirName)) {
    return false;
  }
  
  // Exclude this script's directory
  if (dirPath.includes('/scripts/audit')) {
    return false;
  }
  
  return true;
}

function scanFile(filePath) {
  scannedFiles++;
  
  try {
    const content = readFileSync(filePath, 'utf8');
    const lines = content.split('\n');
    let inIgnoreBlock = false;
    
    for (let i = 0; i < lines.length; i++) {
      const line = lines[i];
      
      // Check for ignore markers
      if (IGNORE_START_MARKER.test(line)) {
        inIgnoreBlock = true;
        continue;
      }
      
      if (IGNORE_END_MARKER.test(line)) {
        inIgnoreBlock = false;
        continue;
      }
      
      // Skip if in ignore block
      if (inIgnoreBlock) {
        continue;
      }
      
      // Check for ignore-line marker
      if (IGNORE_LINE_MARKER.test(line)) {
        continue;
      }
      
      // Strip string literals before checking for patterns
      const strippedLine = stripStringLiterals(line);
      
      for (const pattern of forbiddenPatterns) {
        if (pattern.test(strippedLine)) {
          const relativePath = relative(REPO_ROOT, filePath);
          issues.push({
            file: relativePath,
            line: i + 1,
            pattern: pattern.source,
            context: line.trim()
          });
        }
      }
    }
  } catch (error) {
    // Silently skip files that can't be read
  }
}

/**
 * Get list of changed files in a PR context
 */
function getChangedFiles() {
  try {
    // Check if we're in a GitHub Actions PR context
    const isGitHubPR = process.env.GITHUB_EVENT_NAME === 'pull_request';
    const baseBranch = process.env.GITHUB_BASE_REF;
    
    if (!isGitHubPR || !baseBranch) {
      return null;
    }
    
    // Fetch the base branch with shallow history
    try {
      execSync(`git fetch --depth=2 origin ${baseBranch}`, {
        cwd: REPO_ROOT,
        stdio: 'pipe'
      });
    } catch (error) {
      // Ignore fetch errors, may already have the data
    }
    
    // Get merge base
    const mergeBase = execSync(
      `git merge-base HEAD origin/${baseBranch}`,
      { cwd: REPO_ROOT, encoding: 'utf8' }
    ).trim();
    
    // Get list of changed files
    const changedFilesOutput = execSync(
      `git diff --name-only HEAD ${mergeBase}`,
      { cwd: REPO_ROOT, encoding: 'utf8' }
    );
    
    const changedFiles = changedFilesOutput
      .split('\n')
      .map(f => f.trim())
      .filter(f => f.length > 0)
      .map(f => join(REPO_ROOT, f));
    
    return changedFiles;
  } catch (error) {
    // Fall back to full scan if git operations fail
    console.warn(`Warning: Could not determine changed files: ${error.message}`);
    return null;
  }
}

/**
 * Scan specific list of files
 */
function scanFileList(files) {
  for (const filePath of files) {
    if (!existsSync(filePath)) {
      continue;
    }
    
    try {
      const stats = statSync(filePath);
      if (stats.isFile()) {
        totalFiles++;
        if (shouldScanFile(filePath)) {
          scanFile(filePath);
        }
      }
    } catch (error) {
      // Skip files we can't access
    }
  }
}

function scanDirectory(dirPath) {
  try {
    const entries = readdirSync(dirPath);
    
    for (const entry of entries) {
      const fullPath = join(dirPath, entry);
      
      try {
        const stats = statSync(fullPath);
        
        if (stats.isDirectory()) {
          if (shouldScanDirectory(fullPath, entry)) {
            scanDirectory(fullPath);
          }
        } else if (stats.isFile()) {
          totalFiles++;
          if (shouldScanFile(fullPath)) {
            scanFile(fullPath);
          }
        }
      } catch (error) {
        // Silently skip entries we can't stat
      }
    }
  } catch (error) {
    // Silently skip directories we can't read
  }
}

function printResults() {
  console.log('\n=== Placeholder Scanner Results ===\n');
  console.log(`Scan mode: ${scanMode}`);
  console.log(`Total files: ${totalFiles}`);
  console.log(`Scanned files: ${scannedFiles}\n`);
  
  if (issues.length === 0) {
    console.log('\x1b[32m✓ No placeholder markers found!\x1b[0m');
    console.log('  Repository is clean.\n');
    return 0;
  }
  
  console.log(`\x1b[31m✗ Found ${issues.length} placeholder marker(s):\x1b[0m\n`);
  
  // Group by file
  const byFile = {};
  for (const issue of issues) {
    if (!byFile[issue.file]) {
      byFile[issue.file] = [];
    }
    byFile[issue.file].push(issue);
  }
  
  for (const [file, fileIssues] of Object.entries(byFile)) {
    console.log(`  \x1b[33m${file}\x1b[0m`);
    for (const issue of fileIssues) {
      console.log(`    \x1b[31mLine ${issue.line}:\x1b[0m ${issue.context}`);
    }
    console.log('');
  }
  
  console.log('\x1b[33mPlease remove all placeholder markers before committing.\x1b[0m');
  console.log('Or use inline suppressions for justified exceptions:\n');
  console.log('  // placeholder-scan: ignore-line');
  console.log('  /* placeholder-scan: ignore-start */ ... /* placeholder-scan: ignore-end */\n');
  
  return 1;
}

function main() {
  // Parse CLI arguments
  cliOptions = parseCliArgs();
  
  // Load configuration
  config = loadConfig();
  
  // Build forbidden patterns
  forbiddenPatterns = buildForbiddenPatterns(config);
  
  // Determine scan mode
  const isGitHubPR = process.env.GITHUB_EVENT_NAME === 'pull_request';
  const shouldScanChangedOnly = (isGitHubPR && !cliOptions.fullScan) || cliOptions.changedOnly;
  
  console.log('Scanning for placeholder markers...');
  console.log(`Root: ${REPO_ROOT}`);
  
  if (shouldScanChangedOnly) {
    const changedFiles = getChangedFiles();
    
    if (changedFiles && changedFiles.length > 0) {
      scanMode = 'changed-only (PR diff)';
      console.log(`Mode: Scanning ${changedFiles.length} changed file(s)\n`);
      scanFileList(changedFiles);
    } else {
      scanMode = 'full (fallback)';
      console.log('Mode: Full scan (could not determine changed files)\n');
      scanDirectory(REPO_ROOT);
    }
  } else {
    scanMode = 'full';
    console.log('Mode: Full repository scan\n');
    scanDirectory(REPO_ROOT);
  }
  
  const exitCode = printResults();
  
  // Apply warn-only mode from config or CLI
  const warnOnly = cliOptions.warnOnly || config.warnOnly;
  
  if (warnOnly && exitCode !== 0) {
    console.log('\x1b[33m⚠ Warn-only mode: exiting with code 0\x1b[0m\n');
    process.exit(0);
  }
  
  process.exit(exitCode);
}

main();
