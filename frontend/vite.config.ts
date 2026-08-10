import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  base: '/snipit/',
  plugins: [
    react({
      babel: {
        plugins: [['babel-plugin-react-compiler']],
      },
    }),
  ],
  server: {
    proxy: {
      // The app now calls the API under its own base path (see src/api/apiBase.ts), so the dev
      // server has to forward `/snipit/api` too — and strip the prefix, because a locally-run
      // backend has no PathBase configured and serves the routes at `/api`.
      '/snipit/api': {
        target: 'http://localhost:5257',
        changeOrigin: true,
        secure: false,
        rewrite: (path) => path.replace(/^\/snipit/, ''),
      },
      '/api': {
        target: 'http://localhost:5257',
        changeOrigin: true,
        secure: false,
      },
      '/openapi': {
        target: 'http://localhost:5257',
        changeOrigin: true,
        secure: false,
      }
    }
  }
})
