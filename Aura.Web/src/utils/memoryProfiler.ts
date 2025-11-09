/**
 * Memory Profiling Utility
 *
 * Provides memory leak detection and profiling capabilities in development mode.
 * Exposes window.__AURA_MEMORY_REPORT__ for manual inspection.
 */

interface ComponentStats {
  mountCount: number;
  unmountCount: number;
  activeInstances: number;
  cleanupCallbackCount: number;
}

interface MemoryReport {
  components: Map<string, ComponentStats>;
  blobUrlCount: number;
  totalMountCount: number;
  totalUnmountCount: number;
  activeComponentInstances: number;
  timestamp: Date;
}

class MemoryProfiler {
  private components = new Map<string, ComponentStats>();
  private enabled = import.meta.env.DEV;

  /**
   * Track component mount
   */
  public trackMount(componentName: string): void {
    if (!this.enabled) return;

    const stats = this.getOrCreateStats(componentName);
    stats.mountCount++;
    stats.activeInstances++;
  }

  /**
   * Track component unmount
   */
  public trackUnmount(componentName: string): void {
    if (!this.enabled) return;

    const stats = this.getOrCreateStats(componentName);
    stats.unmountCount++;
    stats.activeInstances = Math.max(0, stats.activeInstances - 1);
  }

  /**
   * Track cleanup callback registration
   */
  public trackCleanupCallback(componentName: string): void {
    if (!this.enabled) return;

    const stats = this.getOrCreateStats(componentName);
    stats.cleanupCallbackCount++;
  }

  /**
   * Get current memory report
   */
  public getReport(): MemoryReport {
    const blobCount =
      (window as typeof window & { __AURA_BLOB_COUNT__?: number }).__AURA_BLOB_COUNT__ || 0;

    let totalMountCount = 0;
    let totalUnmountCount = 0;
    let activeComponentInstances = 0;

    this.components.forEach((stats) => {
      totalMountCount += stats.mountCount;
      totalUnmountCount += stats.unmountCount;
      activeComponentInstances += stats.activeInstances;
    });

    return {
      components: new Map(this.components),
      blobUrlCount: blobCount,
      totalMountCount,
      totalUnmountCount,
      activeComponentInstances,
      timestamp: new Date(),
    };
  }

  /**
   * Print report to console
   */
  public printReport(): void {
    if (!this.enabled) {
      // eslint-disable-next-line no-console
      console.log('Memory profiling only available in development mode');
      return;
    }

    const report = this.getReport();

    // eslint-disable-next-line no-console
    console.group('🧠 Aura Memory Report');
    // eslint-disable-next-line no-console
    console.log('Timestamp:', report.timestamp.toISOString());
    // eslint-disable-next-line no-console
    console.log('Active Blob URLs:', report.blobUrlCount);
    // eslint-disable-next-line no-console
    console.log('Active Component Instances:', report.activeComponentInstances);
    // eslint-disable-next-line no-console
    console.log('Total Mounts:', report.totalMountCount);
    // eslint-disable-next-line no-console
    console.log('Total Unmounts:', report.totalUnmountCount);

    // eslint-disable-next-line no-console
    console.group('Components');
    const sortedComponents = Array.from(report.components.entries()).sort(
      (a, b) => b[1].activeInstances - a[1].activeInstances
    );

    sortedComponents.forEach(([name, stats]) => {
      console.log(
        `${name}: ${stats.activeInstances} active (${stats.mountCount} mounts, ${stats.unmountCount} unmounts, ${stats.cleanupCallbackCount} cleanups)`
      );
    });
    // eslint-disable-next-line no-console
    console.groupEnd();

    // Warnings
    const warnings: string[] = [];

    if (report.blobUrlCount > 50) {
      warnings.push(`⚠️ High Blob URL count: ${report.blobUrlCount} (potential leak)`);
    }

    if (report.activeComponentInstances > 100) {
      warnings.push(
        `⚠️ High component instance count: ${report.activeComponentInstances} (potential leak)`
      );
    }

    sortedComponents.forEach(([name, stats]) => {
      if (stats.mountCount - stats.unmountCount > 10) {
        warnings.push(
          `⚠️ Component ${name} has ${
            stats.mountCount - stats.unmountCount
          } more mounts than unmounts (potential leak)`
        );
      }
    });

    if (warnings.length > 0) {
      console.group('⚠️ Warnings'); // eslint-disable-line no-console
      warnings.forEach((warning) => console.warn(warning));
      console.groupEnd(); // eslint-disable-line no-console
    }

    // eslint-disable-next-line no-console
    console.groupEnd();
  }

  /**
   * Reset all stats
   */
  public reset(): void {
    this.components.clear();
    if (typeof window !== 'undefined') {
      (window as typeof window & { __AURA_BLOB_COUNT__?: number }).__AURA_BLOB_COUNT__ = 0;
    }
  }

  private getOrCreateStats(componentName: string): ComponentStats {
    if (!this.components.has(componentName)) {
      this.components.set(componentName, {
        mountCount: 0,
        unmountCount: 0,
        activeInstances: 0,
        cleanupCallbackCount: 0,
      });
    }
    return this.components.get(componentName)!;
  }
}

// Export singleton
export const memoryProfiler = new MemoryProfiler();

// Expose to window in dev mode
if (import.meta.env.DEV && typeof window !== 'undefined') {
  (window as typeof window & { __AURA_MEMORY_REPORT__?: () => void }).__AURA_MEMORY_REPORT__ = () =>
    memoryProfiler.printReport();
  (window as typeof window & { __AURA_BLOB_COUNT__?: number }).__AURA_BLOB_COUNT__ = 0;

  // eslint-disable-next-line no-console
  console.log('💡 Memory profiling enabled. Run window.__AURA_MEMORY_REPORT__() to view report.');
}

/**
 * Hook to track component lifecycle for memory profiling
 *
 * @example
 * ```tsx
 * const MyComponent = () => {
 *   useMemoryProfiler('MyComponent');
 *   return <div>...</div>;
 * };
 * ```
 */
/**
 * Hook to track component lifecycle for memory profiling
 *
 * @example
 * ```tsx
 * const MyComponent = () => {
 *   useMemoryProfiler('MyComponent');
 *   return <div>...</div>;
 * };
 * ```
 */
export function useMemoryProfiler(componentName: string): void {
  useEffect(() => {
    if (!import.meta.env.DEV) return;

    memoryProfiler.trackMount(componentName);

    return () => {
      memoryProfiler.trackUnmount(componentName);
    };
  }, [componentName]);
}

// Import for the hook
import { useEffect } from 'react';
