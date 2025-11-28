<script setup>
import { ref, computed, watch, onMounted, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'
import ServiceCard from '@/components/ServiceCard.vue'
import CountdownTimer from '@/components/CountdownTimer.vue'
import ConfigEditor from '@/components/ConfigEditor.vue'
import TelegramWidget from '@/components/TelegramWidget.vue'
import WeatherWidget from '@/components/WeatherWidget.vue'

const props = defineProps(['dashboardId'])
const router = useRouter()

const dashboardName = computed(() => {
  if (!props.dashboardId || props.dashboardId === 'undefined' || props.dashboardId === '') {
    return 'default'
  }
  return props.dashboardId
})

const availableDashboards = ref(['default'])
const categories = ref([])
const rawEvents = ref([])
const isLocalUser = ref(false)
const networkInfo = ref({ ip: '...', location: '...', isp: '...', flag: '' })
const weather = ref(null)
const time = ref('')
const dateStr = ref('')

// Разделяем состояния загрузки
const isConfigLoading = ref(true) // Блокирует весь экран (только для конфига)
const isEventsLoading = ref(false) // Локальный лоадер для таймеров

const isEditorOpen = ref(false)
const fullConfig = ref({})
const calendarError = ref(false)
const telegramError = ref(false)

const systemStatus = computed(() => {
    if (calendarError.value || telegramError.value) return 'WARNING'
    return 'ONLINE'
})

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
      if (ev.source === 'Manual') groups.upcoming.push(ev)
    }
  })
  return groups
})

const loadDashboardList = async () => {
  try { availableDashboards.value = await (await fetch('/api/dashboards')).json() } catch (e) {}
}

// --- АСИНХРОННАЯ ЗАГРУЗКА ЧАСТЕЙ ---


const fetchNetworkInfo = async () => {
    if (networkInfo.value.ip !== '...') return // Уже загружено
    try {
       const res = await fetch('/api/whoami')
       const data = await res.json()
       isLocalUser.value = data.isLocal
       networkInfo.value.ip = data.ip
       
       if (!data.isLocal) {
          fetch(`https://ipwho.is/${data.ip}`).then(r=>r.json()).then(geo => {
             if(geo.success) {
                networkInfo.value.location = `${geo.city}, ${geo.country_code}`
                networkInfo.value.flag = geo.flag.emoji
                networkInfo.value.isp = geo.connection.isp
             }
          })
       } else {
          networkInfo.value.location = 'Local Network'; networkInfo.value.flag = '🏠'; networkInfo.value.isp = 'HomeLab';
       }
    } catch (e) {}
}

// --- ГЛАВНАЯ ЗАГРУЗКА ---
const loadData = async () => {
  isConfigLoading.value = true
  categories.value = []
  rawEvents.value = [] // Сбрасываем события при смене дашборда
  calendarError.value = false
  telegramError.value = false
  
  const currentDash = dashboardName.value
  
  let attempts = 0
  const maxAttempts = 3
  let isAuthSuccess = false
  
  try {
    // 1. Блокирующая загрузка только КОНФИГА
    while (attempts < maxAttempts && !isAuthSuccess) {
        attempts++
        const configRes = await fetch(`/api/config/${currentDash}?t=${Date.now()}`)
        
        if (configRes.ok) {
            const configData = await configRes.json()
            fullConfig.value = configData
            categories.value = configData.categories || []
            isAuthSuccess = true
        } else if (configRes.status === 401) {
            console.warn(`Auth attempt ${attempts} canceled/failed.`)
        } else {
            if (configRes.status === 404) {
                 alert(`Дашборд "${currentDash}" не найден.`)
                 if (currentDash !== 'default') router.push('/')
                 return
            }
            throw new Error(`Server Error: ${configRes.status}`)
        }
    }

    if (!isAuthSuccess) {
        alert("Доступ запрещен.")
        if (currentDash !== 'default') router.push('/')
        return
    }

    // --- СТРАНИЦА УЖЕ ГОТОВА К ПОКАЗУ ---
    isConfigLoading.value = false 

    // 2. Запускаем тяжелые запросы ПАРАЛЛЕЛЬНО (не блокируя интерфейс)
    fetchDashboardEvents() // Календарь
    fetchNetworkInfo()     // IP

  } catch (e) {
    console.error("Critical Load Error:", e)
    if (currentDash !== 'default') router.push('/')
    isConfigLoading.value = false
  }
}

watch(() => props.dashboardId, () => loadData(), { immediate: true })

const updateTime = () => {
  const now = new Date()
  time.value = now.toLocaleTimeString([], { hour: '2-digit', minute:'2-digit', second: '2-digit' })
  dateStr.value = now.toLocaleDateString('ru-RU', { weekday: 'short', day: 'numeric', month: 'long' })
}

// Погода (маленькая в шапке) - Тоже новые координаты
const getWeather = async () => {
  try { weather.value = (await (await fetch('https://api.open-meteo.com/v1/forecast?latitude=54.3282&longitude=48.3866&current_weather=true')).json()).current_weather } catch (e) {}
}

let timerInt
let eventsInt

onMounted(() => { 
  loadDashboardList(); 
  getWeather(); 
  timerInt = setInterval(updateTime, 1000); 
  updateTime() 
  // Интервал 10 минут (600000 ms)
  eventsInt = setInterval(fetchDashboardEvents, 600000)
})

onUnmounted(() => {
    clearInterval(timerInt)
    clearInterval(eventsInt)
})

const searchQuery = ref('')
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
  await fetch(`/api/config/${safeName}`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ settings: { title: safeName, auth: { username: "admin", password: "123" } }, categories: [], events: [] }) })
  await loadDashboardList()
  router.push(`/${safeName}`)
}

const onConfigSaved = () => { isEditorOpen.value = false; loadData() }

// Добавь новую переменную состояния
const isFetchingEvents = ref(false) // <-- СЕМАФОР

const fetchDashboardEvents = async () => {
    // Если уже качаем - стоп
    if (isFetchingEvents.value) return 
    
    isFetchingEvents.value = true
    
    // Показываем лоадер UI только если данных вообще нет
    if (rawEvents.value.length === 0) isEventsLoading.value = true;
    
    try {
        const target = dashboardName.value || 'default'
        const res = await fetch(`/api/events/${target}?t=${Date.now()}`)
        if (!res.ok) throw new Error("Fetch failed")
        
        rawEvents.value = await res.json()
        calendarError.value = false 
    } catch(e) { 
        console.error("Auto-refresh events error", e)
        calendarError.value = true
    } finally {
        isEventsLoading.value = false
        isFetchingEvents.value = false // <-- ОСВОБОЖДАЕМ СЕМАФОР
    }
}
</script>

<template>
  <div class="min-h-screen p-6 md:p-10 max-w-[1600px] mx-auto font-sans">
    <ConfigEditor :is-open="isEditorOpen" :config-data="fullConfig" @close="isEditorOpen = false" @save="onConfigSaved" />
    
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
        <button @click="createNew" class="text-zinc-500 hover:text-emerald-400 font-mono text-xs border border-zinc-800 hover:border-emerald-500 px-2 py-1 rounded transition">[+]</button>
        <button @click="isEditorOpen = true" class="text-[10px] font-mono text-zinc-600 hover:text-emerald-400 border border-zinc-800 hover:border-emerald-500/50 px-2 py-1 rounded transition ml-2">[EDIT_CONFIG]</button>
      </div>
    </header>

    <!-- ГЛАВНЫЙ ЛОАДЕР: ТОЛЬКО ДЛЯ КОНФИГА -->
    <div v-if="isConfigLoading" class="text-center text-emerald-500/50 mt-20 font-mono animate-pulse">
        INITIALIZING SYSTEM...
    </div>

    <!-- КОНТЕНТ ПОЯВЛЯЕТСЯ СРАЗУ ПОСЛЕ ЗАГРУЗКИ КОНФИГА -->
    <div v-else class="grid grid-cols-1 lg:grid-cols-4 gap-10 items-start">
      
      <!-- MAIN (LEFT) -->
      <main class="lg:col-span-3 space-y-8">
        <!-- INFO BLOCK -->
        <div class="grid grid-cols-1 md:grid-cols-2 gap-6">
            <div class="bg-zinc-900/80 rounded-sm p-4 border border-zinc-800 flex items-center justify-between relative overflow-hidden group">
                <div class="absolute top-0 left-0 w-1 h-full bg-emerald-500/20"></div>
                <div>
                    <div class="text-3xl font-mono font-bold text-emerald-400 tracking-wider mb-1">{{ time }}</div>
                    <div class="text-zinc-500 font-mono text-[10px] uppercase tracking-widest">{{ dateStr }}</div>
                </div>
                <div v-if="weather" class="text-right">
                     <span class="text-2xl text-emerald-500/80 font-mono">{{ weather.temperature }}°</span>
                     <div class="text-[10px] text-zinc-600">TEMP</div>
                </div>
            </div>
            <div class="bg-zinc-900/50 rounded-sm border border-zinc-800 p-4 font-mono text-xs relative overflow-hidden flex flex-col justify-center">
                <div class="flex justify-between items-center mb-1"><span class="text-zinc-500">IP:</span><span class="text-emerald-300 bg-emerald-900/10 px-1 rounded">{{ networkInfo.ip }}</span></div>
                <div class="flex justify-between items-center mb-1"><span class="text-zinc-500">LOC:</span><span class="text-zinc-300 truncate max-w-[150px]">{{ networkInfo.flag }} {{ networkInfo.location }}</span></div>
                 <div class="flex justify-between items-center"><span class="text-zinc-500">ISP:</span><span class="text-zinc-400 truncate max-w-[150px]">{{ networkInfo.isp }}</span></div>
            </div>
        </div>

        <!-- SEARCH -->
        <div class="relative group">
          <div class="absolute -inset-0.5 bg-emerald-500/20 rounded opacity-0 group-hover:opacity-100 transition duration-500 blur"></div>
          <div class="relative flex items-center bg-zinc-900 border border-zinc-700 rounded-sm">
            <span class="pl-4 text-emerald-500 font-mono">></span>
            <input v-model="searchQuery" type="text" placeholder="INPUT_COMMAND..." class="w-full bg-transparent text-emerald-100 px-4 py-3 focus:outline-none font-mono placeholder:text-zinc-600 uppercase">
          </div>
        </div>

        <!-- LINKS -->
        <div v-if="filteredCategories.length === 0" class="text-center text-zinc-600 font-mono py-10 border border-dashed border-zinc-800">NO_DATA_FOUND</div>
        <div v-for="(cat, index) in filteredCategories" :key="index">
          <h2 class="text-sm font-bold text-zinc-500 mb-4 font-mono uppercase tracking-widest border-l-2 border-emerald-500/50 pl-3">{{ cat.title }}</h2>
          <div class="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4">
            <ServiceCard v-for="item in cat.items" :key="item.name" :item="item" :is-local="isLocalUser" />
          </div>
        </div>
      </main>

      <!-- ASIDE (RIGHT) -->
      <aside class="lg:col-span-1 space-y-8">
        
        <!-- WIDGETS -->
        <template v-if="dashboardName === 'default'">
            <WeatherWidget />
        </template>
        <template v-else>
            <TelegramWidget :dashboard-id="dashboardName" @error-change="(val) => telegramError = val" />
        </template>

        <!-- TIMERS -->
        <div class="space-y-6">
            <div class="flex items-center justify-between text-[10px] font-mono font-bold text-zinc-500 uppercase tracking-widest">
                <span class="flex items-center gap-2">
                    Targets 
                    <span v-if="calendarError" class="text-red-500 bg-red-900/20 px-1 rounded animate-pulse">⚠ SYNC FAIL</span>
                    <span v-if="isEventsLoading" class="text-zinc-600 animate-pulse font-normal lowercase ml-1">syncing...</span>
                </span>
                <span class="text-emerald-500/50">///</span>
            </div>

            <!-- ЛОАДЕР (ПОКАЗЫВАЕТСЯ ПРИ ПЕРВОЙ ЗАГРУЗКЕ СОБЫТИЙ) -->
            <div v-if="isEventsLoading && rawEvents.length === 0" class="text-zinc-600 text-[10px] font-mono italic p-4 border border-zinc-800 border-dashed text-center">
                LOADING_CALENDAR_DATA...
            </div>

            <div v-else>
                <!-- TODAY -->
                <div class="flex flex-col gap-3 mb-6">
                   <div class="flex items-center justify-between text-[10px] font-mono font-bold text-emerald-500 uppercase tracking-widest border-b border-emerald-500/20 pb-1"><span>TODAY</span></div>
                   <div v-if="groupedEvents.today.length > 0" class="flex flex-col gap-3"><CountdownTimer v-for="event in groupedEvents.today" :key="event.name + event.date" :event="event" /></div>
                   <div v-else class="text-zinc-700 font-mono text-[10px] py-2 pl-2 border-l border-zinc-800 italic">NO_ACTIVE_TARGETS</div>
                </div>

                <!-- TOMORROW -->
                <div class="flex flex-col gap-3 mb-6">
                   <div class="flex items-center justify-between text-[10px] font-mono font-bold text-zinc-500 uppercase tracking-widest border-b border-zinc-800 pb-1"><span>TOMORROW</span></div>
                   <div v-if="groupedEvents.tomorrow.length > 0" class="flex flex-col gap-3"><CountdownTimer v-for="event in groupedEvents.tomorrow" :key="event.name + event.date" :event="event" /></div>
                   <div v-else class="text-zinc-700 font-mono text-[10px] py-2 pl-2 border-l border-zinc-800 italic">SCHEDULE_CLEAR</div>
                </div>

                <!-- UPCOMING -->
                <div v-if="groupedEvents.upcoming.length > 0" class="flex flex-col gap-3">
                   <div class="flex items-center justify-between text-[10px] font-mono font-bold text-zinc-600 uppercase tracking-widest border-b border-zinc-800 pb-1"><span>UPCOMING</span></div>
                   <CountdownTimer v-for="event in groupedEvents.upcoming" :key="event.name + event.date" :event="event" />
                </div>
            </div>
        </div>
      </aside>
    </div>
  </div>
</template>