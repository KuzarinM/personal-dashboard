<script setup>
import { ref, onMounted, onUnmounted, computed } from 'vue'
import { request } from '@/api'
import { useSignalR } from '@/composables/useSignalR'
import MonitoringSettingsModal from '@/components/Wigets/Monitoring/MonitoringSettingsModal.vue'

const props = defineProps({ dashboardId: Number })
const { on, off } = useSignalR()

const monitors = ref([])
const loading = ref(true)
const isSettingsOpen = ref(false)
const isScanning = ref(false)

// Локальное время для обновления UI (чтобы таймер "1 min ago" тикал без запросов к API)
const now = ref(Date.now())

const activeCount = computed(() => monitors.value.filter(m => m.isActive).length)
const allActive = computed(() => activeCount.value > 0 && activeCount.value === monitors.value.length)

// --- FETCH DATA ---
const fetchStatus = async () => {
    // Включаем лоадер только если данных нет совсем (первый запуск)
    if (monitors.value.length === 0) loading.value = true
    try {
        const data = await request(`/monitoring/${props.dashboardId}`)
        updateList(data)
    } catch(e) {} 
    finally { loading.value = false }
}

const updateList = (data) => {
    monitors.value = data.sort((a, b) => {
        if (a.isActive !== b.isActive) return b.isActive - a.isActive 
        if (a.isUp !== b.isUp) return a.isUp - b.isUp
        return 0
    })
    // Обновляем локальный таймер, чтобы пересчитать computed значения (если бы они были)
    now.value = Date.now()
}

// --- ACTIONS ---
const toggleGlobal = async () => {
    const newState = !allActive.value
    // Тут лоадер можно, т.к. это действие юзера
    loading.value = true 
    try {
        const data = await request(`/monitoring/${props.dashboardId}/toggle-all?active=${newState}`, { method: 'POST' })
        updateList(data)
    } catch(e) { alert(e.message) }
    finally { loading.value = false }
}

const forceScan = async () => {
    isScanning.value = true
    try {
        const data = await request(`/monitoring/${props.dashboardId}/force-check`, { method: 'POST' })
        updateList(data)
    } catch(e) { alert(e.message) }
    finally { isScanning.value = false }
}

const formatTime = (ts) => {
    if (!ts) return 'PENDING'
    // Используем now.value чтобы Vue знал, что надо перерендерить этот кусок при тике таймера
    const _ = now.value 
    const date = new Date(ts)
    return date.toLocaleTimeString([], {hour: '2-digit', minute:'2-digit'})
}

// Socket Handler
const handleUpdate = () => {
    console.log("[Monitoring] Socket Signal Received")
    fetchStatus()
}

let uiInterval
onMounted(() => {
    fetchStatus()
    
    // ВАЖНО: Интервал теперь только обновляет переменную времени для UI, 
    // он НЕ делает сетевых запросов!
    uiInterval = setInterval(() => {
        now.value = Date.now()
    }, 60000)

    on('monitoring', handleUpdate)
})

onUnmounted(() => {
    clearInterval(uiInterval)
    off('monitoring', handleUpdate)
})
</script>

<template>
  <div class="space-y-3 relative group/widget">
    <MonitoringSettingsModal 
        :is-open="isSettingsOpen" 
        :dashboard-id="dashboardId" 
        @close="isSettingsOpen = false" 
        @refresh="fetchStatus" 
    />

    <!-- Header -->
    <div class="flex items-center justify-between text-[10px] font-mono font-bold text-purple-500 uppercase tracking-widest border-b border-purple-500/20 pb-1">
        <span class="flex items-center gap-2"><span>📡</span> UPTIME_SENSORS</span>
        
        <div class="flex items-center gap-2">
            <!-- Scan -->
            <button @click="forceScan" :disabled="isScanning" class="hover:text-purple-300 transition disabled:opacity-50" title="Force Scan Now">
                <span :class="{'animate-spin': isScanning}">⚡</span>
            </button>

            <!-- Toggle All -->
            <button @click="toggleGlobal" class="border border-purple-500/30 px-1.5 rounded hover:bg-purple-900/20 transition" 
                    :class="allActive ? 'text-purple-400' : 'text-zinc-500'"
                    :title="allActive ? 'Pause All Monitors' : 'Resume All Monitors'">
                {{ allActive ? 'ON' : 'PAUSED' }}
            </button>

            <button @click="isSettingsOpen = true" class="text-zinc-600 hover:text-purple-400 opacity-0 group-hover/widget:opacity-100 transition" title="Configure Sensors">⚙</button>
        </div>
    </div>

    <!-- Empty State -->
    <div v-if="monitors.length === 0 && !loading" class="text-zinc-700 text-[10px] font-mono italic text-center py-2 border border-dashed border-zinc-800 cursor-pointer hover:border-purple-500/50 hover:text-purple-400 transition" @click="isSettingsOpen = true">
        [+] ADD TARGETS
    </div>

    <!-- List -->
    <div v-else class="flex flex-col gap-1 max-h-60 overflow-y-auto custom-scrollbar">
        <div v-for="m in monitors" :key="m.id" 
             class="flex items-center justify-between p-2 rounded border transition text-xs font-mono group/item"
             :class="[
                 !m.isActive ? 'bg-zinc-900/30 border-zinc-800 text-zinc-600 opacity-60' :
                 m.isUp ? 'bg-zinc-900/50 border-zinc-800 hover:border-purple-500/30' :
                 'bg-red-900/10 border-red-900/50 text-red-200'
             ]"
        >
            <div class="flex flex-col overflow-hidden w-full">
                <div class="flex items-center gap-2">
                    <span class="w-1.5 h-1.5 rounded-full flex-shrink-0" 
                          :class="!m.isActive ? 'bg-zinc-700' : (m.isUp ? 'bg-emerald-500 shadow-[0_0_5px_#10b981]' : 'bg-red-500 animate-pulse')">
                    </span>
                    <span class="font-bold truncate" :title="m.target">{{ m.name }}</span>
                </div>
                
                <div class="flex justify-between items-center mt-1 text-[9px]">
                    <div class="flex gap-2 opacity-70">
                        <span>{{ m.type }}</span>
                        <span v-if="m.isActive && m.isUp" class="text-emerald-500/70">{{ m.responseTimeMs }}ms</span>
                        <span v-if="!m.isUp && m.isActive" class="text-red-400 font-bold">{{ m.lastError || 'TIMEOUT' }}</span>
                    </div>
                    <div class="text-zinc-600 text-right flex items-center gap-1">
                        <!-- Этот текст обновится только если изменится now.value или m.lastCheck -->
                        <span>{{ formatTime(m.lastCheck) }}</span>
                        <span v-if="m.isActive" class="bg-zinc-800 px-1 rounded">{{ m.intervalMin }}m</span>
                    </div>
                </div>
            </div>
        </div>
    </div>
  </div>
</template>

<style scoped>
.custom-scrollbar::-webkit-scrollbar { width: 3px; }
.custom-scrollbar::-webkit-scrollbar-thumb { background: #a855f7; }
</style>