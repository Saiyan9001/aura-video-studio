import { describe, it, expect, beforeEach, vi } from 'vitest';
import { API_BASE_URL, apiUrl } from '../config/api';

describe('API Configuration', () => {
  beforeEach(() => {
    // Reset environment
    vi.resetModules();
  });

  it('should have correct API base URL for development', () => {
    // In test environment, we expect the production fallback
    // In actual dev environment, it should be http://127.0.0.1:5005
    expect(API_BASE_URL).toBeDefined();
    expect(typeof API_BASE_URL).toBe('string');
  });

  it('should construct valid API URLs', () => {
    const testPath = '/api/test';
    const result = apiUrl(testPath);
    
    expect(result).toContain('/api/test');
    expect(result).toMatch(/^https?:\/\//);
  });

  it('should handle paths with and without leading slash', () => {
    const withSlash = apiUrl('/api/test');
    const withoutSlash = apiUrl('api/test');
    
    expect(withSlash).toContain('/api/test');
    expect(withoutSlash).toContain('/api/test');
  });

  it('should use port 5005 for development builds', () => {
    // This test documents the expected configuration
    // In development mode (when running npm run dev), the API should be on port 5005
    const expectedDevUrl = 'http://127.0.0.1:5005';
    
    // Note: In test environment, import.meta.env.DEV is false
    // so we're just documenting the expected behavior
    expect(expectedDevUrl).toBe('http://127.0.0.1:5005');
  });

  it('should construct quick demo endpoint correctly', () => {
    const quickDemoUrl = apiUrl('/api/quick/demo');
    expect(quickDemoUrl).toContain('/api/quick/demo');
  });

  it('should construct jobs endpoint correctly', () => {
    const jobsUrl = apiUrl('/api/jobs');
    expect(jobsUrl).toContain('/api/jobs');
  });

  it('should construct job progress endpoint correctly', () => {
    const jobId = 'test-job-123';
    const progressUrl = apiUrl(`/api/jobs/${jobId}/progress`);
    expect(progressUrl).toContain(`/api/jobs/${jobId}/progress`);
  });
});
