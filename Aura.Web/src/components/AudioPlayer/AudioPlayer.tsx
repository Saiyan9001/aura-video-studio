import { makeStyles, tokens, Button, Slider, Label, Tooltip } from '@fluentui/react-components';
import {
  PlayRegular,
  PauseRegular,
  SpeakerMuteRegular,
  Speaker2Regular,
} from '@fluentui/react-icons';
import { useEffect, useRef, useState, useCallback } from 'react';
import type { FC } from 'react';
import WaveSurfer from 'wavesurfer.js';

interface AudioPlayerProps {
  audioUrl: string;
  sceneIndex?: number;
  onPlaybackComplete?: () => void;
  showWaveform?: boolean;
  autoPlay?: boolean;
}

const useStyles = makeStyles({
  container: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
    padding: tokens.spacingVerticalM,
    backgroundColor: tokens.colorNeutralBackground2,
    borderRadius: tokens.borderRadiusMedium,
  },
  waveformContainer: {
    width: '100%',
    minHeight: '80px',
    backgroundColor: tokens.colorNeutralBackground1,
    borderRadius: tokens.borderRadiusSmall,
  },
  controls: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalM,
  },
  playButton: {
    minWidth: '44px',
  },
  timeDisplay: {
    fontSize: tokens.fontSizeBase200,
    color: tokens.colorNeutralForeground2,
    minWidth: '100px',
    textAlign: 'center',
  },
  sliderContainer: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
    flex: 1,
  },
  volumeControl: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
    minWidth: '150px',
  },
  volumeSlider: {
    flex: 1,
  },
  speedControl: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
  },
});

const formatTime = (seconds: number): string => {
  const mins = Math.floor(seconds / 60);
  const secs = Math.floor(seconds % 60);
  return `${mins}:${secs.toString().padStart(2, '0')}`;
};

export const AudioPlayer: FC<AudioPlayerProps> = ({
  audioUrl,
  sceneIndex,
  onPlaybackComplete,
  showWaveform = true,
  autoPlay = false,
}) => {
  const styles = useStyles();
  const waveformRef = useRef<HTMLDivElement>(null);
  const wavesurferRef = useRef<WaveSurfer | null>(null);
  const [isPlaying, setIsPlaying] = useState(false);
  const [currentTime, setCurrentTime] = useState(0);
  const [duration, setDuration] = useState(0);
  const [volume, setVolume] = useState(1);
  const [isMuted, setIsMuted] = useState(false);
  const [playbackSpeed, setPlaybackSpeed] = useState(1);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    if (!waveformRef.current || !showWaveform) {
      return;
    }

    setIsLoading(true);

    const wavesurfer = WaveSurfer.create({
      container: waveformRef.current,
      waveColor: tokens.colorBrandBackground,
      progressColor: tokens.colorBrandBackgroundHover,
      cursorColor: tokens.colorNeutralForeground1,
      barWidth: 2,
      barGap: 1,
      height: 80,
      normalize: true,
      backend: 'WebAudio',
    });

    wavesurferRef.current = wavesurfer;

    wavesurfer.load(audioUrl);

    wavesurfer.on('ready', () => {
      setDuration(wavesurfer.getDuration());
      setIsLoading(false);
      if (autoPlay) {
        wavesurfer.play();
        setIsPlaying(true);
      }
    });

    wavesurfer.on('audioprocess', () => {
      setCurrentTime(wavesurfer.getCurrentTime());
    });

    wavesurfer.on('finish', () => {
      setIsPlaying(false);
      setCurrentTime(0);
      if (onPlaybackComplete) {
        onPlaybackComplete();
      }
    });

    wavesurfer.on('error', (error) => {
      console.error('WaveSurfer error:', error);
      setIsLoading(false);
    });

    return () => {
      wavesurfer.destroy();
    };
  }, [audioUrl, showWaveform, autoPlay, onPlaybackComplete]);

  const handlePlayPause = useCallback(() => {
    if (!wavesurferRef.current) return;

    if (isPlaying) {
      wavesurferRef.current.pause();
    } else {
      wavesurferRef.current.play();
    }
    setIsPlaying(!isPlaying);
  }, [isPlaying]);

  const handleSeek = useCallback(
    (_ev: unknown, data: { value: number }) => {
      if (!wavesurferRef.current || !duration) return;
      const seekTime = (data.value / 100) * duration;
      wavesurferRef.current.seekTo(seekTime / duration);
      setCurrentTime(seekTime);
    },
    [duration]
  );

  const handleVolumeChange = useCallback(
    (_ev: unknown, data: { value: number }) => {
      const newVolume = data.value / 100;
      setVolume(newVolume);
      if (wavesurferRef.current) {
        wavesurferRef.current.setVolume(newVolume);
      }
      if (newVolume > 0 && isMuted) {
        setIsMuted(false);
      }
    },
    [isMuted]
  );

  const handleMuteToggle = useCallback(() => {
    if (!wavesurferRef.current) return;

    if (isMuted) {
      wavesurferRef.current.setVolume(volume);
      setIsMuted(false);
    } else {
      wavesurferRef.current.setVolume(0);
      setIsMuted(true);
    }
  }, [isMuted, volume]);

  const handleSpeedChange = useCallback((_ev: unknown, data: { value: number }) => {
    const newSpeed = data.value;
    setPlaybackSpeed(newSpeed);
    if (wavesurferRef.current) {
      wavesurferRef.current.setPlaybackRate(newSpeed);
    }
  }, []);

  const progressPercentage = duration > 0 ? (currentTime / duration) * 100 : 0;

  return (
    <div className={styles.container}>
      {sceneIndex !== undefined && <Label weight="semibold">Scene {sceneIndex + 1} Audio</Label>}

      {showWaveform && (
        <div ref={waveformRef} className={styles.waveformContainer}>
          {isLoading && (
            <div style={{ padding: '30px', textAlign: 'center' }}>Loading audio...</div>
          )}
        </div>
      )}

      <div className={styles.controls}>
        <Tooltip content={isPlaying ? 'Pause' : 'Play'} relationship="label">
          <Button
            appearance="primary"
            icon={isPlaying ? <PauseRegular /> : <PlayRegular />}
            onClick={handlePlayPause}
            disabled={isLoading}
            className={styles.playButton}
          />
        </Tooltip>

        <div className={styles.timeDisplay}>
          {formatTime(currentTime)} / {formatTime(duration)}
        </div>

        <div className={styles.sliderContainer}>
          <Slider
            value={progressPercentage}
            onChange={handleSeek}
            min={0}
            max={100}
            step={0.1}
            disabled={isLoading}
          />
        </div>

        <div className={styles.volumeControl}>
          <Tooltip content={isMuted ? 'Unmute' : 'Mute'} relationship="label">
            <Button
              appearance="subtle"
              icon={isMuted ? <SpeakerMuteRegular /> : <Speaker2Regular />}
              onClick={handleMuteToggle}
              disabled={isLoading}
            />
          </Tooltip>
          <Slider
            value={isMuted ? 0 : volume * 100}
            onChange={handleVolumeChange}
            min={0}
            max={100}
            step={1}
            disabled={isLoading}
            className={styles.volumeSlider}
          />
        </div>

        <div className={styles.speedControl}>
          <Label size="small">Speed:</Label>
          <Slider
            value={playbackSpeed}
            onChange={handleSpeedChange}
            min={0.5}
            max={2}
            step={0.25}
            disabled={isLoading}
            style={{ minWidth: '100px' }}
          />
          <Label size="small">{playbackSpeed}x</Label>
        </div>
      </div>
    </div>
  );
};
