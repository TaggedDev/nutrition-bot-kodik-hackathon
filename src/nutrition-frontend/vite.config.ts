import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

const backendUrl = process.env.NUTRITION_BACKEND_URL ?? 'http://localhost:6861'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    strictPort: true,
    proxy: {
      '/api': {
        target: backendUrl,
        changeOrigin: true,
      },
    },
  },
})
