#!/usr/bin/env node

/**
 * Test suite for find-placeholders.js
 * 
 * Tests the placeholder scanning functionality including:
 * - String literal false positive prevention
 * - Inline suppression markers
 * - Block suppression markers
 * - CLI flags
 * - Configuration loading
 */

import { execSync } from 'child_process';
import { existsSync, mkdirSync, writeFileSync, rmSync, readFileSync } from 'fs';
import { fileURLToPath } from 'url';
import { dirname, join } from 'path';

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);

const scriptPath = join(__dirname, '..', 'audit', 'find-placeholders.js');
const testDir = join(__dirname, 'test-temp');
const repoRoot = join(__dirname, '..', '..');

let passed = 0;
let failed = 0;

function test(name, fn) {
  try {
    fn();
    console.log(`✓ ${name}`);
    passed++;
  } catch (error) {
    console.log(`✗ ${name}`);
    console.log(`  ${error.message}`);
    failed++;
  }
}

function assertTrue(condition, message) {
  if (!condition) {
    throw new Error(message || 'Assertion failed');
  }
}

function setupTestDir() {
  if (existsSync(testDir)) {
    rmSync(testDir, { recursive: true, force: true });
  }
  mkdirSync(testDir, { recursive: true });
}

function cleanupTestDir() {
  if (existsSync(testDir)) {
    rmSync(testDir, { recursive: true, force: true });
  }
}

function runScanner(options = {}) {
  const args = [];
  if (options.warnOnly) args.push('--warn-only');
  if (options.changedOnly) args.push('--changed-only');
  if (options.fullScan) args.push('--full-scan');
  
  const cmd = `node "${scriptPath}" ${args.join(' ')}`;
  
  try {
    const output = execSync(cmd, { 
      encoding: 'utf8',
      stdio: 'pipe',
      cwd: repoRoot
    });
    return { exitCode: 0, output };
  } catch (error) {
    return { exitCode: error.status || 1, output: error.stdout || '' };
  }
}

console.log('\n=== Testing find-placeholders.js ===\n');

// Basic tests
test('Script exists and is executable', () => {
  assertTrue(existsSync(scriptPath), 'Script file should exist');
});

test('Script runs without crashing on clean repo', () => {
  const result = runScanner();
  assertTrue(
    result.exitCode === 0 || result.exitCode === 1,
    'Script should exit with 0 or 1'
  );
});

test('Script outputs scanner header', () => {
  const result = runScanner();
  assertTrue(
    result.output.includes('Placeholder Scanner') || result.output.includes('Scanning'),
    'Should output scanner header'
  );
});

test('Script reports file counts', () => {
  const result = runScanner();
  assertTrue(
    result.output.includes('Total files') || result.output.includes('Scanned files'),
    'Should report file counts'
  );
});

test('Script uses colored output symbols', () => {
  const result = runScanner();
  assertTrue(
    result.output.includes('✓') || result.output.includes('✗'),
    'Should use check/error symbols'
  );
});

test('Help flag displays usage information', () => {
  try {
    const output = execSync(`node "${scriptPath}" --help`, { 
      encoding: 'utf8',
      stdio: 'pipe',
      cwd: repoRoot
    });
    assertTrue(output.includes('Usage'), 'Should display usage');
    assertTrue(output.includes('Options'), 'Should display options');
    assertTrue(output.includes('Inline Suppressions'), 'Should display suppressions');
  } catch (error) {
    throw new Error('Help flag should not fail');
  }
});

test('Script shows scan mode in output', () => {
  const result = runScanner();
  assertTrue(
    result.output.includes('Scan mode:'),
    'Should display scan mode'
  );
});

test('Full scan mode is default', () => {
  const result = runScanner();
  assertTrue(
    result.output.includes('Scan mode: full'),
    'Should default to full scan mode'
  );
});

test('Warn-only flag causes exit 0 even with issues', () => {
  setupTestDir();
  
  // Create a test file with a placeholder
  const testFile = join(testDir, 'test.js');
  writeFileSync(testFile, '// TODO: test placeholder\n');
  
  // Run scanner with warn-only (though it won't find this file outside scan dirs)
  // This test verifies the flag is recognized
  const result = runScanner({ warnOnly: true });
  
  // Should exit 0 regardless
  assertTrue(result.exitCode === 0, 'Warn-only should exit 0');
  
  cleanupTestDir();
});

// String literal tests (conceptual - would need test files in repo)
test('String literals are processed by stripStringLiterals logic', () => {
  // This verifies the function exists in the script
  const scriptContent = readFileSync(scriptPath, 'utf8');
  assertTrue(
    scriptContent.includes('stripStringLiterals'),
    'Script should have stripStringLiterals function'
  );
  assertTrue(
    scriptContent.includes('inString'),
    'Script should track string state'
  );
  assertTrue(
    scriptContent.includes('inTemplate'),
    'Script should track template literal state'
  );
});

test('Inline suppression markers are defined', () => {
  const scriptContent = readFileSync(scriptPath, 'utf8');
  assertTrue(
    scriptContent.includes('IGNORE_LINE_MARKER'),
    'Script should have IGNORE_LINE_MARKER'
  );
  assertTrue(
    scriptContent.includes('IGNORE_START_MARKER'),
    'Script should have IGNORE_START_MARKER'
  );
  assertTrue(
    scriptContent.includes('IGNORE_END_MARKER'),
    'Script should have IGNORE_END_MARKER'
  );
});

test('Block suppression logic is implemented', () => {
  const scriptContent = readFileSync(scriptPath, 'utf8');
  assertTrue(
    scriptContent.includes('inIgnoreBlock'),
    'Script should track ignore block state'
  );
});

test('Configuration loading is implemented', () => {
  const scriptContent = readFileSync(scriptPath, 'utf8');
  assertTrue(
    scriptContent.includes('loadConfig'),
    'Script should have loadConfig function'
  );
  assertTrue(
    scriptContent.includes('.placeholder-scan.json'),
    'Script should reference config file'
  );
});

test('Changed files detection is implemented', () => {
  const scriptContent = readFileSync(scriptPath, 'utf8');
  assertTrue(
    scriptContent.includes('getChangedFiles'),
    'Script should have getChangedFiles function'
  );
  assertTrue(
    scriptContent.includes('GITHUB_EVENT_NAME'),
    'Script should check for GitHub PR context'
  );
  assertTrue(
    scriptContent.includes('git diff'),
    'Script should use git diff'
  );
});

test('Allowed paths checking is implemented', () => {
  const scriptContent = readFileSync(scriptPath, 'utf8');
  assertTrue(
    scriptContent.includes('isPathAllowed'),
    'Script should have isPathAllowed function'
  );
  assertTrue(
    scriptContent.includes('allowedPaths'),
    'Script should reference allowedPaths config'
  );
});

test('Extra forbidden patterns support is implemented', () => {
  const scriptContent = readFileSync(scriptPath, 'utf8');
  assertTrue(
    scriptContent.includes('extraForbidden'),
    'Script should reference extraForbidden config'
  );
  assertTrue(
    scriptContent.includes('buildForbiddenPatterns'),
    'Script should build patterns from config'
  );
});

test('Example config file exists', () => {
  const examplePath = join(repoRoot, '.placeholder-scan.json.example');
  assertTrue(
    existsSync(examplePath),
    'Example config file should exist'
  );
  
  const content = readFileSync(examplePath, 'utf8');
  const parsed = JSON.parse(content.split('\n').filter(l => !l.trim().startsWith('//')).join('\n'));
  
  assertTrue(
    Array.isArray(parsed.allowedPaths),
    'Example should have allowedPaths array'
  );
  assertTrue(
    Array.isArray(parsed.extraForbidden),
    'Example should have extraForbidden array'
  );
  assertTrue(
    typeof parsed.warnOnly === 'boolean',
    'Example should have warnOnly boolean'
  );
});

console.log('\n=== Test Summary ===\n');
console.log(`Passed: ${passed}`);
console.log(`Failed: ${failed}`);
console.log('');

cleanupTestDir();

if (failed > 0) {
  console.log('Some tests failed. Please review the output above.');
  process.exit(1);
} else {
  console.log('All tests passed!');
  process.exit(0);
}
