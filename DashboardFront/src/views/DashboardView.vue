<script setup>
import { ref, computed, watch, onMounted, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'
import { request } from '@/api'
import { widgetRegistry } from '@/config/widgets'
import { useSignalR } from '@/composables/useSignalR'

// --- LAYOUT COMPONENTS ---
import TheHeader from '@/components/dashboard/TheHeader.vue'
import TheSidebar from '@/components/dashboard/TheSidebar.vue'
import TimeStatusWidget from '@/components/dashboard/TimeStatusWidget.vue'
import NetworkWidget from '@/components/dashboard/NetworkWidget.vue'
import FloatingWindow from '@/components/FloatingWindow.vue'

// --- WIDGETS (For Floating Windows) ---
import NotesWidget from '@/components/NotesWidget.vue'
import ServiceCard from '@/components/ServiceCard.vue'
import AlarmOverlay from '@/components/AlarmOverlay.vue'

// --- MODALS ---
import DashboardManagerModal from '@/components/DashboardManagerModal.vue'
import SettingsModal from '@/components/SettingsModal.vue'
import MenuEditorModal from '@/components/MenuEditorModal.vue'

const props = defineProps(['dashboardId'])
const router = useRouter()
const { start, stop, on, off, isConnected } = useSignalR() 

// ==========================================
// STATE
// ==========================================
const myDashboards = ref([])
const dashboardData = ref({
  id: 0,
  title: 'LOADING...',
  isPublic: false,
  categories: [],
  schedule: {},
  notes: [],
  manualEvents: [],
  calendars: [],
  activeIntegrations: [],
  urgency: null,
  widgetLayout: null,
  headerLayout: null,
  myRole: 'Viewer',
  teamMembers: [] 
})

// UI Flags
const isLoading = ref(true)
const isDashManagerOpen = ref(false)
const isSettingsOpen = ref(false)
const isMenuEditorOpen = ref(false)
const searchQuery = ref('')
const isPreviewOpen = ref(false)
const previewUrl = ref('')

// Floating Widgets
const floatingWidgets = ref(new Set())
const getInitialX = () => (typeof window !== 'undefined') ? (window.innerWidth / 2 - 160) : 100

// Refs
const notesWidgetRef = ref(null)

// Auth
const isAuthenticated = !!localStorage.getItem('jwt_token')
const isLocalUser = ref(false)

// Errors
const telegramError = ref(false)

// Work/Break Logic
const workStatus = ref({ isActive: false, state: 'IDLE', elapsed: '00:00:00', remaining: '00:00:00', breakTime: '00:00:00', percent: 0 })
const breakState = ref({ isBreak: false, breakStartTs: 0, totalBreakMs: 0, currentSessionMs: 0, lastUpdateDate: new Date().toDateString() })

// Alarm State
const activeAlarm = ref(null)

// ==========================================
// ROLES & COMPUTED
// ==========================================
const canEditSettings = computed(() => dashboardData.value.myRole === 'Owner')
const canEditContent = computed(() => ['Owner', 'Editor'].includes(dashboardData.value.myRole))

const filteredCategories = computed(() => {
  if (!searchQuery.value) return dashboardData.value.categories || [];
  const query = searchQuery.value.toLowerCase()
  return dashboardData.value.categories.map(cat => {
    if (cat.title.toLowerCase().includes(query)) return cat;
    const items = cat.items.filter(item => 
       item.name.toLowerCase().includes(query) || (item.description && item.description.toLowerCase().includes(query))
    );
    return { ...cat, items }
  }).filter(c => c.items.length > 0)
})

// ==========================================
// ACTIONS & SMART UPDATES
// ==========================================

// 1. Обработчики Сокетов
const handleRefreshLayout = async () => {
    console.log("[Dashboard] Layout updated remotely")
    await loadData() // Полная перезагрузка конфига
}

const handleRefreshContent = async () => {
    console.log("[Dashboard] Content structure updated")
    await loadData() // Перезагружаем категории и ссылки
}

const handleRefreshNotes = async () => {
    // Атомарное обновление заметок без перезагрузки всего приложения
    try {
        const notes = await request(`/content/${dashboardData.value.id}/notes`)
        dashboardData.value.notes = notes
    } catch(e) { console.error(e) }
}

const handleAlarm = (data) => {
// Проверяем настройку в localStorage (она меняется в RemindersWidget)
    const isScreenEnabled = localStorage.getItem('reminders_overlay') !== 'false'
    
    if (isScreenEnabled) {
        activeAlarm.value = data
    } else {
        console.log("Alarm received but screen blocked by settings")
    }
    
    // Звук играем всегда (или тоже можно добавить настройку)
    const ctx = new (window.AudioContext || window.webkitAudioContext)()
    const osc = ctx.createOscillator(); const gain = ctx.createGain()
    osc.connect(gain); gain.connect(ctx.destination)
    osc.type = 'square'; osc.frequency.setValueAtTime(880, ctx.currentTime)
    gain.gain.setValueAtTime(0.1, ctx.currentTime)
    osc.start(); osc.stop(ctx.currentTime + 0.5)
}

// 2. Загрузка Данных
const loadData = async () => {
  isLoading.value = true
  const id = props.dashboardId || 1 
  try {
    const data = await request(`/dashboards/${id}`)
    
    // Fallbacks
    data.manualEvents = data.manualEvents || []
    data.calendars = data.calendars || []
    data.teamMembers = data.teamMembers || []
    
    dashboardData.value = data
    
    // Start Socket connection specific to this dashboard group
    await start(id)

    try { const who = await request('/whoami'); isLocalUser.value = who.isLocal } catch {}
    await fetchStatus()
  } catch (e) {
    if (e.message === 'Unauthorized') return
    if (e.message.includes('404')) alert('Dashboard not found')
  } finally {
    isLoading.value = false
  }
}

// 3. UI Actions
const toggleFloating = (id) => {
    if (floatingWidgets.value.has(id)) floatingWidgets.value.delete(id)
    else floatingWidgets.value.add(id)
}

const handleGlobalKeydown = (e) => {
    if (!e.altKey) return
    const widgetId = Object.keys(widgetRegistry).find(id => widgetRegistry[id].shortcut === e.code)
    if (widgetId) {
        e.preventDefault()
        let isAllowed = false
        if (!dashboardData.value.headerLayout) {
             if (['calculator', 'telegram', 'userstatus'].includes(widgetId)) isAllowed = true
        } else {
             try {
                 const layout = JSON.parse(dashboardData.value.headerLayout)
                 const conf = layout.find(w => w.id === widgetId)
                 if (conf && conf.enabled) isAllowed = true
             } catch(e) {}
        }
        if (isAllowed) toggleFloating(widgetId)
    }
}

// 4. Timer Logic
const formatDuration = (ms) => {     
     if (ms < 0) ms = 0
     const s = Math.floor(ms/1000); const h = Math.floor(s/3600); const m = Math.floor((s%3600)/60); const sec = s%60; const pad = n => n.toString().padStart(2,'0'); 
     return `${pad(h)}:${pad(m)}:${pad(sec)}`
}

const updateWorkStatus = () => {
    const currentTotalBreak = breakState.value.totalBreakMs + breakState.value.currentSessionMs
    if (breakState.value.isBreak) { 
         workStatus.value = { state: 'BREAK', elapsed: formatDuration(breakState.value.currentSessionMs), remaining: 'PAUSED', breakTime: formatDuration(currentTotalBreak), percent: 0, isActive: true }
         return
    }
    const settings = dashboardData.value.schedule
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
        const data = await request(`/status/${dashboardData.value.id}`)
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
    try { await request(`/status/${dashboardData.value.id}`, { method: 'POST', body: JSON.stringify(payload) }) } catch(e){}
}

const toggleBreak = () => {
    const now = Date.now()
    if (breakState.value.isBreak) {
        breakState.value.isBreak = false; breakState.value.totalBreakMs += (now - breakState.value.breakStartTs); breakState.value.breakStartTs = 0; breakState.value.currentSessionMs = 0
    } else {
        breakState.value.isBreak = true; breakState.value.breakStartTs = now; breakState.value.currentSessionMs = 0
    }
    saveStatus(); updateWorkStatus()
}

const tickBreakTimer = () => { if (breakState.value.isBreak && breakState.value.breakStartTs > 0) breakState.value.currentSessionMs = Date.now() - breakState.value.breakStartTs }
const logout = () => { localStorage.removeItem('jwt_token'); router.push('/login') }
const handleWidgetRefresh = () => loadData()
const loadDashboardList = async () => { if (!isAuthenticated) return; try { myDashboards.value = await request('/dashboards/list') } catch (e) {} }
const handlePreview = (url) => { 
    if (notesWidgetRef.value) {
        notesWidgetRef.value.openPreview(url)
    }
}
const handleAlarmDismiss = () => { activeAlarm.value = null }

// ==========================================
// LIFECYCLE
// ==========================================
let workInt
watch(() => props.dashboardId, () => loadData(), { immediate: true })

onMounted(() => {
  loadDashboardList()
  workInt = setInterval(() => { updateWorkStatus(); tickBreakTimer() }, 1000)
  window.addEventListener('keydown', handleGlobalKeydown)
  
  // REGISTER LISTENERS
  on('layout', handleRefreshLayout)
  on('content', handleRefreshContent)
  on('notes', handleRefreshNotes)
  on('alarm', handleAlarm)
})

onUnmounted(() => {
    clearInterval(workInt)
    window.removeEventListener('keydown', handleGlobalKeydown)
    
    // CLEANUP
    off('layout', handleRefreshLayout)
    off('content', handleRefreshContent)
    off('notes', handleRefreshNotes)
    off('alarm', handleAlarm)
    stop() // Close Socket connection
})
</script>

<template>
  <div class="min-h-screen p-4 md:p-8 max-w-[1800px] mx-auto font-sans text-zinc-300">
    <!-- MODALS -->
    <DashboardManagerModal :is-open="isDashManagerOpen" :current-id="dashboardData.id" :dashboards="myDashboards" @close="isDashManagerOpen = false" @refresh="loadDashboardList" />
    <SettingsModal :is-open="isSettingsOpen" :dashboard-id="dashboardData.id" :config="dashboardData" @close="isSettingsOpen = false" @refresh="loadData" />
    <MenuEditorModal :is-open="isMenuEditorOpen" :dashboard-id="dashboardData.id" :initial-categories="dashboardData.categories" @close="isMenuEditorOpen = false" @refresh="loadData" />
    
    <!-- ALARM OVERLAY -->
    <Teleport to="body">
        <AlarmOverlay :is-open="!!activeAlarm" :message="activeAlarm?.message" @dismiss="handleAlarmDismiss" />
    </Teleport>

    <!-- FLOATING WIDGETS -->
    <FloatingWindow 
         v-for="id in floatingWidgets" 
         :key="id" 
         :title="widgetRegistry[id].name" 
         :initial-x="getInitialX()" 
         :initial-y="100" 
         @close="floatingWidgets.delete(id)"
    >
        <component 
             :is="widgetRegistry[id].comp" 
             :dashboard-id="dashboardData.id" 
             @error-change="v => telegramError = v"
             @refresh="handleWidgetRefresh"
             :events-grouped="{}" 
             :urgency-settings="dashboardData.urgency"
             :manual-events="dashboardData.manualEvents"
             :calendars="dashboardData.calendars"
             :team-members="dashboardData.teamMembers" 
        />
    </FloatingWindow>

    <!-- HEADER -->
    <TheHeader 
        :title="dashboardData.title" 
        :is-public="dashboardData.isPublic" 
        :floating-widgets="floatingWidgets" 
        :header-layout="dashboardData.headerLayout"
        :show-settings="canEditSettings" 
        
        :socket-connected="isConnected"
        
        @open-nav="isDashManagerOpen = true"
        @open-links="isMenuEditorOpen = true"
        @open-settings="isSettingsOpen = true"
        @toggle-floating="toggleFloating"
        @logout="logout"
    />

    <!-- MAIN GRID -->
    <div v-if="isLoading" class="text-center mt-20 text-emerald-500 font-mono animate-pulse">INITIALIZING NEURAL LINK...</div>
    
    <div v-else class="grid grid-cols-1 lg:grid-cols-4 gap-6 min-h-[calc(100vh-140px)]">
            
      <!-- LEFT COLUMN -->
      <main class="lg:col-span-3 flex flex-col gap-6 h-full overflow-hidden">
        
        <div class="grid grid-cols-1 md:grid-cols-5 gap-6 flex-shrink-0 min-h-[140px]">
            <TimeStatusWidget 
                 class="md:col-span-3" 
                 :dashboard-id="dashboardData.id" 
                 :schedule="dashboardData.schedule" 
                 :active-integrations="dashboardData.activeIntegrations" 
                 :work-status="workStatus"
                 :break-state="breakState"
                 @toggle-break="toggleBreak"
            />
            <NetworkWidget class="md:col-span-2" />
        </div>

        <NotesWidget 
           ref="notesWidgetRef" 
           class="flex-1 shadow-2xl min-h-[600px] h-full" 
           :dashboard-id="dashboardData.id" 
           :initial-notes="dashboardData.notes" 
           :allow-edit="canEditContent"
        >
          <template #links-content>
             <div class="overflow-y-auto h-full pr-2 custom-scrollbar pb-10">
               <div class="sticky top-0 z-10 bg-zinc-900/90 backdrop-blur pb-4 pt-2 mb-2 border-b border-zinc-800 flex gap-2">
                   <div class="relative flex-1 flex items-center bg-zinc-950 border border-zinc-700 rounded-sm px-3 focus-within:border-emerald-500 transition">
                      <span class="text-emerald-500 font-mono mr-2">></span>
                      <input v-model="searchQuery" type="text" placeholder="FILTER_SERVICES..." class="w-full bg-transparent text-emerald-100 py-2 text-xs focus:outline-none font-mono uppercase placeholder:text-zinc-700">
                   </div>
                   <button v-if="canEditContent" @click="isMenuEditorOpen = true" class="bg-zinc-950 border border-zinc-700 rounded-sm px-3 text-zinc-500 hover:text-emerald-400 hover:border-emerald-500 transition">⚙</button>
               </div>
               
               <div v-for="(cat, idx) in filteredCategories" :key="idx" class="mb-8">
                 <h2 class="text-xs font-bold text-zinc-500 mb-3 font-mono uppercase tracking-widest border-l-2 border-emerald-500/50 pl-3">{{ cat.title }}</h2>
                 <div class="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-3">
                    <ServiceCard v-for="item in cat.items" :key="item.name" :item="item" :is-local-user="isLocalUser" @open-preview="handlePreview" />
                 </div>
               </div>
             </div>
          </template>
        </NotesWidget>
      </main>

      <!-- RIGHT SIDEBAR -->
      <TheSidebar 
         :dashboard-id="dashboardData.id"
         :widget-layout="dashboardData.widgetLayout"
         :active-integrations="dashboardData.activeIntegrations"
         :urgency="dashboardData.urgency"
         :manual-events="dashboardData.manualEvents"
         :calendars="dashboardData.calendars"
         :team-members="dashboardData.teamMembers" 
         @refresh="handleWidgetRefresh"
         @error-change="v => telegramError = v"
      />
    </div>
  </div>
</template>

<style scoped>
.custom-scrollbar::-webkit-scrollbar { width: 4px; background: transparent; }
.custom-scrollbar::-webkit-scrollbar-thumb { background: #3f3f46; border-radius: 2px; }
.custom-scrollbar::-webkit-scrollbar-thumb:hover { background: #10b981; }
</style>