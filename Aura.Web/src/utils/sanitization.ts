/**
 * Input sanitization utilities for security
 */

/**
 * Sanitizes HTML content by encoding special characters
 */
export function sanitizeHtml(input: string): string {
  const div = document.createElement('div');
  div.textContent = input;
  return div.innerHTML;
}

/**
 * Checks if input contains XSS patterns
 */
export function containsXssPattern(input: string): boolean {
  const xssPatterns = [
    /<script[^>]*>/i,
    /<\/script>/i,
    /javascript:/i,
    /on\w+\s*=/i,
    /<iframe/i,
    /<object/i,
    /<embed/i,
    /<svg[^>]*on\w+/i,
  ];

  return xssPatterns.some((pattern) => pattern.test(input));
}

/**
 * Checks if input contains prompt injection patterns
 */
export function containsPromptInjection(input: string): boolean {
  const injectionPatterns = [
    /ignore\s+all\s+previous\s+instruction/i,
    /ignore\s+previous\s+instruction/i,
    /ignore\s+all\s+prior\s+instruction/i,
    /ignore\s+prior\s+instruction/i,
    /ignore\s+all\s+above\s+instruction/i,
    /ignore\s+above\s+instruction/i,
    /disregard\s+all\s+previous\s+instruction/i,
    /disregard\s+previous\s+instruction/i,
    /disregard\s+all\s+prior\s+instruction/i,
    /disregard\s+prior\s+instruction/i,
    /disregard\s+all\s+above\s+instruction/i,
    /disregard\s+above\s+instruction/i,
    /forget\s+all\s+previous\s+instruction/i,
    /forget\s+previous\s+instruction/i,
    /forget\s+all\s+prior\s+instruction/i,
    /forget\s+prior\s+instruction/i,
    /forget\s+all\s+above\s+instruction/i,
    /forget\s+above\s+instruction/i,
    /system\s*:\s*/i,
    /<\|im_start\|>/i,
    /<\|im_end\|>/i,
    /\[INST\]/i,
    /\[\/INST\]/i,
    /<\|endoftext\|>/i,
    /you\s+are\s+now\s+a\s+/i,
    /you\s+are\s+now\s+an\s+/i,
    /act\s+as\s+a\s+/i,
    /act\s+as\s+an\s+/i,
  ];

  return injectionPatterns.some((pattern) => pattern.test(input));
}

/**
 * Checks if input contains path traversal attempts
 */
export function containsPathTraversal(input: string): boolean {
  return input.includes('..');
}

/**
 * Removes control characters except common whitespace
 */
export function removeControlCharacters(input: string): string {
  let result = '';
  for (const char of input) {
    const code = char.charCodeAt(0);
    // Allow: 9 (tab), 10 (newline), 13 (carriage return), 32+ (printable)
    if ((code >= 32 && code !== 127) || code === 9 || code === 10 || code === 13) {
      result += char;
    }
  }
  return result;
}

/**
 * Validates and sanitizes file name
 */
export function sanitizeFileName(fileName: string): string {
  // Remove path separators and traversal attempts
  let sanitized = fileName.replace(/[/\\]/g, '').replace(/\.\./g, '');

  // Remove control characters
  sanitized = removeControlCharacters(sanitized);

  // Limit length
  if (sanitized.length > 255) {
    const ext = sanitized.split('.').pop() || '';
    const nameWithoutExt = sanitized.substring(0, sanitized.lastIndexOf('.'));
    sanitized = nameWithoutExt.substring(0, 255 - ext.length - 1) + '.' + ext;
  }

  return sanitized;
}

/**
 * Validates file extension against whitelist
 */
export function isAllowedExtension(fileName: string, allowedExtensions: string[]): boolean {
  const ext = `.${fileName.split('.').pop()?.toLowerCase() || ''}`;
  return allowedExtensions.includes(ext);
}

/**
 * Real-time validation result for form fields
 */
export interface ValidationResult {
  isValid: boolean;
  error?: string;
  warning?: string;
}

/**
 * Validates user input against security rules
 */
export function validateUserInput(
  input: string,
  options: {
    maxLength?: number;
    checkXss?: boolean;
    checkPromptInjection?: boolean;
    allowEmpty?: boolean;
  } = {}
): ValidationResult {
  const { maxLength, checkXss = true, checkPromptInjection = false, allowEmpty = false } = options;

  // Check empty
  if (!allowEmpty && (!input || input.trim().length === 0)) {
    return { isValid: false, error: 'This field is required' };
  }

  // Check max length
  if (maxLength && input.length > maxLength) {
    return {
      isValid: false,
      error: `Must not exceed ${maxLength.toLocaleString()} characters`,
    };
  }

  // Check for XSS patterns
  if (checkXss && containsXssPattern(input)) {
    return {
      isValid: false,
      error: 'Input contains potentially dangerous content',
    };
  }

  // Check for prompt injection
  if (checkPromptInjection && containsPromptInjection(input)) {
    return {
      isValid: false,
      error: 'Input contains invalid patterns',
    };
  }

  // Warning if approaching max length
  if (maxLength && input.length > maxLength * 0.9) {
    return {
      isValid: true,
      warning: `Approaching limit: ${input.length} / ${maxLength} characters`,
    };
  }

  return { isValid: true };
}
