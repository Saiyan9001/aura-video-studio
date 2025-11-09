/**
 * Memory Leak Detector
 *
 * Monitors for common memory leak patterns in development mode
 * and provides warnings in the console.
 */

interface LeakPattern {
  name: string;
  check: () => { detected: boolean; details: string };
  threshold: number;
}

class MemoryLeakDetector {
  private checkInterval: number | null = null;
  private enabled = import.meta.env.DEV;
  private checkIntervalMs = 30000; // 30 seconds

  private patterns: LeakPattern[] = [
    {
      name: 'Blob URLs',
      check: () => {
        const count =
          (window as typeof window & { __AURA_BLOB_COUNT__?: number }).__AURA_BLOB_COUNT__ || 0;
        return {
          detected: count > 50,
          details: `${count} active Blob URLs`,
        };
      },
      threshold: 50,
    },
    {
      name: 'DOM Event Listeners',
      check: () => {
        // This is approximate - we can't directly count all event listeners
        // but we can check for common patterns
        const bodyListeners = (
          window.document.body as typeof window.document.body & {
            __eventListeners?: Record<string, unknown[]>;
          }
        ).__eventListeners;

        if (bodyListeners) {
          const count = Object.values(bodyListeners).reduce(
            (sum, listeners) => sum + listeners.length,
            0
          );
          return {
            detected: count > 100,
            details: `Approximately ${count} listeners on body element`,
          };
        }
        return { detected: false, details: '' };
      },
      threshold: 100,
    },
    {
      name: 'Timers',
      check: () => {
        // Check for runaway setInterval calls
        // This is a heuristic based on the timer ID counter
        const highestTimerId = setTimeout(() => {}, 0);
        clearTimeout(highestTimerId);

        return {
          detected: highestTimerId > 10000,
          details: `Highest timer ID: ${highestTimerId} (indicates many timers created)`,
        };
      },
      threshold: 10000,
    },
  ];

  /**
   * Start monitoring for memory leaks
   */
  public start(): void {
    if (!this.enabled || this.checkInterval !== null) return;

    // eslint-disable-next-line no-console
    console.log('🔍 Memory leak detector started (checking every 30 seconds)');

    this.checkInterval = window.setInterval(() => {
      this.checkForLeaks();
    }, this.checkIntervalMs);
  }

  /**
   * Stop monitoring
   */
  public stop(): void {
    if (this.checkInterval !== null) {
      clearInterval(this.checkInterval);
      this.checkInterval = null;
    }
  }

  /**
   * Check for leak patterns
   */
  public checkForLeaks(): void {
    if (!this.enabled) return;

    const detectedLeaks: Array<{ pattern: string; details: string }> = [];

    this.patterns.forEach((pattern) => {
      const result = pattern.check();
      if (result.detected) {
        detectedLeaks.push({
          pattern: pattern.name,
          details: result.details,
        });
      }
    });

    if (detectedLeaks.length > 0) {
      // eslint-disable-next-line no-console
      console.group('⚠️ Potential Memory Leaks Detected');
      detectedLeaks.forEach(({ pattern, details }) => {
        console.warn(`${pattern}: ${details}`);
      });
      console.log('Run window.__AURA_MEMORY_REPORT__() for more details'); // eslint-disable-line no-console
      // eslint-disable-next-line no-console
      console.groupEnd();
    }
  }

  /**
   * Add a custom leak pattern
   */
  public addPattern(pattern: LeakPattern): void {
    this.patterns.push(pattern);
  }
}

// Export singleton
export const memoryLeakDetector = new MemoryLeakDetector();

// Auto-start in development mode
if (import.meta.env.DEV && typeof window !== 'undefined') {
  // Start after a delay to let the app initialize
  setTimeout(() => {
    memoryLeakDetector.start();
  }, 5000);

  // Stop on page unload
  window.addEventListener('beforeunload', () => {
    memoryLeakDetector.stop();
  });
}
