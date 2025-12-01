<script setup>
import { ref, computed, watch, onMounted, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'

// Компоненты
import ServiceCard from '@/components/ServiceCard.vue'
import CountdownTimer from '@/components/CountdownTimer.vue'
import ConfigEditor from '@/components/ConfigEditor.vue'
import TelegramWidget from '@/components/TelegramWidget.vue'
import WeatherWidget from '@/components/WeatherWidget.vue'
import NotesWidget from '@/components/NotesWidget.vue'
import CalculatorModal from '@/components/CalculatorModal.vue'

const props = defineProps(['dashboardId'])
const router = useRouter()

// --- STATE ---
const availableDashboards = ref(['default'])
const categories = ref([])
const rawEvents = ref([])
const isLocalUser = ref(false)
const networkInfo = ref({ ip: '...', location: '...', isp: '...', flag: '' })
const weather = ref(null)
const time = ref('')
const dateStr = ref('')

// Состояния UI
const isConfigLoading = ref(true)
const isEventsLoading = ref(false)
const isEditorOpen = ref(false)
const isCalcOpen = ref(false) // Модалка калькулятора
const fullConfig = ref({})
const calendarError = ref(false)
const telegramError = ref(false)
const workStatus = ref({ isActive: false, text: '', timeLeft: '' })
const searchQuery = ref('')

// Вычисляем имя текущего дашборда
const dashboardName = computed(() => {
  if (!props.dashboardId || props.dashboardId === 'undefined' || props.dashboardId === '') {
    return 'default'
  }
  return props.dashboardId
})

const systemStatus = computed(() => {
    if (calendarError.value || (isTelegramEnabled.value && telegramError.value)) return 'WARNING'
    return 'ONLINE'
})

// --- TELEGRAM TOGGLE ---
const isTelegramEnabled = ref(localStorage.getItem('tg_enabled') !== 'false')
const toggleTelegram = () => {
    isTelegramEnabled.value = !isTelegramEnabled.value
    localStorage.setItem('tg_enabled', isTelegramEnabled.value)
}

// --- NETWORK INFO ---
const fetchNetworkInfo = async () => {
    try {
       // 1. Проверяем локальность через наш сервер (он видит внутренний IP)
       const internalRes = await fetch('/api/whoami')
       const internalData = await internalRes.json()
       isLocalUser.value = internalData.isLocal
       
       // 2. Получаем внешний IP и Гео через публичный API
       // Если мы локально, networkInfo.ip покажет внешний адрес шлюза
       const ipRes = await fetch('https://api.ipify.org?format=json')
       const ipData = await ipRes.json()
       networkInfo.value.ip = ipData.ip

       const geoRes = await fetch(`https://ipwho.is/${ipData.ip}`)
       const geo = await geoRes.json()
       if(geo.success) {
            networkInfo.value.location = `${geo.city}, ${geo.country_code}`
            networkInfo.value.flag = geo.flag.emoji
            networkInfo.value.isp = geo.connection.isp
       }
    } catch (e) {
        console.error("Net Info Error", e)
        networkInfo.value.ip = 'N/A'
    }
}

// --- WORK SCHEDULE ---
const updateWorkStatus = () => {
    const settings = fullConfig.value.settings?.workSchedule
    if (!settings || !settings.enabled) {
        workStatus.value = { isActive: false, text: '', timeLeft: '' }
        return
    }

    const now = new Date()
    const currentDay = now.getDay() // 0-Sun, 1-Mon...

    if (!settings.days || !settings.days.includes(currentDay)) {
        workStatus.value = { isActive: false, text: 'WEEKEND', timeLeft: '' }
        return
    }

    const [startH, startM] = settings.start.split(':').map(Number)
    const [endH, endM] = settings.end.split(':').map(Number)
    
    const startTime = new Date(now).setHours(startH, startM, 0, 0)
    const endTime = new Date(now).setHours(endH, endM, 0, 0)
    const nowTime = now.getTime()

    if (nowTime >= startTime && nowTime < endTime) {
        const diff = endTime - nowTime
        const hrs = Math.floor(diff / 3600000)
        const mins = Math.floor((diff % 3600000) / 60000)
        workStatus.value = { isActive: true, text: 'WORKING', timeLeft: `-${hrs}h ${mins}m` }
    } else if (nowTime < startTime) {
        workStatus.value = { isActive: false, text: 'BEFORE_SHIFT', timeLeft: '' }
    } else {
        workStatus.value = { isActive: false, text: 'AFTER_SHIFT', timeLeft: '' }
    }
}

// --- EVENTS LOADING & MERGING ---
const fetchDashboardEvents = async () => {
    if (rawEvents.value.length === 0) isEventsLoading.value = true;
    try {
        // 1. Автоматические события (Календарь)
        const res = await fetch(`/api/events/${dashboardName.value}?t=${Date.now()}`)
        if (!res.ok) throw new Error("Fetch failed")
        let apiData = await res.json()
        
        // Нормализация (ищем DTSTART)
        apiData = apiData.map(ev => {
            const normalized = {}
            for (const key in ev) normalized[key.toLowerCase()] = ev[key]

            // Приоритет выбора даты (берем максимальную, чтобы учесть переносы)
            const candidates = [normalized.dtstart, normalized.date, normalized.start]
            let rawDate = null
            let maxTs = 0
            
            candidates.forEach(val => {
                if (!val) return
                const actualVal = (typeof val === 'object' && val?.value) ? val.value : val
                const ts = new Date(actualVal).getTime()
                if (!isNaN(ts) && ts > maxTs) { maxTs = ts; rawDate = actualVal; }
            })

            return {
                ...normalized, 
                name: ev.name || ev.summary || normalized.summary || 'Event',
                date: rawDate,
                source: 'Auto'
            }
        }).filter(e => e.date) // Убираем без даты

        // 2. Ручные события
        const manualEvents = (fullConfig.value.events || []).map(e => ({ ...e, source: 'Manual' }))

        // 3. Override Logic (Ручные перекрывают календарные по имени)
        const finalEvents = []
        const manualKeys = new Set()
        
        manualEvents.forEach(m => {
            finalEvents.push(m)
            try {
                // Ключ: имя + дата (день). Если есть ручное на этот день - календарное скрываем
                manualKeys.add(`${m.name.toLowerCase().trim()}_${new Date(m.date).toDateString()}`)
            } catch(e) {}
        })

        apiData.forEach(a => {
            try {
                const key = `${a.name.toLowerCase().trim()}_${new Date(a.date).toDateString()}`
                if (!manualKeys.has(key)) finalEvents.push(a)
            } catch(e) { finalEvents.push(a) }
        })

        rawEvents.value = finalEvents
        calendarError.value = false 
     } catch(e) { 
         console.error("Events error", e)
        calendarError.value = true
    } finally {
        isEventsLoading.value = false
    }
}

// --- GROUPING EVENTS ---
const groupedEvents = computed(() => {
  const now = new Date()
  const todayStart = new Date(now.getFullYear(), now.getMonth(), now.getDate())
  const tomorrowStart = new Date(todayStart); tomorrowStart.setDate(todayStart.getDate() + 1)
  const afterTomorrowStart = new Date(todayStart); afterTomorrowStart.setDate(todayStart.getDate() + 2)

  const groups = { today: [], tomorrow: [], upcoming: [] }

  rawEvents.value.forEach(ev => {
    const evDate = new Date(ev.date)
    if (evDate < now && ev.source !== 'Manual') return 

    if (evDate >= todayStart && evDate < tomorrowStart) {
      groups.today.push(ev)
    } else if (evDate >= tomorrowStart && evDate < afterTomorrowStart) {
      groups.tomorrow.push(ev)
    } else {
      // Upcoming: показываем ТОЛЬКО ручные важные цели, чтобы не захламлять календарными встречами
      if (ev.source === 'Manual') groups.upcoming.push(ev)
    }
  })
  
  // Сортировка
  const sortFn = (a, b) => new Date(a.date) - new Date(b.date)
  groups.today.sort(sortFn)
  groups.tomorrow.sort(sortFn)
  groups.upcoming.sort(sortFn)

  return groups
})

// --- MAIN LOAD ---
const loadDashboardList = async () => {
  try { availableDashboards.value = await (await fetch('/api/dashboards')).json() } catch (e) {}
}

const loadData = async () => {
  isConfigLoading.value = true
  categories.value = []
  rawEvents.value = [] 
  calendarError.value = false
  telegramError.value = false
  
  const currentDash = dashboardName.value
  let attempts = 0
  let isAuthSuccess = false
  
  try {
    while (attempts < 3 && !isAuthSuccess) {
        attempts++
        const configRes = await fetch(`/api/config/${currentDash}?t=${Date.now()}`)
        
        if (configRes.ok) {
            const configData = await configRes.json()
            fullConfig.value = configData
            categories.value = configData.categories || []
            isAuthSuccess = true
        } else if (configRes.status === 401) {
            // Browser auth prompt handles this
        } else {
            if (configRes.status === 404 && currentDash !== 'default') {
                 router.push('/')
                 return
            }
            throw new Error(`Server Error: ${configRes.status}`)
        }
    }
    
    if (!isAuthSuccess && currentDash !== 'default') {
        router.push('/')
        return
    }

    isConfigLoading.value = false 
    fetchDashboardEvents() 
    fetchNetworkInfo()
    updateWorkStatus()

  } catch (e) {
    console.error("Critical Load Error:", e)
    isConfigLoading.value = false
  }
}

watch(() => props.dashboardId, () => loadData(), { immediate: true })

// --- TIMERS & UTILS ---
const updateTime = () => {
  const now = new Date()
  time.value = now.toLocaleTimeString([], { hour: '2-digit', minute:'2-digit', second: '2-digit' })
  dateStr.value = now.toLocaleDateString('ru-RU', { weekday: 'short', day: 'numeric', month: 'long' })
}
const getWeather = async () => {
  try { weather.value = (await (await fetch('https://api.open-meteo.com/v1/forecast?latitude=54.3282&longitude=48.3866&current_weather=true')).json()).current_weather } catch (e) {}
}

const filteredCategories = computed(() => {
  if (!searchQuery.value) return categories.value
  const query = searchQuery.value.toLowerCase()
  return categories.value.map(cat => {
    if (cat.title.toLowerCase().includes(query)) return cat
    const items = cat.items.filter(item => item.name.toLowerCase().includes(query) || item.desc.toLowerCase().includes(query))
    return { ...cat, items }
  }).filter(c => c.items.length > 0)
})

const createNew = async () => {
  const name = prompt("Name (a-z):")
  if (!name) return
  const safeName = name.replace(/[^a-z0-9-_]/gi, '').toLowerCase()
  if (!safeName) return
  await fetch(`/api/config/${safeName}`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ settings: { title: safeName }, categories: [], events: [] }) })
  await loadDashboardList()
  router.push(`/${safeName}`)
}

const onConfigSaved = () => { isEditorOpen.value = false; loadData() }

// Hotkeys (Alt+C)
const handleGlobalKeys = (e) => {
    if (e.altKey && e.code === 'KeyC') {
        e.preventDefault()
        isCalcOpen.value = !isCalcOpen.value
    }
}

let timerInt, eventsInt, workInt

onMounted(() => {
   loadDashboardList()
   getWeather()
   timerInt = setInterval(updateTime, 1000)
   updateTime()
   eventsInt = setInterval(fetchDashboardEvents, 600000)
   workInt = setInterval(updateWorkStatus, 60000)
   window.addEventListener('keydown', handleGlobalKeys)
})

onUnmounted(() => {
    clearInterval(timerInt)
    clearInterval(eventsInt)
    clearInterval(workInt)
    window.removeEventListener('keydown', handleGlobalKeys)
})
</script>

<template>
  <div class="min-h-screen p-6 md:p-10 max-w-[1600px] mx-auto font-sans">
    
    <!-- MODALS -->
    <ConfigEditor :is-open="isEditorOpen" :config-data="fullConfig" @close="isEditorOpen = false" @save="onConfigSaved" />
    <CalculatorModal :is-open="isCalcOpen" @close="isCalcOpen = false" />
    
    <!-- HEADER -->
    <header class="mb-8 flex flex-col md:flex-row items-start md:items-center justify-between border-b border-zinc-800 pb-6 gap-4">
      <div>
        <h1 class="text-3xl font-mono font-bold text-zinc-100 uppercase tracking-widest flex items-center gap-3">
          <span class="text-emerald-500 animate-pulse">●</span> {{ fullConfig.settings?.title || 'SYSTEM' }}<span class="text-emerald-500">_</span>CTL
        </h1>
        <p class="text-zinc-500 font-mono text-xs mt-1 pl-6">ID: <span class="text-emerald-500">{{ dashboardName }}</span></p>
      </div>
      
      <div class="hidden md:block text-right">
        <div class="text-[10px] font-mono" :class="systemStatus === 'ONLINE' ? 'text-emerald-500/50' : 'text-red-500/50 animate-pulse'">STATUS</div>
        <div class="font-mono text-sm" :class="systemStatus === 'ONLINE' ? 'text-emerald-400' : 'text-red-500'">{{ systemStatus }}</div>
      </div>
      
      <div class="flex items-center gap-3">
        <div class="flex items-center bg-zinc-900 border border-zinc-700 rounded px-2">
           <span class="text-zinc-500 text-[10px] mr-2 font-mono">LOAD:</span>
           <select :value="dashboardName" @change="router.push($event.target.value === 'default' ? '/' : '/' + $event.target.value)" class="bg-transparent text-emerald-500 text-xs font-mono py-1 outline-none cursor-pointer uppercase">
             <option v-for="d in availableDashboards" :key="d" :value="d">{{ d }}</option>
           </select>
        </div>
        
        <!-- КНОПКА КАЛЬКУЛЯТОРА -->
        <button 
          @click="isCalcOpen = true" 
          class="text-zinc-500 hover:text-emerald-400 font-mono text-xs border border-zinc-800 hover:border-emerald-500 px-2 py-1 rounded transition ml-2"
          title="Alt+C"
        >[CALC]</button>

        <button 
            v-if="dashboardName !== 'default'"
            @click="toggleTelegram"
            class="text-[10px] font-mono border px-2 py-1 rounded transition w-20 text-center ml-2"
            :class="isTelegramEnabled ? 'text-sky-400 border-sky-500/50 hover:bg-sky-500/10' : 'text-zinc-600 border-zinc-800 hover:text-zinc-400'"
        >{{ isTelegramEnabled ? 'TG: ON' : 'TG: OFF' }}</button>

        <button @click="createNew" class="text-zinc-500 hover:text-emerald-400 font-mono text-xs border border-zinc-800 hover:border-emerald-500 px-2 py-1 rounded transition ml-2">[+]</button>
        <button @click="isEditorOpen = true" class="text-[10px] font-mono text-zinc-600 hover:text-emerald-400 border border-zinc-800 hover:border-emerald-500/50 px-2 py-1 rounded transition ml-2">[CFG]</button>
      </div>
    </header>

    <div v-if="isConfigLoading" class="text-center text-emerald-500/50 mt-20 font-mono animate-pulse">ACCESSING SECURE DATA...</div>
    
    <div v-else class="grid grid-cols-1 lg:grid-cols-4 gap-10 items-start h-[calc(100vh-200px)]">
      
      <!-- MAIN (LEFT) -->
      <main class="lg:col-span-3 h-full flex flex-col gap-6">
        
        <!-- INFO BLOCK -->
        <div class="grid grid-cols-1 md:grid-cols-2 gap-6 flex-shrink-0">
            <!-- Clock -->
            <div class="bg-zinc-900/80 rounded-sm p-4 border border-zinc-800 flex flex-col justify-between relative overflow-hidden group min-h-[110px]">
                <div class="absolute top-0 left-0 w-1 h-full bg-emerald-500/20"></div>
                <div class="flex justify-between items-start">
                    <div>
                        <div class="text-3xl font-mono font-bold text-emerald-400 tracking-wider mb-1">{{ time }}</div>
                        <div class="text-zinc-500 font-mono text-[10px] uppercase tracking-widest">{{ dateStr }}</div>
                    </div>
                    <div v-if="weather" class="text-right">
                         <span class="text-2xl text-emerald-500/80 font-mono">{{ weather.temperature }}°</span>
                         <div class="text-[10px] text-zinc-600">TEMP</div>
                    </div>
                </div>
                <!-- Work Schedule -->
                <div v-if="workStatus.text" class="mt-4 pt-2 border-t border-zinc-800 flex items-center justify-between font-mono text-[10px]">
                     <span :class="workStatus.isActive ? 'text-amber-500 animate-pulse' : 'text-zinc-600'">
                        {{ workStatus.isActive ? '⚡' : '💤' }} {{ workStatus.text }}
                     </span>
                     <span v-if="workStatus.isActive" class="text-zinc-400">{{ workStatus.timeLeft }} LEFT</span>
                </div>
            </div>

            <!-- Network -->
            <div class="bg-zinc-900/50 rounded-sm border border-zinc-800 p-4 font-mono text-xs relative overflow-hidden flex flex-col justify-center">
                <div class="flex justify-between items-center mb-1"><span class="text-zinc-500">IP:</span><span class="text-emerald-300 bg-emerald-900/10 px-1 rounded">{{ networkInfo.ip }}</span></div>
                <div class="flex justify-between items-center mb-1"><span class="text-zinc-500">LOC:</span><span class="text-zinc-300 truncate max-w-[150px]">{{ networkInfo.flag }} {{ networkInfo.location }}</span></div>
                <div class="flex justify-between items-center"><span class="text-zinc-500">ISP:</span><span class="text-zinc-400 truncate max-w-[150px]">{{ networkInfo.isp }}</span></div>
            </div>
        </div>

        <!-- NOTES WIDGET (Container) -->
        <NotesWidget 
            :dashboard-id="dashboardName" 
            :allow-notes="dashboardName !== 'default'"
            class="flex-1 shadow-2xl shadow-black/50"
        >
            <template #links-content>
                <!-- Search -->
                <div class="relative group mb-6 sticky top-0 z-20">
                    <div class="absolute -inset-0.5 bg-emerald-500/20 rounded opacity-0 group-hover:opacity-100 transition duration-500 blur"></div>
                    <div class="relative flex items-center bg-zinc-900 border border-zinc-700 rounded-sm shadow-xl">
                        <span class="pl-4 text-emerald-500 font-mono">></span>
                        <input v-model="searchQuery" type="text" placeholder="INPUT_COMMAND..." class="w-full bg-transparent text-emerald-100 px-4 py-3 focus:outline-none font-mono placeholder:text-zinc-600 uppercase">
                    </div>
                </div>

                <div v-if="filteredCategories.length === 0" class="text-center text-zinc-600 font-mono py-10 border border-dashed border-zinc-800">NO_DATA_FOUND</div>
                
                <!-- Cards Grid -->
                <div v-for="(cat, index) in filteredCategories" :key="index" class="mb-8 last:mb-0">
                    <h2 class="text-sm font-bold text-zinc-500 mb-4 font-mono uppercase tracking-widest border-l-2 border-emerald-500/50 pl-3 sticky top-16 bg-zinc-950/90 py-1 z-10 backdrop-blur w-fit pr-4 rounded-r">
                        {{ cat.title }}
                    </h2>
                    <div class="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4">
                        <ServiceCard v-for="item in cat.items" :key="item.name" :item="item" :is-local-user="isLocalUser" />
                    </div>
                </div>
            </template>
        </NotesWidget>
      </main>

      <!-- ASIDE (RIGHT) -->
      <aside class="lg:col-span-1 space-y-8 overflow-y-auto h-full pr-1 custom-scrollbar">
        <template v-if="dashboardName === 'default'">
            <WeatherWidget />
        </template>
        <template v-else>
            <TelegramWidget v-if="isTelegramEnabled" :dashboard-id="dashboardName" @error-change="(val) => telegramError = val" />
            <div v-else class="text-center py-4 border border-zinc-800 border-dashed rounded text-zinc-700 text-[10px] font-mono">[ TG DISABLED ]</div>
        </template>

        <!-- TIMERS -->
        <div class="space-y-6">
            <div class="flex items-center justify-between text-[10px] font-mono font-bold text-zinc-500 uppercase tracking-widest">
                <span class="flex items-center gap-2">Targets <span v-if="calendarError" class="text-red-500 bg-red-900/20 px-1 rounded animate-pulse">⚠ SYNC FAIL</span></span>
                <span class="text-emerald-500/50">///</span>
            </div>

            <div v-if="isEventsLoading && rawEvents.length === 0" class="text-zinc-600 text-[10px] font-mono italic p-4 border border-zinc-800 border-dashed text-center">LOADING...</div>
            <div v-else>
                <!-- TODAY -->
                <div class="flex flex-col gap-3 mb-6">
                   <div class="flex items-center justify-between text-[10px] font-mono font-bold text-emerald-500 uppercase tracking-widest border-b border-emerald-500/20 pb-1"><span>TODAY</span></div>
                   <div v-if="groupedEvents.today.length > 0" class="flex flex-col gap-3">
                       <CountdownTimer v-for="event in groupedEvents.today" :key="event.name+event.date" :event="event" :urgency-settings="fullConfig.settings?.urgency"/>
                   </div>
                   <div v-else class="text-zinc-700 font-mono text-[10px] py-2 pl-2 border-l border-zinc-800 italic">NO_ACTIVE_TARGETS</div>
                </div>
                <!-- TOMORROW -->
                <div class="flex flex-col gap-3 mb-6">
                   <div class="flex items-center justify-between text-[10px] font-mono font-bold text-zinc-500 uppercase tracking-widest border-b border-zinc-800 pb-1"><span>TOMORROW</span></div>
                   <div v-if="groupedEvents.tomorrow.length > 0" class="flex flex-col gap-3">
                       <CountdownTimer v-for="event in groupedEvents.tomorrow" :key="event.name+event.date" :event="event" :urgency-settings="fullConfig.settings?.urgency" />
                   </div>
                   <div v-else class="text-zinc-700 font-mono text-[10px] py-2 pl-2 border-l border-zinc-800 italic">SCHEDULE_CLEAR</div>
                </div>
                <!-- UPCOMING (Manual Only) -->
                <div v-if="groupedEvents.upcoming.length > 0" class="flex flex-col gap-3">
                   <div class="flex items-center justify-between text-[10px] font-mono font-bold text-zinc-600 uppercase tracking-widest border-b border-zinc-800 pb-1"><span>UPCOMING</span></div>
                   <CountdownTimer v-for="event in groupedEvents.upcoming" :key="event.name+event.date" :event="event" :urgency-settings="fullConfig.settings?.urgency" />
                </div>
            </div>
        </div>
      </aside>
    </div>
  </div>
</template>