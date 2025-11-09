import { describe, it, expect } from 'vitest';
import { getStatusDisplayText, getStatusAppearance } from '../../services/openAIValidationService';

describe('openAIValidationService', () => {
  describe('getStatusDisplayText', () => {
    it('should return correct text for Valid status', () => {
      const text = getStatusDisplayText('Valid');
      expect(text).toBe('Validated ✓');
    });

    it('should return correct text for Invalid status', () => {
      const text = getStatusDisplayText('Invalid');
      expect(text).toBe('Invalid ✕');
    });

    it('should return correct text for RateLimited status', () => {
      const text = getStatusDisplayText('RateLimited');
      expect(text).toBe('Rate Limited (valid key, retry later)');
    });

    it('should return correct text for PermissionDenied status', () => {
      const text = getStatusDisplayText('PermissionDenied');
      expect(text).toBe('Permission Denied');
    });

    it('should return correct text for ServiceIssue status', () => {
      const text = getStatusDisplayText('ServiceIssue');
      expect(text).toBe('Service Issue (retry later)');
    });

    it('should return correct text for NetworkError status', () => {
      const text = getStatusDisplayText('NetworkError');
      expect(text).toBe('Network Error');
    });

    it('should return correct text for Timeout status', () => {
      const text = getStatusDisplayText('Timeout');
      expect(text).toBe('Timeout');
    });
  });

  describe('getStatusAppearance', () => {
    it('should return success appearance for Valid status', () => {
      const appearance = getStatusAppearance('Valid');
      expect(appearance).toBe('success');
    });

    it('should return danger appearance for Invalid status', () => {
      const appearance = getStatusAppearance('Invalid');
      expect(appearance).toBe('danger');
    });

    it('should return danger appearance for PermissionDenied status', () => {
      const appearance = getStatusAppearance('PermissionDenied');
      expect(appearance).toBe('danger');
    });

    it('should return warning appearance for RateLimited status', () => {
      const appearance = getStatusAppearance('RateLimited');
      expect(appearance).toBe('warning');
    });

    it('should return warning appearance for ServiceIssue status', () => {
      const appearance = getStatusAppearance('ServiceIssue');
      expect(appearance).toBe('warning');
    });

    it('should return subtle appearance for NetworkError status', () => {
      const appearance = getStatusAppearance('NetworkError');
      expect(appearance).toBe('subtle');
    });

    it('should return subtle appearance for Timeout status', () => {
      const appearance = getStatusAppearance('Timeout');
      expect(appearance).toBe('subtle');
    });
  });
});
