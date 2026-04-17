<script setup>
import { computed, ref, onMounted, onUnmounted, watch } from 'vue'
import { request } from '@/api'
import { widgetRegistry } from '@/config/widgets'
import { useSignalR } from '@/composables/useSignalR'

const props = defineProps({
  dashboardId: Number,
  widgetLayout: String,
  activeIntegrations: { type: Array, default: () => [] },
  urgency: Object,
  // Внутренние данные для виджетов
  manualEvents: Array, 
  calendars: Array,
  teamMembers: { type: Array, default: () => [] } // <--- Приходит из DashboardView
})

const emit = defineEmits(['refresh', 'error-change'])
const { on, off } = useSignalR()

// --- EVENTS LOGIC (Polling Removed -> Sockets) ---
const eventsList = ref([])
const calendarError = ref(false)

const fetchEvents = async () => {
  try {
    const data = await request(`/events/${props.dashboardId}`)
    eventsList.value = data.map(e => ({ ...e, date: new Date(e.date) }))
    calendarError.value = false
  } catch (e) { calendarError.value = true }
}

const handleCalendarUpdate = () => {
    console.log("[Sidebar] Calendar Update Signal")
    fetchEvents()
}

const eventsGrouped = computed(() => {
  const now = new Date();
  const todayStart = new Date(now.getFullYear(), now.getMonth(), now.getDate());
  const tomorrowStart = new Date(todayStart); tomorrowStart.setDate(todayStart.getDate() + 1);
  const afterTomorrowStart = new Date(todayStart); afterTomorrowStart.setDate(todayStart.getDate() + 2);
  
  const groups = { today: [], tomorrow: [], upcoming: [] }
  
  eventsList.value.forEach(ev => {
    if (ev.date < now && ev.source !== 'Manual') return 
    if (ev.date >= todayStart && ev.date < tomorrowStart) groups.today.push(ev)
    else if (ev.date >= tomorrowStart && ev.date < afterTomorrowStart) groups.tomorrow.push(ev)
    else groups.upcoming.push(ev)
  })
  return groups
})

let evInt
watch(() => props.dashboardId, fetchEvents, { immediate: true })

onMounted(() => { 
    fetchEvents()
    // Слушаем сигнал 'calendar' от бекенда
    on('calendar', handleCalendarUpdate)
    // Оставляем редкий интервал (раз в 5 минут) на всякий случай
    evInt = setInterval(fetchEvents, 300000) 
})

onUnmounted(() => { 
    clearInterval(evInt)
    off('calendar', handleCalendarUpdate)
})

// --- WIDGETS LAYOUT ---
const activeWidgets = computed(() => {
    const defaultOrder = [
        { id: 'weather', enabled: true },
        { id: 'telegram', enabled: true },
        { id: 'events', enabled: true },
        { id: 'monitoring', enabled: true },
        { id: 'userstatus', enabled: true }
    ]
    let layout = defaultOrder
    try {
        if (props.widgetLayout) layout = JSON.parse(props.widgetLayout)
    } catch(e) {}
    
    return layout.filter(w => w.enabled && widgetRegistry[w.id])
})
</script>

<template>
  <aside class="lg:col-span-1 flex flex-col gap-6 overflow-y-auto custom-scrollbar pr-1 pb-10 h-full">
    <component 
         v-for="widget in activeWidgets" 
         :key="widget.id"
        :is="widgetRegistry[widget.id].comp"
        :dashboard-id="dashboardId"
        
        @error-change="$emit('error-change', $event)"
        @refresh="$emit('refresh'); fetchEvents()"
        
        :events-grouped="eventsGrouped"
        :urgency-settings="urgency"
        :manual-events="manualEvents"
        :calendars="calendars"
        :team-members="teamMembers"
    />
  </aside>
</template>

<style scoped>
.custom-scrollbar::-webkit-scrollbar { width: 4px; }
.custom-scrollbar::-webkit-scrollbar-track { background: transparent; }
.custom-scrollbar::-webkit-scrollbar-thumb { background: #3f3f46; border-radius: 2px; }
.custom-scrollbar::-webkit-scrollbar-thumb:hover { background: #10b981; }
</style>