import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { fileURLToPath, URL } from 'node:url';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

// https://vite.dev/config/
export default defineConfig({
  // Store Vite's dep-optimization cache in the OS temp directory so it is
  // never inside a Dropbox (or OneDrive) folder, which causes EBUSY lock
  // errors when the sync client races against Vite's atomic rename.
  cacheDir: join(tmpdir(), 'vite-cache', 'theragraf-web'),
  plugins: [react()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    port: 5173,
    proxy: {
      // In local dev, forward /api/* to the Function App running on port 7071.
      '/api': {
        target: 'http://localhost:7071',
        changeOrigin: true,
      },
    },
  },
});
