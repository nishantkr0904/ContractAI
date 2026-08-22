import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

// The SPA talks to the .NET API same-origin in dev: everything under /api is
// proxied to the backend on :5194, so the browser never makes a cross-origin
// request and the API needs no CORS configuration. In production the built assets
// are served behind the same origin as the API (Phase 6), so the relative /api
// paths hold there too.
export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    proxy: {
      '/api': {
        target: 'http://localhost:5194',
        changeOrigin: true,
      },
    },
  },
})
