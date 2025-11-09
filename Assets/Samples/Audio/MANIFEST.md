# Sample Audio Manifest

This file describes the sample audio tracks that should be included in the Assets/Samples/Audio directory.

## Audio Specifications

All sample audio should be:
- CC0 (Public Domain) or CC-BY license
- High quality (at least 128kbps MP3 or WAV)
- Suitable for background music in videos
- Royalty-free with commercial use allowed
- Loop-friendly (can seamlessly repeat)

## Recommended Sample Audio Tracks

### 1. sample-upbeat-01.mp3
- **Description**: Upbeat, energetic background music
- **Duration**: 30-60 seconds (loopable)
- **Tempo**: 120-140 BPM
- **Mood**: Energetic, motivational
- **Use Case**: Marketing, product demos, social media
- **License**: CC0 Public Domain
- **Source**: Placeholder (can be replaced with actual CC0 music from FreeMusicArchive or ccMixter)

### 2. sample-ambient-01.mp3
- **Description**: Calm ambient background
- **Duration**: 30-60 seconds (loopable)
- **Tempo**: Slow, atmospheric
- **Mood**: Calm, peaceful
- **Use Case**: Tutorials, educational content
- **License**: CC0 Public Domain
- **Source**: Placeholder (can be replaced with actual CC0 music)

### 3. sample-corporate-01.mp3
- **Description**: Professional corporate background
- **Duration**: 30-60 seconds (loopable)
- **Tempo**: 100-110 BPM
- **Mood**: Professional, confident
- **Use Case**: Business presentations, corporate videos
- **License**: CC0 Public Domain
- **Source**: Placeholder (can be replaced with actual CC0 music)

### 4. sample-inspiring-01.mp3
- **Description**: Inspiring, uplifting music
- **Duration**: 30-60 seconds (loopable)
- **Tempo**: 100-120 BPM
- **Mood**: Inspiring, hopeful
- **Use Case**: Storytelling, testimonials, inspirational content
- **License**: CC0 Public Domain
- **Source**: Placeholder (can be replaced with actual CC0 music)

### 5. sample-tech-01.mp3
- **Description**: Modern tech/electronic background
- **Duration**: 30-60 seconds (loopable)
- **Tempo**: 110-130 BPM
- **Mood**: Modern, innovative
- **Use Case**: Tech demos, app showcases
- **License**: CC0 Public Domain
- **Source**: Placeholder (can be replaced with actual CC0 music)

## Audio Sources

Recommended sources for CC0/CC-BY music:
- **Free Music Archive** (https://freemusicarchive.org/)
- **ccMixter** (http://ccmixter.org/)
- **Incompetech** (https://incompetech.com/music/royalty-free/)
- **Purple Planet** (https://www.purple-planet.com/)
- **Bensound** (https://www.bensound.com/royalty-free-music)

## Implementation Notes

For the initial implementation, sample audio files can be:
1. Generated silence with appropriate metadata
2. Simple tone sequences for testing
3. Replaced with actual CC0 music in production builds

The SampleAssets.cs service will handle loading and serving these audio files. Audio files should be stored as MP3 (for smaller size) or WAV (for higher quality) format.

## Format Guidelines

- **Sample Rate**: 44.1kHz or 48kHz
- **Bit Rate**: Minimum 128kbps for MP3
- **Channels**: Stereo
- **Format**: MP3 (preferred) or WAV
- **File Size**: Target 1-5MB per track
