<script setup>
import { ref, onMounted } from 'vue'

const forecast = ref([])
const loading = ref(true)

const getWeatherIcon = (code) => {
    if (code === 0) return '☀️'
    if (code < 3) return '⛅'
    if (code < 48) return '🌫️'
    if (code < 60) return '🌧️'
    if (code < 70) return '☔'
    if (code < 80) return '❄️'
    if (code < 95) return '⛈️'
    return '🌊'
}

const getDayName = (dateStr) => {
    const date = new Date(dateStr)
    return date.toLocaleDateString('en-US', { weekday: 'short' }).toUpperCase()
}

const fetchWeather = async () => {
  try {
    // НОВЫЕ КООРДИНАТЫ (Ульяновск)
    const lat = 54.3282
    const lon = 48.3866
    
    const res = await fetch(`https://api.open-meteo.com/v1/forecast?latitude=${lat}&longitude=${lon}&daily=weathercode,temperature_2m_max,temperature_2m_min&timezone=auto`)
    const data = await res.json()
    
    const daily = data.daily
    const result = []
    
    for (let i = 0; i < 5; i++) {
        result.push({
            date: daily.time[i],
            code: daily.weathercode[i],
            max: Math.round(daily.temperature_2m_max[i]),
            min: Math.round(daily.temperature_2m_min[i])
        })
    }
    forecast.value = result
  } catch (e) {
    console.error(e)
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  fetchWeather()
})
</script>

<template>
  <!-- Темплейт тот же самый, без изменений -->
  <div class="space-y-3">
    <div class="flex items-center justify-between text-[10px] font-mono font-bold text-cyan-500 uppercase tracking-widest border-b border-cyan-500/20 pb-1">
       <span class="flex items-center gap-2">
         <svg class="w-3 h-3 fill-current" viewBox="0 0 24 24"><path d="M18.435 5.094A6.7 6.7 0 0 0 12.5 2a6.7 6.7 0 0 0-5.96 3.11C2.863 5.76 0 8.932 0 12.5c0 3.736 3.162 6.845 7.027 6.996h10.967C21.313 19.352 24 16.59 24 13.25c0-3.32-2.668-6.066-5.565-6.156z"/></svg>
         FORECAST_5D
       </span>
       <span class="bg-cyan-500/20 text-cyan-400 px-1.5 rounded">ULY</span>
    </div>

    <div v-if="loading" class="text-zinc-600 text-[10px] font-mono italic animate-pulse">
        SCANNING_ATMOSPHERE...
    </div>

    <div v-else class="flex flex-col gap-1">
        <div v-for="(day, index) in forecast" :key="day.date" class="flex items-center justify-between py-1.5 px-2 rounded hover:bg-zinc-900/50 border border-transparent hover:border-zinc-800 transition group">
            <div class="flex items-center gap-3 w-1/3">
                <span class="text-[10px] font-mono font-bold" :class="index === 0 ? 'text-cyan-400' : 'text-zinc-500'">
                    {{ index === 0 ? 'TODAY' : getDayName(day.date) }}
                </span>
            </div>
            <div class="text-sm w-1/3 text-center opacity-80 group-hover:opacity-100 transition scale-90 group-hover:scale-110 duration-300">
                {{ getWeatherIcon(day.code) }}
            </div>
            <div class="w-1/3 text-right font-mono text-[10px]">
                <span class="text-zinc-300">{{ day.max }}°</span>
                <span class="text-zinc-600 mx-1">/</span>
                <span class="text-zinc-500">{{ day.min }}°</span>
            </div>
        </div>
    </div>
  </div>
</template>