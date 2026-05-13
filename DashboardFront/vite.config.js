import { fileURLToPath, URL } from 'node:url'
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
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
  server: {
    proxy: {
      '/api': {
        target: 'https://dev.me.mcrt.space',
        //target:"https://localhost:7052",
        changeOrigin: true,
        secure: false, // Игнорировать самоподписанный сертификат
        ws: true       // <--- ДОБАВЬ ЭТУ СТРОКУ! (Включает прокси для WebSockets)
      },
      // ПРОКСИ ДЛЯ FREEIPAPI
            '/geo-proxy': {
        target: 'https://api.ip.sb/geoip',
        changeOrigin: true,
        secure: true,
        rewrite: (path) => path.replace(/^\/geo-proxy/, ''), // Убираем префикс
        configure: (proxy) => {
          proxy.on('proxyRes', (proxyRes) => {
            proxyRes.headers['Access-Control-Allow-Origin'] = '*';
          });
        }
      }
    }
  }
})