<script setup>
import { ref, onMounted, onUnmounted, computed, watch } from 'vue'
import { request } from '@/api'
import WeatherSettingsModal from '@/components/WeatherSettingsModal.vue'

const props = defineProps({
  dashboardId: Number,
  schedule: Object,
  activeIntegrations: { type: Array, default: () => [] }
})

// Time State
const time = ref('')
const dateStr = ref('')

// Weather State
const weather = ref({ temp: '--', code: 0, desc: 'SCANNING' })
const isWeatherSettingsOpen = ref(false)

// Work/Break State
const workStatus = ref({ isActive: false, state: 'IDLE', elapsed: '00:00:00', remaining: '00:00:00', breakTime: '00:00:00', percent: 0 })
const breakState = ref({ isBreak: false, breakStartTs: 0, totalBreakMs: 0, currentSessionMs: 0, lastUpdateDate: new Date().toDateString() })

// --- TIME ---
const updateTime = () => {
  const now = new Date();
  time.value = now.toLocaleTimeString('en-US', { hour12: false, hour: '2-digit', minute:'2-digit', second: '2-digit' })
  dateStr.value = now.toLocaleDateString('en-US', { weekday: 'short', day: 'numeric', month: 'short' }).toUpperCase()
}

// --- WEATHER ---
const getWeatherIcon = (code) => { if (code === 0) return '☀️'; if (code < 3) return '⛅'; if (code < 48) return '🌫️'; if (code < 60) return '🌧️'; if (code < 70) return '☔'; if (code < 80) return '❄️'; if (code < 95) return '⛈️'; return '🌊' }
const getWmoDesc = (code) => { const map = { 0:'CLEAR', 1:'MAINLY CLEAR', 2:'PARTLY CLOUDY', 3:'OVERCAST', 45:'FOG', 48:'RIME FOG', 51:'DRIZZLE', 61:'RAIN', 71:'SNOW', 80:'SHOWER', 95:'THUNDERSTORM' }; return map[code] || 'UNKNOWN' }

const getWeather = async () => {
    if (!props.activeIntegrations.includes('Weather')) return
    try {
        const data = await request(`/weather/dashboards/${props.dashboardId}`)
        if (data.current) weather.value = { temp: Math.round(data.current.temp), code: data.current.code, desc: getWmoDesc(data.current.code) }
    } catch(e) {}
}

// --- WORK LOGIC ---
const formatDuration = (ms) => { if (ms < 0) ms = 0; const s = Math.floor(ms/1000); const h = Math.floor(s/3600); const m = Math.floor((s%3600)/60); const sec = s%60; const pad = n => n.toString().padStart(2,'0'); return `${pad(h)}:${pad(m)}:${pad(sec)}` }

const updateWorkStatus = () => {
    const currentTotalBreak = breakState.value.totalBreakMs + breakState.value.currentSessionMs
    if (breakState.value.isBreak) {
         workStatus.value = { state: 'BREAK', elapsed: formatDuration(breakState.value.currentSessionMs), remaining: 'PAUSED', breakTime: formatDuration(currentTotalBreak), percent: 0, isActive: true }
         return
    }
    const settings = props.schedule
    if (!settings?.enabled) { workStatus.value.state = 'IDLE'; return }
    
    const now = new Date(); const currentDay = now.getDay()
    if (!settings.days?.includes(currentDay)) { workStatus.value.state = 'WEEKEND'; workStatus.value.breakTime = formatDuration(currentTotalBreak); return }
    
    const [startH, startM] = settings.start.split(':').map(Number); const [endH, endM] = settings.end.split(':').map(Number)
    const startTime = new Date(now).setHours(startH, startM, 0, 0); const endTime = new Date(now).setHours(endH, endM, 0, 0); const nowTime = now.getTime()
    
    if (nowTime >= startTime && nowTime < endTime) {
         const totalShift = endTime - startTime; const elapsedRaw = nowTime - startTime; const remainingRaw = endTime - nowTime
         workStatus.value = { state: 'WORK', isActive: true, elapsed: formatDuration(elapsedRaw), remaining: formatDuration(remainingRaw), breakTime: formatDuration(currentTotalBreak), percent: (elapsedRaw / totalShift) * 100 }
    } else if (nowTime < startTime) {
        workStatus.value = { state: 'BEFORE_SHIFT', remaining: formatDuration(startTime - nowTime), breakTime: formatDuration(currentTotalBreak) }
    } else {
        workStatus.value = { state: 'AFTER_SHIFT', elapsed: formatDuration(nowTime - endTime), breakTime: formatDuration(currentTotalBreak) }
    }
}

const fetchStatus = async () => {
    try {
        const data = await request(`/status/${props.dashboardId}`)
        const rawDate = data.lastUpdate || data.lastUpdateDate
        const serverDateObj = new Date(rawDate)
        const serverDateStr = !isNaN(serverDateObj) ? serverDateObj.toDateString() : ''
        const todayStr = new Date().toDateString()
        if (serverDateStr !== todayStr) {
            breakState.value = { isBreak: false, breakStartTs: 0, totalBreakMs: 0, currentSessionMs: 0, lastUpdateDate: new Date() }
            saveStatus()
        } else {
            breakState.value = { ...data, lastUpdateDate: serverDateObj }
            if (data.isBreak && data.breakStartTs > 0) breakState.value.currentSessionMs = Date.now() - data.breakStartTs
            else breakState.value.currentSessionMs = 0
        }
        updateWorkStatus()
    } catch (e) {}
}

const saveStatus = async () => {
    const payload = { ...breakState.value, lastUpdate: new Date(), lastUpdateDate: new Date() }
    delete payload.currentSessionMs
    try { await request(`/status/${props.dashboardId}`, { method: 'POST', body: JSON.stringify(payload) }) } catch(e){}
}

const toggleBreak = async () => {
    const now = Date.now()
    const willBeOnBreak = !breakState.value.isBreak

    // 1. Логика таймеров (оставляем твою рабочую)
    if (breakState.value.isBreak) {
        breakState.value.isBreak = false
        breakState.value.totalBreakMs += (now - breakState.value.breakStartTs)
        breakState.value.breakStartTs = 0
        breakState.value.currentSessionMs = 0
    } else {
        breakState.value.isBreak = true
        breakState.value.breakStartTs = now
        breakState.value.currentSessionMs = 0
    }

    // 2. Сохраняем состояние таймеров на бэк
    saveStatus()
    updateWorkStatus()

    // 3. НОВОЕ: Синхронизируем статус в Team Status
    try {
        const teamStatusPayload = willBeOnBreak 
            ? { 
                statusEmoji: '🍴', 
                statusText: 'На обеде', 
                statusColor: 'red', 
                statusMessage: '' 
              }
            : { 
                statusEmoji: '🖥️', 
                statusText: 'Вернулся', 
                statusColor: 'emerald', 
                statusMessage: '' 
              }

        // Отправляем на стандартный эндпоинт управления личным статусом
        await request('/users/status/me', {
            method: 'PUT',
            body: JSON.stringify(teamStatusPayload)
        })
        
        // После этого SignalR на бэкенде должен сам разослать всем 
        // (включая тебя) уведомление об обновлении Team Status.
    } catch (e) {
        console.error("Failed to sync team status on break:", e)
    }
}

const tickBreakTimer = () => { if (breakState.value.isBreak && breakState.value.breakStartTs > 0) breakState.value.currentSessionMs = Date.now() - breakState.value.breakStartTs }

let timerInt
watch(() => props.dashboardId, () => { fetchStatus(); getWeather(); }, { immediate: true })

onMounted(() => {
    updateTime(); 
    timerInt = setInterval(() => { updateTime(); updateWorkStatus(); tickBreakTimer(); }, 1000)
    // Погоду можно обновлять реже
    setInterval(getWeather, 1800000)
})

onUnmounted(() => clearInterval(timerInt))
</script>

<template>
  <div class="bg-zinc-900/80 border border-zinc-800 p-5 rounded-sm flex flex-col justify-between relative overflow-hidden group">
      <WeatherSettingsModal :is-open="isWeatherSettingsOpen" :dashboard-id="dashboardId" @close="isWeatherSettingsOpen = false" @refresh="getWeather" />
      
      <div class="absolute top-0 left-0 w-1 h-full bg-emerald-500/30 transition-all duration-1000" :style="{ height: workStatus.state === 'WORK' ? workStatus.percent + '%' : '100%' }"></div>
      
      <div class="flex justify-between items-start">
          <div><div class="text-4xl font-mono font-bold text-emerald-400 tracking-wider leading-none">{{ time }}</div><div class="text-zinc-500 font-mono text-xs uppercase tracking-widest mt-1">{{ dateStr }}</div></div>
          <div v-if="activeIntegrations.includes('Weather')" class="text-right group cursor-pointer" @click="isWeatherSettingsOpen = true">
              <div class="flex items-center justify-end gap-2"><span class="text-3xl filter drop-shadow-lg">{{ getWeatherIcon(weather.code) }}</span><span class="text-3xl text-emerald-100 font-mono font-bold">{{ weather.temp }}°</span></div>
              <div class="text-[10px] text-emerald-500/70 font-mono uppercase tracking-wide mt-1">{{ weather.desc }}</div>
          </div>
      </div>
      
      <div class="mt-4 pt-3 border-t border-zinc-800/50 flex items-center justify-between font-mono text-xs">
            <div class="flex items-center gap-4"><div class="flex items-center gap-2"><span class="w-2 h-2 rounded-full animate-pulse" :class="{'bg-emerald-500': workStatus.state === 'WORK', 'bg-blue-500': workStatus.state === 'BREAK', 'bg-zinc-600': ['IDLE','WEEKEND','BEFORE_SHIFT','AFTER_SHIFT'].includes(workStatus.state)}"></span><span class="font-bold tracking-wider" :class="{'text-emerald-400': workStatus.state === 'WORK', 'text-blue-400': workStatus.state === 'BREAK', 'text-zinc-500': !workStatus.isActive}">{{ workStatus.state }}</span></div><button v-if="workStatus.state !== 'IDLE'" @click="toggleBreak" class="border px-3 py-0.5 rounded text-[10px] transition uppercase tracking-wide" :class="breakState.isBreak ? 'border-blue-500 text-blue-400 bg-blue-900/10 hover:bg-blue-900/20' : 'border-zinc-700 text-zinc-400 hover:text-white hover:border-zinc-500'">{{ breakState.isBreak ? 'STOP BREAK' : 'START BREAK' }}</button></div>
            <div class="flex gap-6 text-zinc-400" v-if="workStatus.isActive || workStatus.state === 'BREAK'">
                <div class="flex flex-col items-end leading-none"><span class="text-[8px] text-zinc-600 uppercase">Elapsed</span><span class="text-emerald-100 font-bold">{{ workStatus.elapsed }}</span></div>
                <div class="flex flex-col items-end leading-none"><span class="text-[8px] text-zinc-600 uppercase">Remaining</span><span class="text-emerald-100 font-bold">{{ workStatus.remaining }}</span></div>
                <div class="flex flex-col items-end leading-none border-l border-zinc-800 pl-4"><span class="text-[8px] uppercase" :class="breakState.isBreak ? 'text-blue-400 animate-pulse' : 'text-zinc-600'">{{ breakState.isBreak ? 'On Break' : 'Total Break' }}</span><span class="font-bold" :class="breakState.isBreak ? 'text-blue-300' : 'text-zinc-400'">{{ workStatus.breakTime }}</span></div>
            </div>
      </div>
  </div>
</template>