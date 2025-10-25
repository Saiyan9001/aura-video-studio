/**
 * Media processing utilities for thumbnail and waveform generation
 */

export interface ThumbnailData {
  dataUrl: string;
  timestamp: number;
}

export interface WaveformData {
  peaks: number[];
  duration: number;
}

/**
 * Generates thumbnails for a video file at regular intervals
 * @param file The video file to process
 * @param count Number of thumbnails to generate
 * @returns Array of thumbnail data URLs with timestamps
 */
export async function generateVideoThumbnails(
  file: File,
  count: number = 5
): Promise<ThumbnailData[]> {
  return new Promise((resolve, reject) => {
    const video = document.createElement('video');
    const canvas = document.createElement('canvas');
    const ctx = canvas.getContext('2d');

    if (!ctx) {
      reject(new Error('Failed to get canvas context'));
      return;
    }

    video.preload = 'metadata';
    video.muted = true;

    const thumbnails: ThumbnailData[] = [];
    let currentThumbnail = 0;

    video.onloadedmetadata = () => {
      const duration = video.duration;
      
      // Set canvas size to video dimensions
      canvas.width = 160;
      canvas.height = 90;

      const interval = duration / (count + 1);

      const captureFrame = () => {
        if (currentThumbnail >= count) {
          URL.revokeObjectURL(video.src);
          resolve(thumbnails);
          return;
        }

        const timestamp = interval * (currentThumbnail + 1);
        video.currentTime = timestamp;
      };

      video.onseeked = () => {
        ctx.drawImage(video, 0, 0, canvas.width, canvas.height);
        const dataUrl = canvas.toDataURL('image/jpeg', 0.7);
        
        thumbnails.push({
          dataUrl,
          timestamp: video.currentTime,
        });

        currentThumbnail++;
        captureFrame();
      };

      video.onerror = () => {
        URL.revokeObjectURL(video.src);
        reject(new Error('Failed to load video'));
      };

      captureFrame();
    };

    video.src = URL.createObjectURL(file);
  });
}

/**
 * Extracts audio waveform data from an audio or video file
 * @param file The audio/video file to process
 * @param samples Number of waveform samples to generate
 * @returns Waveform peak data
 */
export async function generateWaveform(
  file: File,
  samples: number = 100
): Promise<WaveformData> {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const audioContext = new (window.AudioContext || (window as any).webkitAudioContext)();
  
  try {
    const arrayBuffer = await file.arrayBuffer();
    const audioBuffer = await audioContext.decodeAudioData(arrayBuffer);
    
    const channelData = audioBuffer.getChannelData(0);
    const peaks: number[] = [];
    const blockSize = Math.floor(channelData.length / samples);
    
    for (let i = 0; i < samples; i++) {
      const start = i * blockSize;
      const end = start + blockSize;
      let peak = 0;
      
      for (let j = start; j < end; j++) {
        const abs = Math.abs(channelData[j]);
        if (abs > peak) {
          peak = abs;
        }
      }
      
      peaks.push(peak);
    }
    
    await audioContext.close();
    
    return {
      peaks,
      duration: audioBuffer.duration,
    };
  } catch (error) {
    await audioContext.close();
    throw new Error(`Failed to generate waveform: ${error instanceof Error ? error.message : 'Unknown error'}`);
  }
}

/**
 * Gets the duration of a media file
 * @param file The media file
 * @returns Duration in seconds
 */
export async function getMediaDuration(file: File): Promise<number> {
  return new Promise((resolve, reject) => {
    const element = file.type.startsWith('video/') 
      ? document.createElement('video')
      : document.createElement('audio');
    
    element.preload = 'metadata';
    element.muted = true;

    element.onloadedmetadata = () => {
      const duration = element.duration;
      URL.revokeObjectURL(element.src);
      resolve(duration);
    };

    element.onerror = () => {
      URL.revokeObjectURL(element.src);
      reject(new Error('Failed to load media file'));
    };

    element.src = URL.createObjectURL(file);
  });
}

/**
 * Validates if a file is a supported media type
 * @param file The file to validate
 * @returns True if supported, false otherwise
 */
export function isSupportedMediaType(file: File): boolean {
  const supportedTypes = [
    // Video formats
    'video/mp4',
    'video/webm',
    'video/ogg',
    'video/quicktime',
    // Audio formats
    'audio/mpeg',
    'audio/mp3',
    'audio/wav',
    'audio/ogg',
    'audio/webm',
    'audio/aac',
    // Image formats
    'image/jpeg',
    'image/jpg',
    'image/png',
    'image/gif',
    'image/webp',
  ];

  return supportedTypes.some(type => file.type === type || file.type.startsWith(type.split('/')[0] + '/'));
}

/**
 * Gets a preview image for a media file
 * @param file The media file
 * @param type The media type
 * @returns Data URL of the preview image
 */
export async function getMediaPreview(file: File, type: 'video' | 'audio' | 'image'): Promise<string | null> {
  if (type === 'image') {
    return new Promise((resolve) => {
      const reader = new FileReader();
      reader.onload = (e) => resolve(e.target?.result as string);
      reader.onerror = () => resolve(null);
      reader.readAsDataURL(file);
    });
  }

  if (type === 'video') {
    try {
      const thumbnails = await generateVideoThumbnails(file, 1);
      return thumbnails[0]?.dataUrl || null;
    } catch {
      return null;
    }
  }

  return null;
}
