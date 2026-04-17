<script setup>
import { ref, onMounted } from 'vue'
import { request } from '@/api'
import WeatherSettingsModal from '@/components/WeatherSettingsModal.vue'

const props = defineProps({
  dashboardId: Number
})

const forecast = ref([])
const current = ref(null)
const cityName = ref('') // Хранит имя города
const loading = ref(true)
const isSettingsOpen = ref(false)

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
  loading.value = true
  try {
    // 1. Сначала загружаем настройки, чтобы узнать имя города
    try {
        const settings = await request(`/weather/dashboards/${props.dashboardId}/settings`)
        console.log(settings)
        // Если вернулся пустой объект, cityName останется пустым
        if (settings && settings.CityName) {
            cityName.value = settings.CityName.toUpperCase()
        }
    } catch (e) {
        // Если ошибка настроек, просто игнорируем, будет дефолтный заголовок
        console.warn("Weather settings load error:", e)
    }

    // 2. Загружаем сам прогноз
    const data = await request(`/weather/dashboards/${props.dashboardId}`)
    
    if (data.notConfigured) {
        // Если не настроено, сбрасываем данные
        forecast.value = []
        current.value = null
        loading.value = false
        return
    }

    if (data.daily) {
        forecast.value = data.daily
    }
    if (data.current) {
        current.value = data.current
    }

  } catch (e) {
    console.error("Weather load error:", e)
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  fetchWeather()
  setInterval(fetchWeather, 1800000) // 30 min
})
</script>

<template>
  <div class="space-y-3 relative group/widget">
    
    <WeatherSettingsModal 
        :is-open="isSettingsOpen" 
        :dashboard-id="dashboardId" 
        @close="isSettingsOpen = false" 
        @refresh="fetchWeather" 
    />

    <!-- HEADER -->
    <div class="flex items-center justify-between text-[10px] font-mono font-bold text-cyan-500 uppercase tracking-widest border-b border-cyan-500/20 pb-1">
       <span class="flex items-center gap-2">
         <svg class="w-3 h-3 fill-current" viewBox="0 0 24 24"><path d="M18.435 5.094A6.7 6.7 0 0 0 12.5 2a6.7 6.7 0 0 0-5.96 3.11C2.863 5.76 0 8.932 0 12.5c0 3.736 3.162 6.845 7.027 6.996h10.967C21.313 19.352 24 16.59 24 13.25c0-3.32-2.668-6.066-5.565-6.156z"/></svg>
         
         <!-- Вывод имени города или дефолта -->
         <span>{{ cityName || 'ATMOSPHERE' }}</span>
       </span>
       
       <div class="flex items-center gap-2">
           <span v-if="current" class="bg-cyan-500/20 text-cyan-400 px-1.5 rounded">{{ Math.round(current.temp) }}°</span>
           
           <!-- Кнопка настроек (видна при наведении на виджет) -->
           <button 
               @click="isSettingsOpen = true" 
               class="text-zinc-600 hover:text-cyan-400 opacity-0 group-hover/widget:opacity-100 transition"
               title="Configure Location"
           >
               ⚙
           </button>
       </div>
    </div>

    <!-- STATES -->
    <div v-if="loading && forecast.length === 0" class="text-zinc-600 text-[10px] font-mono italic animate-pulse">
        SCANNING...
    </div>
    
    <div v-else-if="forecast.length === 0" class="text-zinc-700 text-[10px] font-mono italic py-2 text-center border border-dashed border-zinc-800 cursor-pointer hover:border-cyan-500/50 hover:text-cyan-400 transition" @click="isSettingsOpen = true">
        [+] SET LOCATION
    </div>

    <!-- LIST -->
    <div v-else class="flex flex-col gap-1">
        <div v-for="(day, index) in forecast" :key="day.date" class="flex items-center justify-between py-1.5 px-2 rounded hover:bg-zinc-900/50 border border-transparent hover:border-zinc-800 transition group">
            <div class="flex items-center gap-3 w-1/3">
                <span class="text-[10px] font-mono font-bold" :class="index === 0 ? 'text-cyan-400' : 'text-zinc-500'">
                    {{ index === 0 ? 'TODAY' : getDayName(day.date) }}
                </span>
            </div>
            <div class="text-sm w-1/3 text-center opacity-80 group-hover:opacity-100 transition scale-90 group-hover:scale-110 duration-300">
                 {{ getWeatherIcon(day.code)}} {{day.code}}
            </div>
            <div class="w-1/3 text-right font-mono text-[10px]">
                <span class="text-zinc-300">{{ Math.round(day.maxTemp) }}°</span>
                <span class="text-zinc-600 mx-1">/</span>
                <span class="text-zinc-500">{{ Math.round(day.minTemp) }}°</span>
            </div>
        </div>
    </div>
  </div>
</template>