/**
 * Form validation utilities using Zod
 */

import { z } from 'zod';

/**
 * Common validation schemas
 */
export const validationSchemas = {
  email: z.string().email('Invalid email address'),
  password: z.string().min(8, 'Password must be at least 8 characters'),
  url: z.string().url('Invalid URL'),
  positiveNumber: z.number().positive('Must be a positive number'),
  nonEmptyString: z.string().min(1, 'This field is required'),
  optionalString: z.string().optional(),
};

/**
 * Validation helpers
 */
export const validators = {
  required: (message = 'This field is required') => z.string().min(1, message),

  minLength: (length: number, message?: string) =>
    z.string().min(length, message || `Must be at least ${length} characters`),

  maxLength: (length: number, message?: string) =>
    z.string().max(length, message || `Must be no more than ${length} characters`),

  length: (min: number, max: number, message?: string) =>
    z
      .string()
      .min(min, message || `Must be between ${min} and ${max} characters`)
      .max(max, message || `Must be between ${min} and ${max} characters`),

  email: (message = 'Invalid email address') => z.string().email(message),

  url: (message = 'Invalid URL') => z.string().url(message),

  httpUrl: (message = 'Invalid HTTP/HTTPS URL') =>
    z
      .string()
      .url(message)
      .refine((val: string) => val.startsWith('http://') || val.startsWith('https://'), {
        message: message || 'URL must start with http:// or https://',
      }),

  number: (message = 'Must be a number') => z.number({ invalid_type_error: message }),

  positiveNumber: (message = 'Must be a positive number') => z.number().positive(message),

  range: (min: number, max: number, message?: string) =>
    z
      .number()
      .min(min, message || `Must be at least ${min}`)
      .max(max, message || `Must be no more than ${max}`),

  phone: (message = 'Invalid phone number') =>
    z.string().regex(/^[+]?[(]?[0-9]{1,4}[)]?[-\s.]?[(]?[0-9]{1,4}[)]?[-\s.]?[0-9]{1,9}$/, message),

  alphanumeric: (message = 'Must contain only letters and numbers') =>
    z.string().regex(/^[a-zA-Z0-9]+$/, message),

  filePath: (message = 'Invalid file path') =>
    z
      .string()
      .min(1, 'Path cannot be empty')
      .refine(
        (val: string) => {
          // Allow absolute paths on Windows (C:\...) or Unix (/...)
          return /^([a-zA-Z]:\\|\\\\|\/)/i.test(val) || val.includes('\\') || val.includes('/');
        },
        { message: message }
      ),

  apiKey: (minLength = 10, message?: string) =>
    z
      .string()
      .min(minLength, message || `API key must be at least ${minLength} characters`)
      .regex(/^[a-zA-Z0-9_-]+$/, 'API key contains invalid characters'),

  port: () =>
    z
      .number()
      .int('Port must be a whole number')
      .min(1, 'Port must be at least 1')
      .max(65535, 'Port must be no more than 65535'),

  urlWithPort: (message = 'Invalid URL with port') =>
    z
      .string()
      .url(message)
      .refine(
        (val: string) => {
          try {
            const url = new URL(val);
            return (
              url.hostname && (url.port || url.protocol === 'http:' || url.protocol === 'https:')
            );
          } catch {
            return false;
          }
        },
        { message: 'Must be a valid URL with host and port' }
      ),

  duration: (minSeconds = 10, maxMinutes = 30, message?: string) =>
    z
      .number()
      .min(minSeconds / 60, message || `Duration must be at least ${minSeconds} seconds`)
      .max(maxMinutes, message || `Duration must be no more than ${maxMinutes} minutes`),

  nonEmptyArray: (message = 'At least one item is required') => z.array(z.any()).min(1, message),

  hexColor: (message = 'Invalid hex color') =>
    z.string().regex(/^[0-9A-Fa-f]{6}$/, message || 'Must be a 6-digit hex color (e.g., FF0000)'),
};

/**
 * Custom validation functions
 */
export function createFormSchema<T extends z.ZodRawShape>(shape: T) {
  return z.object(shape);
}

export function validateField<T>(
  schema: z.ZodType<T>,
  value: unknown
): { valid: boolean; error?: string } {
  try {
    schema.parse(value);
    return { valid: true };
  } catch (err: unknown) {
    if (err instanceof z.ZodError) {
      return { valid: false, error: err.errors[0]?.message };
    }
    return { valid: false, error: 'Validation failed' };
  }
}

/**
 * File validation
 */
export function validateFile(
  file: File,
  options: {
    maxSize?: number; // in bytes
    allowedTypes?: string[];
  }
): { valid: boolean; error?: string } {
  if (options.maxSize && file.size > options.maxSize) {
    const sizeMB = (options.maxSize / (1024 * 1024)).toFixed(2);
    return { valid: false, error: `File size must be less than ${sizeMB}MB` };
  }

  if (options.allowedTypes && !options.allowedTypes.includes(file.type)) {
    return { valid: false, error: `File type must be one of: ${options.allowedTypes.join(', ')}` };
  }

  return { valid: true };
}

/**
 * Form submission helper
 */
export async function handleFormSubmit<T>(
  data: T,
  submitFn: (data: T) => Promise<void>,
  options?: {
    onSuccess?: () => void;
    onError?: (error: Error) => void;
  }
): Promise<{ success: boolean; error?: Error }> {
  try {
    await submitFn(data);
    options?.onSuccess?.();
    return { success: true };
  } catch (error) {
    const err = error instanceof Error ? error : new Error('Submission failed');
    options?.onError?.(err);
    return { success: false, error: err };
  }
}

/**
 * Brief validation schema
 */
export const briefValidationSchema = z.object({
  topic: z.string().min(3, 'Topic must be at least 3 characters'),
  audience: z.string().optional(),
  goal: z.string().optional(),
  tone: z.string().optional(),
  language: z.string().optional(),
  durationMinutes: z
    .number()
    .min(10 / 60, 'Duration must be at least 10 seconds (0.17 minutes)')
    .max(30, 'Duration must be no more than 30 minutes'),
});

/**
 * Validates a brief validation request before sending to API
 */
export function validateBriefRequest(request: {
  topic?: string;
  audience?: string;
  goal?: string;
  tone?: string;
  language?: string;
  durationMinutes?: number;
}): { valid: boolean; errors: string[] } {
  const result = briefValidationSchema.safeParse(request);

  if (result.success) {
    return { valid: true, errors: [] };
  }

  const errors = result.error.errors.map(
    (err: z.ZodIssue) => `${err.path.join('.')}: ${err.message}`
  );

  return { valid: false, errors };
}

/**
 * Video creation form validation schema
 */
export const createVideoSchema = z.object({
  topic: z
    .string()
    .min(3, 'Topic must be at least 3 characters')
    .max(100, 'Topic must be no more than 100 characters'),
  audience: z.string().optional(),
  goal: z.string().optional(),
  tone: z.string().optional(),
  language: z.string().optional(),
  aspect: z.enum(['Widescreen16x9', 'Vertical9x16', 'Square1x1']).optional(),
  targetDurationMinutes: z
    .number()
    .min(10 / 60, 'Duration must be at least 10 seconds')
    .max(30, 'Duration must be no more than 30 minutes'),
});

/**
 * API Keys validation schema
 */
export const apiKeysSchema = z.object({
  openai: z
    .string()
    .optional()
    .refine(
      (val: string | undefined) =>
        !val || val.length === 0 || (val.startsWith('sk-') && val.length > 20),
      {
        message: 'OpenAI API key must start with "sk-" and be at least 20 characters',
      }
    ),
  elevenlabs: z
    .string()
    .optional()
    .refine((val: string | undefined) => !val || val.length === 0 || val.length >= 32, {
      message: 'ElevenLabs API key must be at least 32 characters',
    }),
  pexels: z
    .string()
    .optional()
    .refine((val: string | undefined) => !val || val.length === 0 || val.length >= 20, {
      message: 'Pexels API key must be at least 20 characters',
    }),
  pixabay: z
    .string()
    .optional()
    .refine((val: string | undefined) => !val || val.length === 0 || val.length >= 15, {
      message: 'Pixabay API key must be at least 15 characters',
    }),
  unsplash: z
    .string()
    .optional()
    .refine((val: string | undefined) => !val || val.length === 0 || val.length >= 30, {
      message: 'Unsplash API key must be at least 30 characters',
    }),
  stabilityai: z
    .string()
    .optional()
    .refine(
      (val: string | undefined) =>
        !val || val.length === 0 || (val.startsWith('sk-') && val.length > 20),
      {
        message: 'Stability AI API key must start with "sk-" and be at least 20 characters',
      }
    ),
});

/**
 * Provider paths validation schema
 */
export const providerPathsSchema = z.object({
  stableDiffusionUrl: z
    .string()
    .optional()
    .refine(
      (val: string | undefined) => !val || val.length === 0 || /^https?:\/\/.+:\d+/.test(val),
      {
        message: 'Must be a valid URL with protocol and port (e.g., http://127.0.0.1:7860)',
      }
    ),
  ollamaUrl: z
    .string()
    .optional()
    .refine(
      (val: string | undefined) => !val || val.length === 0 || /^https?:\/\/.+:\d+/.test(val),
      {
        message: 'Must be a valid URL with protocol and port (e.g., http://127.0.0.1:11434)',
      }
    ),
  ffmpegPath: z
    .string()
    .optional()
    .refine((val: string | undefined) => !val || val.length === 0 || !val.includes('..'), {
      message: 'Path cannot contain directory traversal sequences (..)',
    }),
  ffprobePath: z
    .string()
    .optional()
    .refine((val: string | undefined) => !val || val.length === 0 || !val.includes('..'), {
      message: 'Path cannot contain directory traversal sequences (..)',
    }),
  outputDirectory: z
    .string()
    .optional()
    .refine((val: string | undefined) => !val || val.length === 0 || !val.includes('..'), {
      message: 'Path cannot contain directory traversal sequences (..)',
    }),
});

/**
 * Security validation helpers
 */
export const securityValidators = {
  /**
   * Checks for XSS patterns in user input
   */
  noXss: (message = 'Input contains potentially dangerous content') =>
    z.string().refine(
      (val: string) => {
        const xssPatterns = [
          /<script[^>]*>/i,
          /javascript:/i,
          /on\w+\s*=/i,
          /<iframe/i,
          /<object/i,
          /<embed/i,
        ];
        return !xssPatterns.some((pattern) => pattern.test(val));
      },
      { message }
    ),

  /**
   * Checks for prompt injection attempts
   */
  noPromptInjection: (message = 'Input contains prompt injection attempt') =>
    z.string().refine(
      (val: string) => {
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
        ];
        return !injectionPatterns.some((pattern) => pattern.test(val));
      },
      { message }
    ),

  /**
   * Validates path doesn't contain traversal attempts
   */
  noPathTraversal: (message = 'Path contains directory traversal attempt') =>
    z.string().refine((val: string) => !val.includes('..'), { message }),

  /**
   * Validates file extension is in allowed list
   */
  allowedExtension: (extensions: string[], message?: string) =>
    z.string().refine(
      (val: string) => {
        const ext = val.split('.').pop()?.toLowerCase();
        return ext && extensions.includes(`.${ext}`);
      },
      {
        message: message || `File must be one of: ${extensions.join(', ')}`,
      }
    ),

  /**
   * Removes control characters except whitespace
   */
  noControlChars: (message = 'Input contains invalid control characters') =>
    z.string().refine(
      (val: string) => {
        // Allow common whitespace: space, tab, newline, carriage return
        for (const char of val) {
          const code = char.charCodeAt(0);
          // Control chars are 0-31 and 127-159
          // Allow: 9 (tab), 10 (newline), 13 (carriage return), 32+ (printable)
          if (
            (code < 32 && code !== 9 && code !== 10 && code !== 13) ||
            (code >= 127 && code <= 159)
          ) {
            return false;
          }
        }
        return true;
      },
      { message }
    ),
};

/**
 * Enhanced brief validation with security checks
 */
export const secureBriefSchema = z.object({
  topic: z
    .string()
    .min(3, 'Topic must be at least 3 characters')
    .max(10000, 'Topic must not exceed 10,000 characters')
    .pipe(securityValidators.noXss())
    .pipe(securityValidators.noPromptInjection())
    .pipe(securityValidators.noControlChars()),
  audience: z
    .string()
    .optional()
    .refine((val: string | undefined) => !val || val.length <= 200, {
      message: 'Audience must not exceed 200 characters',
    }),
  goal: z
    .string()
    .optional()
    .refine((val: string | undefined) => !val || val.length <= 300, {
      message: 'Goal must not exceed 300 characters',
    }),
  tone: z
    .string()
    .optional()
    .refine((val: string | undefined) => !val || val.length <= 100, {
      message: 'Tone must not exceed 100 characters',
    }),
  language: z.string().optional(),
  durationMinutes: z
    .number()
    .min(10 / 60, 'Duration must be at least 10 seconds')
    .max(120, 'Duration must be no more than 120 minutes'),
});

/**
 * Script text validation with security checks
 */
export const scriptTextValidator = z
  .string()
  .max(50000, 'Script text must not exceed 50,000 characters per scene')
  .pipe(securityValidators.noXss())
  .pipe(securityValidators.noControlChars());

/**
 * File upload validation with allowed extensions
 */
export const allowedFileExtensions = [
  '.mp4',
  '.mp3',
  '.wav',
  '.jpg',
  '.jpeg',
  '.png',
  '.json',
  '.srt',
  '.vtt',
  '.ass',
  '.ssa',
];

export function validateFileUpload(
  file: File,
  options?: {
    maxSizeMB?: number;
    allowedExtensions?: string[];
  }
): { valid: boolean; error?: string } {
  const maxSize = (options?.maxSizeMB || 50) * 1024 * 1024;
  const extensions = options?.allowedExtensions || allowedFileExtensions;

  // Check size
  if (file.size > maxSize) {
    return {
      valid: false,
      error: `File size must be less than ${options?.maxSizeMB || 50}MB`,
    };
  }

  // Check extension
  const fileName = file.name.toLowerCase();
  const hasValidExtension = extensions.some((ext) => fileName.endsWith(ext));

  if (!hasValidExtension) {
    return {
      valid: false,
      error: `File type must be one of: ${extensions.join(', ')}`,
    };
  }

  // Check for path traversal in filename
  if (file.name.includes('..') || file.name.includes('/') || file.name.includes('\\')) {
    return {
      valid: false,
      error: 'Invalid filename',
    };
  }

  return { valid: true };
}

/**
 * Sanitize text for display (basic HTML encoding)
 */
export function sanitizeText(text: string): string {
  const div = document.createElement('div');
  div.textContent = text;
  return div.innerHTML;
}

/**
 * Character counter helper for forms
 */
export function createCharCounter(maxLength: number) {
  return {
    validate: (value: string): { valid: boolean; remaining: number; percentage: number } => {
      const length = value.length;
      const remaining = maxLength - length;
      const percentage = (length / maxLength) * 100;
      return {
        valid: length <= maxLength,
        remaining,
        percentage,
      };
    },
    getMessage: (value: string): string => {
      const { remaining, valid } = createCharCounter(maxLength).validate(value);
      if (!valid) {
        return `Exceeds limit by ${Math.abs(remaining)} characters`;
      }
      if (remaining < maxLength * 0.1) {
        return `${remaining} characters remaining`;
      }
      return `${value.length} / ${maxLength} characters`;
    },
  };
}
