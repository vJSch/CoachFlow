import { defineConfig } from 'vitest/config'

// Eigene Konfiguration ohne PWA-Plugin, damit Tests keinen Service Worker erzeugen.
export default defineConfig({
  test: {
    include: ['src/**/*.test.ts'],
  },
})
