import { fileURLToPath, URL } from 'node:url'
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
// Если ты остался на tailwind v3 (как мы договаривались)
import tailwindcss from 'tailwindcss' 
import autoprefixer from 'autoprefixer'

export default defineConfig({
  plugins: [vue()],
  css: {
    postcss: {
      plugins: [tailwindcss, autoprefixer],
    },
  },
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url))
    }
  },
  // --- ВОТ ЭТО САМОЕ ГЛАВНОЕ ---
  server: {
    proxy: {
      '/api': {
        target: 'http://localhost:3000', // Куда слать запросы
        changeOrigin: true,
        secure: false,
      }
    }
  }
})