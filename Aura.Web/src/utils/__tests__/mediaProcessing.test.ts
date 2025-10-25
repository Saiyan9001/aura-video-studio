/**
 * Tests for media processing utilities
 */

import { describe, it, expect } from 'vitest';
import {
  isSupportedMediaType,
} from '../mediaProcessing';

describe('mediaProcessing', () => {
  describe('isSupportedMediaType', () => {
    it('should accept video files', () => {
      const videoFile = new File([''], 'test.mp4', { type: 'video/mp4' });
      expect(isSupportedMediaType(videoFile)).toBe(true);
    });

    it('should accept audio files', () => {
      const audioFile = new File([''], 'test.mp3', { type: 'audio/mpeg' });
      expect(isSupportedMediaType(audioFile)).toBe(true);
    });

    it('should accept image files', () => {
      const imageFile = new File([''], 'test.jpg', { type: 'image/jpeg' });
      expect(isSupportedMediaType(imageFile)).toBe(true);
    });

    it('should reject unsupported file types', () => {
      const textFile = new File([''], 'test.txt', { type: 'text/plain' });
      expect(isSupportedMediaType(textFile)).toBe(false);
    });

    it('should accept webm video files', () => {
      const webmFile = new File([''], 'test.webm', { type: 'video/webm' });
      expect(isSupportedMediaType(webmFile)).toBe(true);
    });

    it('should accept wav audio files', () => {
      const wavFile = new File([''], 'test.wav', { type: 'audio/wav' });
      expect(isSupportedMediaType(wavFile)).toBe(true);
    });

    it('should accept png image files', () => {
      const pngFile = new File([''], 'test.png', { type: 'image/png' });
      expect(isSupportedMediaType(pngFile)).toBe(true);
    });

    it('should accept gif image files', () => {
      const gifFile = new File([''], 'test.gif', { type: 'image/gif' });
      expect(isSupportedMediaType(gifFile)).toBe(true);
    });

    it('should accept webp image files', () => {
      const webpFile = new File([''], 'test.webp', { type: 'image/webp' });
      expect(isSupportedMediaType(webpFile)).toBe(true);
    });

    it('should accept ogg video files', () => {
      const oggFile = new File([''], 'test.ogg', { type: 'video/ogg' });
      expect(isSupportedMediaType(oggFile)).toBe(true);
    });

    it('should accept quicktime video files', () => {
      const movFile = new File([''], 'test.mov', { type: 'video/quicktime' });
      expect(isSupportedMediaType(movFile)).toBe(true);
    });

    it('should accept aac audio files', () => {
      const aacFile = new File([''], 'test.aac', { type: 'audio/aac' });
      expect(isSupportedMediaType(aacFile)).toBe(true);
    });

    it('should reject pdf files', () => {
      const pdfFile = new File([''], 'test.pdf', { type: 'application/pdf' });
      expect(isSupportedMediaType(pdfFile)).toBe(false);
    });

    it('should reject zip files', () => {
      const zipFile = new File([''], 'test.zip', { type: 'application/zip' });
      expect(isSupportedMediaType(zipFile)).toBe(false);
    });
  });

  // Note: Integration tests for generateVideoThumbnails, generateWaveform, etc.
  // are difficult to unit test due to browser API dependencies (HTMLVideoElement, AudioContext, etc.)
  // These would be better tested as E2E tests or in a browser environment
});

