import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'
import { VitePWA } from 'vite-plugin-pwa'

// https://vite.dev/config/
export default defineConfig({
  build: {
    outDir: '../CoachFlow.Api/wwwroot',
    emptyOutDir: true,
  },
  server: {
    proxy: {
      '/api': 'http://localhost:5080',
      '/health': 'http://localhost:5080',
    },
  },
  plugins: [
    react(),
    VitePWA({
      registerType: 'autoUpdate',
      includeAssets: ['apple-touch-icon.png'],
      manifest: {
        name: 'CoachFlow',
        short_name: 'CoachFlow',
        description: 'Trainingsplanung für Volleyball',
        lang: 'de',
        theme_color: '#0f766e',
        background_color: '#ffffff',
        display: 'standalone',
        icons: [
          { src: 'icon-192.png', sizes: '192x192', type: 'image/png' },
          { src: 'icon-512.png', sizes: '512x512', type: 'image/png' },
        ],
      },
      workbox: {
        // API-Antworten werden nicht pauschal gecacht; die Lesekopie der Pläne folgt separat.
        navigateFallback: 'index.html',
        navigateFallbackDenylist: [/^\/api\//, /^\/health\//],
      },
    }),
  ],
})
