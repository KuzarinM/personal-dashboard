<script setup>
import { ref, watch } from 'vue'
import { request } from '@/api'

const props = defineProps({
  isOpen: Boolean,
  dashboardId: Number
})

const emit = defineEmits(['close', 'refresh'])
const monitors = ref([])
const isLoading = ref(false)

// Form State
const newItem = ref({ name: '', type: 'Ping', target: '', intervalMin: 10 })
const isCreating = ref(false)

watch(() => props.isOpen, (val) => {
  if (val) loadMonitors()
})

const loadMonitors = async () => {
    isLoading.value = true
    try {
        monitors.value = await request(`/monitoring/${props.dashboardId}`)
    } catch(e) {} 
    finally { isLoading.value = false }
}

const addMonitor = async () => {
    if (!newItem.value.name || !newItem.value.target) return
    isCreating.value = true
    try {
        const added = await request(`/monitoring/${props.dashboardId}`, {
            method: 'POST',
            body: JSON.stringify(newItem.value)
        })
        // Оптимистичное добавление в список
        monitors.value.push(added) 
        newItem.value = { name: '', type: 'Ping', target: '', intervalMin: 10 }
        emit('refresh')
    } catch (e) { alert(e.message) }
    finally { isCreating.value = false }
}

const toggleActive = async (monitor) => {
    monitor.isActive = !monitor.isActive
    await updateMonitor(monitor)
}

const deleteMonitor = async (id) => {
    if(!confirm("Remove this monitor?")) return
    
    // Оптимистичное удаление из UI сразу
    monitors.value = monitors.value.filter(m => m.id !== id)
    
    try {
        await request(`/monitoring/${id}`, { method: 'DELETE' })
        emit('refresh')
    } catch(e) {
        alert('Delete failed')
        loadMonitors() // Откат если ошибка
    }
}

const updateMonitor = async (monitor) => {
    try {
        await request(`/monitoring/${monitor.id}`, { method: 'PATCH', body: JSON.stringify(monitor) })
        emit('refresh')
    } catch(e) { alert(e.message) }
}
</script>

<template>
  <div v-if="isOpen" class="fixed inset-0 z-50 flex items-center justify-center p-4">
    <div class="absolute inset-0 bg-black/90 backdrop-blur-sm" @click="$emit('close')"></div>
    <div class="relative bg-zinc-950 border border-purple-500/30 w-full max-w-2xl flex flex-col rounded shadow font-sans overflow-hidden max-h-[85vh]">
      
      <div class="p-3 border-b border-zinc-800 bg-zinc-900/50 flex justify-between items-center">
        <h2 class="text-purple-500 font-mono font-bold tracking-widest text-sm">UPTIME_SENSORS</h2>
        <button @click="$emit('close')" class="text-zinc-500 hover:text-red-400 text-xs font-mono">[ESC]</button>
      </div>

      <div class="p-6 space-y-6 overflow-y-auto custom-scrollbar flex-1">
        
        <!-- ADD NEW -->
        <div class="bg-zinc-900/30 p-3 rounded border border-zinc-800">
            <div class="text-[10px] text-zinc-500 font-mono uppercase mb-2">Deploy New Sensor</div>
            <div class="grid grid-cols-12 gap-2">
                <div class="col-span-3">
                    <input v-model="newItem.name" class="input-cyber" placeholder="Name (e.g. Gateway)">
                </div>
                <div class="col-span-2">
                    <select v-model="newItem.type" class="input-cyber cursor-pointer">
                        <option value="Ping">PING</option>
                        <option value="Http">HTTPS</option>
                    </select>
                </div>
                <div class="col-span-4">
                    <input v-model="newItem.target" class="input-cyber" placeholder="IP (127.0.0.1) or Domain">
                </div>
                <div class="col-span-2">
                    <input v-model.number="newItem.intervalMin" type="number" min="10" class="input-cyber" placeholder="Min (10)">
                </div>
                <div class="col-span-1">
                    <button @click="addMonitor" :disabled="isCreating" class="w-full h-full bg-purple-900/20 border border-purple-500/30 text-purple-400 hover:bg-purple-500 hover:text-black text-xs font-bold transition">
                        +
                    </button>
                </div>
            </div>
        </div>

        <!-- LIST -->
        <div class="space-y-2">
            <div v-if="monitors.length === 0" class="text-center text-zinc-600 font-mono text-xs py-4">NO ACTIVE SENSORS</div>
            <div v-for="m in monitors" :key="m.id" class="flex items-center gap-3 p-2 border border-zinc-800 rounded bg-zinc-900/20 hover:border-zinc-700 transition group">
                
                <!-- Status Toggle -->
                <button @click="toggleActive(m)" class="w-8 h-8 flex items-center justify-center border rounded transition text-[9px] font-bold"
                    :class="m.isActive ? 'bg-purple-500/20 border-purple-500/50 text-purple-400' : 'bg-zinc-950 border-zinc-700 text-zinc-600'">
                    {{ m.isActive ? 'ON' : 'OFF' }}
                </button>

                <!-- Info -->
                <div class="flex-1 grid grid-cols-12 gap-2 items-center">
                    <input v-model="m.name" @change="updateMonitor(m)" class="col-span-3 bg-transparent text-xs font-bold text-zinc-300 outline-none focus:text-purple-400" :disabled="!m.isActive">
                    <span class="col-span-1 text-[9px] font-mono text-zinc-500 bg-zinc-900 px-1 rounded text-center">{{ m.type }}</span>
                    <input v-model="m.target" @change="updateMonitor(m)" class="col-span-5 bg-transparent text-xs font-mono text-zinc-400 outline-none focus:text-purple-400" :disabled="!m.isActive">
                    <div class="col-span-3 flex items-center gap-1">
                        <input v-model.number="m.intervalMin" @change="updateMonitor(m)" type="number" min="10" class="w-10 bg-transparent text-right text-xs font-mono text-zinc-500 focus:text-purple-400 outline-none">
                        <span class="text-[9px] text-zinc-600">min</span>
                    </div>
                </div>

                <!-- Delete -->
                <button @click="deleteMonitor(m.id)" class="text-zinc-600 hover:text-red-500 px-2 opacity-0 group-hover:opacity-100 transition">×</button>
            </div>
        </div>

      </div>
    </div>
  </div>
</template>

<style scoped>
.input-cyber { @apply w-full bg-zinc-950 border border-zinc-800 p-2 text-xs font-mono text-zinc-300 outline-none focus:border-purple-500 transition; }
.custom-scrollbar::-webkit-scrollbar { width: 4px; }
.custom-scrollbar::-webkit-scrollbar-thumb { background: #3f3f46; border-radius: 2px; }
</style>