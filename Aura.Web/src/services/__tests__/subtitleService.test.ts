import { describe, it, expect } from 'vitest';
import { SubtitleService } from '../subtitleService';

describe('SubtitleService', () => {
  let subtitleService: SubtitleService;

  beforeEach(() => {
    subtitleService = new SubtitleService();
  });

  describe('generateSubtitles', () => {
    it('should generate subtitle cues from scenes', () => {
      const scenes = [
        { sceneIndex: 0, text: 'Hello world', startTime: 0, duration: 2 },
        { sceneIndex: 1, text: 'How are you', startTime: 2, duration: 2 },
      ];

      const cues = subtitleService.generateSubtitles(scenes);

      expect(cues).toHaveLength(2);
      expect(cues[0]).toEqual({
        startTime: 0,
        endTime: 2,
        text: 'Hello world',
      });
      expect(cues[1]).toEqual({
        startTime: 2,
        endTime: 4,
        text: 'How are you',
      });
    });
  });

  describe('exportToSRT', () => {
    it('should format subtitles in SRT format', () => {
      const cues = [
        { startTime: 0, endTime: 2, text: 'Hello world' },
        { startTime: 2, endTime: 4, text: 'How are you' },
      ];

      const srt = subtitleService.exportToSRT(cues);

      expect(srt).toContain('1\n00:00:00,000 --> 00:00:02,000\nHello world');
      expect(srt).toContain('2\n00:00:02,000 --> 00:00:04,000\nHow are you');
    });

    it('should format timestamps with correct hours, minutes, seconds, milliseconds', () => {
      const cues = [{ startTime: 3665.5, endTime: 3667.25, text: 'Test' }];

      const srt = subtitleService.exportToSRT(cues);

      expect(srt).toContain('01:01:05,500 --> 01:01:07,250');
    });
  });

  describe('exportToVTT', () => {
    it('should format subtitles in VTT format with WEBVTT header', () => {
      const cues = [
        { startTime: 0, endTime: 2, text: 'Hello world' },
        { startTime: 2, endTime: 4, text: 'How are you' },
      ];

      const vtt = subtitleService.exportToVTT(cues);

      expect(vtt).toContain('WEBVTT');
      expect(vtt).toContain('1\n00:00:00.000 --> 00:00:02.000\nHello world');
      expect(vtt).toContain('2\n00:00:02.000 --> 00:00:04.000\nHow are you');
    });

    it('should use period separator for milliseconds in VTT', () => {
      const cues = [{ startTime: 1.5, endTime: 2.5, text: 'Test' }];

      const vtt = subtitleService.exportToVTT(cues);

      expect(vtt).toContain('00:00:01.500 --> 00:00:02.500');
    });
  });
});
