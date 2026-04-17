<script setup>
import { ref, onMounted, onUnmounted } from 'vue'
import { request } from '@/api'

const networkInfo = ref({ 
    ip: 'CONNECTING...', 
    fullLocation: '...', 
    type: 'WAN', 
    isLocal: false 
})

const getFlagEmoji = (countryCode) => {
    if (!countryCode || countryCode.length !== 2) return '🌐'
    return countryCode.toUpperCase().replace(/./g, char => String.fromCodePoint(char.charCodeAt(0) + 127397))
}

const fetchNetworkInfo = async () => {
  try {
    // 1. Узнаем наш внутренний статус (локальный/нет)
    const internal = await request('/whoami')
    
    // 2. Узнаем реальный ПУБЛИЧНЫЙ IP пользователя напрямую с фронта.
    // Сервис ipify очень простой, поддерживает CORS и никогда не блокирует.
    const ipRes = await fetch('https://api.ipify.org?format=json')
    const { ip } = await ipRes.json()

    // 3. Теперь запрашиваем GEO-данные через наш ПРОКСИ для этого конкретного IP.
    // Так как мы передаем конкретный IP в URL, API вернет данные пользователя, а не прокси.
    const geoRes = await fetch(`/geo-proxy/${ip}`)
    const ext = await geoRes.json()

    if (ext) {
        const newType = internal.isLocal ? 'LAN (SECURE)' : 'WAN (PUBLIC)'
        const flag = getFlagEmoji(ext.country_code)
        const isp = ext.organization || ext.isp || 'Unknown ISP'
        const locString = `${flag} ${ext.city || '...'}, ${ext.country_code || ''} | ${isp}`

        networkInfo.value = { 
            ip: ip, 
            fullLocation: locString, 
            type: newType, 
            isLocal: internal.isLocal 
        }
    }
  } catch(e) { 
      console.error("Telemetry error:", e)
      if (networkInfo.value.ip === 'CONNECTING...') networkInfo.value.ip = 'OFFLINE'
  }
}

let netInt
onMounted(() => {
    fetchNetworkInfo()
    netInt = setInterval(fetchNetworkInfo, 300000)
})
onUnmounted(() => clearInterval(netInt))
</script>

<template>
  <div class="bg-zinc-900/50 border border-zinc-800 p-4 rounded-sm flex flex-col relative overflow-hidden group">
      <div class="absolute top-0 right-0 p-2 opacity-5 pointer-events-none group-hover:opacity-10 transition-opacity">
          <svg width="60" height="60" viewBox="0 0 24 24" fill="currentColor"><path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-1 17.93c-3.95-.49-7-3.85-7-7.93 0-.62.08-1.21.21-1.79L9 15v1c0 1.1.9 2 2 2v1.93zm6.9-2.54c-.26-.81-1-1.39-1.9-1.39h-1v-3c0-.55-.45-1-1-1H8v-2h2c.55 0 1-.45 1-1V7h2c1.1 0 2-.9 2-2v-.41c2.93 1.19 5 4.06 5 7.41 0 2.08-.8 3.97-2.1 5.39z"/></svg>
      </div>
      <div class="text-[10px] text-zinc-500 font-mono uppercase tracking-widest border-b border-zinc-800 pb-1 mb-3 flex justify-between">
          <span>Network_Telemetry</span>
          <span :class="networkInfo.ip === 'OFFLINE' ? 'text-red-500' : 'text-emerald-500'" class="font-bold">ONLINE</span>
      </div>
      <div class="space-y-3 font-mono text-xs flex-1 flex flex-col justify-center relative z-10">
          <div class="flex justify-between border-b border-zinc-800/50 pb-1">
              <span class="text-zinc-500">UPLINK_IP</span>
              <span class="text-emerald-300 font-bold tracking-wider select-all">{{ networkInfo.ip }}</span>
          </div>
          <div class="flex flex-col border-b border-zinc-800/50 pb-1">
              <span class="text-zinc-500 text-[9px] mb-0.5 uppercase">Location / ISP</span>
              <span class="text-zinc-300 truncate" :title="networkInfo.fullLocation">{{ networkInfo.fullLocation }}</span>
          </div>
          <div class="flex justify-between items-center pt-1">
              <span class="text-zinc-500 uppercase text-[9px]">Access_Mode</span>
              <span :class="['text-[9px] px-2 py-0.5 rounded border font-bold', networkInfo.isLocal ? 'text-emerald-400 border-emerald-500/20' : 'text-amber-400 border-amber-500/20']">{{ networkInfo.type }}</span>
          </div>
      </div>
  </div>
</template>