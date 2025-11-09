import { defineConfig, Plugin } from 'vite';
import react from '@vitejs/plugin-react';
import path from 'path';
import { visualizer } from 'rollup-plugin-visualizer';
import viteCompression from 'vite-plugin-compression';

/**
 * Performance Budget Plugin
 * Warns when bundle sizes exceed configured limits
 */
function performanceBudgetPlugin(): Plugin {
  // Performance budgets in KB
  const budgets = {
    'react-vendor': 200,
    'fluentui-components': 250,
    'fluentui-icons': 200,
    'ffmpeg-vendor': 500,
    'audio-vendor': 100,
    vendor: 300,
    total: 1500, // Total bundle size budget
  };

  return {
    name: 'performance-budget',
    enforce: 'post',
    writeBundle(options, bundle) {
      const chunks: { [key: string]: number } = {};
      let totalSize = 0;

      // Calculate chunk sizes
      Object.entries(bundle).forEach(([fileName, chunk]) => {
        if (chunk.type === 'chunk' && 'code' in chunk) {
          const size = chunk.code.length / 1024; // Convert to KB
          totalSize += size;

          // Extract chunk name
          const chunkName = fileName.replace(/^assets\//, '').replace(/-[a-f0-9]+\.js$/, '');
          chunks[chunkName] = (chunks[chunkName] || 0) + size;
        }
      });

      // Check budgets
      console.log('\n📊 Performance Budget Report:\n');
      let hasViolations = false;

      Object.entries(chunks).forEach(([name, size]) => {
        const budget = budgets[name];
        if (budget && size > budget) {
          console.warn(`⚠️  ${name}: ${size.toFixed(2)}KB exceeds budget of ${budget}KB`);
          hasViolations = true;
        } else if (budget) {
          console.log(`✅ ${name}: ${size.toFixed(2)}KB (budget: ${budget}KB)`);
        }
      });

      // Check total size
      if (totalSize > budgets.total) {
        console.warn(
          `⚠️  Total bundle size: ${totalSize.toFixed(2)}KB exceeds budget of ${budgets.total}KB`
        );
        hasViolations = true;
      } else {
        console.log(`✅ Total bundle size: ${totalSize.toFixed(2)}KB (budget: ${budgets.total}KB)`);
      }

      if (hasViolations) {
        console.warn(
          '\n⚠️  Performance budget violations detected! Consider code splitting or removing unused dependencies.\n'
        );
      } else {
        console.log('\n✅ All performance budgets met!\n');
      }
    },
  };
}

// https://vitejs.dev/config/
export default defineConfig(({ mode }) => {
  const isProduction = mode === 'production';

  return {
    plugins: [
      react(),
      // Generate bundle analysis report
      visualizer({
        filename: './dist/stats.html',
        open: false,
        gzipSize: true,
        brotliSize: true,
      }),
      // Compress assets with gzip and brotli
      viteCompression({
        algorithm: 'gzip',
        ext: '.gz',
        threshold: 1024, // Only compress files larger than 1KB
      }),
      viteCompression({
        algorithm: 'brotliCompress',
        ext: '.br',
        threshold: 1024,
      }),
      // Performance budget plugin
      performanceBudgetPlugin(),
    ],
    // Use relative base path for production to work when served from Aura.Api
    base: '/',
    resolve: {
      alias: {
        '@': path.resolve(__dirname, './src'),
      },
    },
    server: {
      port: 5173,
      open: true,
      proxy: {
        '/api': {
          target: 'http://127.0.0.1:5005',
          changeOrigin: true,
        },
      },
    },
    build: {
      outDir: 'dist',
      // Only generate source maps in development or as hidden source maps in production
      sourcemap: isProduction ? 'hidden' : true,
      // Increase warning limit but maintain awareness of large chunks
      chunkSizeWarningLimit: 600,
      emptyOutDir: true,
      assetsDir: 'assets',
      // Enable minification with terser for better compression
      minify: 'terser',
      terserOptions: {
        compress: {
          drop_console: isProduction, // Remove console.logs in production
          drop_debugger: isProduction, // Remove debugger statements in production
          pure_funcs: isProduction ? ['console.log', 'console.info', 'console.debug'] : [],
          passes: 2, // Multiple passes for better optimization
        },
        mangle: {
          safari10: true, // Support Safari 10+
        },
      },
      rollupOptions: {
        output: {
          entryFileNames: 'assets/[name]-[hash].js',
          chunkFileNames: 'assets/[name]-[hash].js',
          assetFileNames: 'assets/[name]-[hash].[ext]',
          // Improved code splitting strategy with route-based chunks
          manualChunks: (id) => {
            // FFmpeg - large library, keep separate
            if (id.includes('@ffmpeg')) {
              return 'ffmpeg-vendor';
            }
            // Wavesurfer - audio visualization
            if (id.includes('wavesurfer')) {
              return 'audio-vendor';
            }
            // React core - separate chunk
            if (id.includes('node_modules/react/') || id.includes('node_modules/react-dom/')) {
              return 'react-vendor';
            }
            // Fluent UI Components - separate from icons to avoid circular dependencies
            // This includes all @fluentui/react-* packages except react-icons
            if (id.includes('@fluentui/react-') && !id.includes('@fluentui/react-icons')) {
              return 'fluentui-components';
            }
            // Fluent UI Icons - separate chunk
            if (id.includes('@fluentui/react-icons')) {
              return 'fluentui-icons';
            }
            // Router - separate chunk
            if (id.includes('react-router')) {
              return 'router-vendor';
            }
            // State management
            if (id.includes('zustand') || id.includes('@tanstack/react-query')) {
              return 'state-vendor';
            }
            // All other node_modules go to vendor chunk
            if (id.includes('node_modules')) {
              return 'vendor';
            }
          },
        },
        treeshake: {
          moduleSideEffects: 'no-external', // Enable aggressive tree shaking
          propertyReadSideEffects: false,
          tryCatchDeoptimization: false,
        },
      },
      // Asset optimization
      assetsInlineLimit: 4096, // Inline assets smaller than 4KB as base64
      cssCodeSplit: true, // Split CSS into separate files per chunk
      // Optimize dependencies
      commonjsOptions: {
        include: [/node_modules/],
        extensions: ['.js', '.cjs'],
      },
      // Performance optimizations
      reportCompressedSize: isProduction,
    },
    test: {
      globals: true,
      environment: 'jsdom',
      setupFiles: './src/test/setup.ts',
      include: [
        'src/**/*.test.{ts,tsx}',
        'tests/smoke/**/*.test.ts',
        'tests/integration/**/*.test.ts',
      ],
      exclude: ['tests/e2e/**', 'node_modules/**'],
      coverage: {
        provider: 'v8',
        reporter: ['text', 'json', 'html'],
        include: ['src/**/*.{ts,tsx}'],
        exclude: ['src/**/*.test.{ts,tsx}', 'src/test/**'],
        thresholds: {
          lines: 70,
          branches: 70,
          statements: 70,
          perFile: true,
        },
        all: false,
      },
    },
  };
});
