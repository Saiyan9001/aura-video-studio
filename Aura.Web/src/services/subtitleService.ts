export interface SubtitleCue {
  startTime: number;
  endTime: number;
  text: string;
}

export interface Subtitle {
  sceneIndex: number;
  text: string;
  startTime: number;
  duration: number;
}

export class SubtitleService {
  generateSubtitles(scenes: Subtitle[]): SubtitleCue[] {
    return scenes.map((scene) => ({
      startTime: scene.startTime,
      endTime: scene.startTime + scene.duration,
      text: scene.text,
    }));
  }

  exportToSRT(cues: SubtitleCue[]): string {
    return cues
      .map((cue, index) => {
        const startTime = this.formatSRTTime(cue.startTime);
        const endTime = this.formatSRTTime(cue.endTime);
        return `${index + 1}\n${startTime} --> ${endTime}\n${cue.text}\n`;
      })
      .join('\n');
  }

  exportToVTT(cues: SubtitleCue[]): string {
    const header = 'WEBVTT\n\n';
    const body = cues
      .map((cue, index) => {
        const startTime = this.formatVTTTime(cue.startTime);
        const endTime = this.formatVTTTime(cue.endTime);
        return `${index + 1}\n${startTime} --> ${endTime}\n${cue.text}\n`;
      })
      .join('\n');
    return header + body;
  }

  private formatSRTTime(seconds: number): string {
    const hours = Math.floor(seconds / 3600);
    const minutes = Math.floor((seconds % 3600) / 60);
    const secs = Math.floor(seconds % 60);
    const millis = Math.floor((seconds % 1) * 1000);

    return `${this.pad(hours, 2)}:${this.pad(minutes, 2)}:${this.pad(secs, 2)},${this.pad(millis, 3)}`;
  }

  private formatVTTTime(seconds: number): string {
    const hours = Math.floor(seconds / 3600);
    const minutes = Math.floor((seconds % 3600) / 60);
    const secs = Math.floor(seconds % 60);
    const millis = Math.floor((seconds % 1) * 1000);

    return `${this.pad(hours, 2)}:${this.pad(minutes, 2)}:${this.pad(secs, 2)}.${this.pad(millis, 3)}`;
  }

  private pad(num: number, size: number): string {
    let s = String(num);
    while (s.length < size) {
      s = '0' + s;
    }
    return s;
  }

  downloadSubtitles(content: string, filename: string): void {
    const blob = new Blob([content], { type: 'text/plain;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = filename;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
  }
}

export const subtitleService = new SubtitleService();
