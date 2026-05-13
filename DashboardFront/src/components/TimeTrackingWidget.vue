<script setup>
import { ref, onMounted, computed } from 'vue'
import { request } from '@/api'

const props = defineProps({ dashboardId: Number })

const selectedDate = ref(new Date())
const summary = ref({ totalLogged: '00:00', tasks:[] })
const loading = ref(true)

// Refs
const logModel = ref({ taskName: '', timeInput: '' })
const timeInputRef = ref(null)
const dateInputRef = ref(null) // Ссылка на скрытый инпут даты

// --- ВЫБОР ДАТЫ ---
const displayDate = computed(() => {
    const today = new Date()
    if (selectedDate.value.toDateString() === today.toDateString()) return 'TODAY'
    return selectedDate.value.toLocaleDateString('en-US', { day: 'numeric', month: 'short' }).toUpperCase()
})

const datePickerValue = computed({
    get: () => {
        const d = new Date(selectedDate.value)
        const offset = d.getTimezoneOffset() * 60000
        return new Date(d - offset).toISOString().split('T')[0]
    },
    set: (val) => {
        if (!val) return
        selectedDate.value = new Date(val)
        fetchSummary()
    }
})

const changeDate = (days) => {
    const d = new Date(selectedDate.value)
    d.setDate(d.getDate() + days)
    selectedDate.value = d
    fetchSummary()
}

// Открываем нативный календарь надежным способом
const openDatePicker = () => {
    if (dateInputRef.value) {
        try {
            dateInputRef.value.showPicker()
        } catch (e) {
            // Фолбек для очень старых браузеров
            dateInputRef.value.focus() 
        }
    }
}

const getApiDate = () => {
    const offset = selectedDate.value.getTimezoneOffset() * 60000
    return new Date(selectedDate.value - offset).toISOString()
}

// --- API ACTIONS ---
const fetchSummary = async () => {
    loading.value = true
    try {
        const dateStr = encodeURIComponent(getApiDate())
        const data = await request(`/time/daily-summary?date=${dateStr}`)
        
        summary.value = {
            totalLogged: data.totalFormatted || '00:00',
            tasks: data.tasks ||[]
        }
    } catch (e) {
        console.error("TimeTracker Error:", e)
    } finally {
        loading.value = false
    }
}

const logTime = async () => {
    if (!logModel.value.taskName || !logModel.value.timeInput) return
    loading.value = true
    try {
        await request('/time/log', {
            method: 'POST',
            body: JSON.stringify({
                taskName: logModel.value.taskName,
                timeInput: logModel.value.timeInput,
                tags:[],
                date: getApiDate()
            })
        })
        logModel.value.taskName = ''
        logModel.value.timeInput = ''
        await fetchSummary()
    } catch (e) { 
        alert(e.message) 
        loading.value = false
    }
}

const deleteEntry = async (id) => {
    if (!confirm('Delete this time entry?')) return
    loading.value = true
    try {
        await request(`/time/entries/${id}`, { method: 'DELETE' })
        await fetchSummary()
    } catch(e) {
        loading.value = false
    }
}

// --- УПРАВЛЕНИЕ ТЕГАМИ ---
const addTag = async (taskId) => {
    const tag = prompt('Enter new tag name (e.g. dev, bugfix):')
    if (!tag) return
    loading.value = true
    try {
        await request(`/time/tasks/${taskId}/tags`, { 
            method: 'POST', 
            body: JSON.stringify({ tagName: tag }) 
        })
        await fetchSummary()
    } catch(e) {
        loading.value = false
    }
}

const removeTag = async (taskId, tagId) => {
    loading.value = true
    try {
        await request(`/time/tasks/${taskId}/tags/${tagId}`, { method: 'DELETE' })
        await fetchSummary()
    } catch(e) {
        loading.value = false
    }
}

// --- UI HELPERS ---
const appendToTask = (taskName) => {
    logModel.value.taskName = taskName
    if (timeInputRef.value) timeInputRef.value.focus()
}

onMounted(() => {
    fetchSummary()
})
</script>

<template>
  <div class="space-y-3 relative group/widget flex flex-col">
    
    <!-- HEADER -->
    <div class="flex items-center justify-between text-[10px] font-mono font-bold text-teal-500 uppercase tracking-widest border-b border-teal-500/20 pb-1 relative z-20">
       <span class="flex items-center gap-2"><span>⏱️</span> TIME_TRACKER</span>
       
       <!-- Date Navigator & Picker -->
       <div class="flex items-center gap-2 bg-teal-900/10 border border-teal-500/30 rounded px-1 relative">
           <button @click="changeDate(-1)" class="hover:text-white px-1 transition"><</button>
           
           <div class="relative flex items-center justify-center min-w-[50px] group/date">
               <!-- Текст, по клику на который открываем инпут -->
               <span @click="openDatePicker" class="text-center text-[9px] cursor-pointer group-hover/date:text-white transition">
                   {{ displayDate }}
               </span>
               
               <!-- Визуально спрятанный инпут, но присутствующий в DOM -->
               <input 
                   type="date" 
                   ref="dateInputRef"
                   v-model="datePickerValue"
                   class="absolute w-0 h-0 opacity-0 pointer-events-none"
                   tabindex="-1"
               >
           </div>
           
           <button @click="changeDate(1)" class="hover:text-white px-1 transition">></button>
       </div>
    </div>

    <!-- MAIN CONTENT AREA (Wrapper for Loading Overlay) -->
    <div class="relative flex-1 flex flex-col gap-3 min-h-[150px]">
        
        <!-- LOADING OVERLAY -->
        <div 
            v-if="loading" 
            class="absolute inset-0 z-50 flex flex-col items-center justify-center bg-zinc-950/70 backdrop-blur-sm rounded border border-teal-500/20 animate-in fade-in duration-200"
        >
            <div class="w-6 h-6 border-2 border-teal-500/20 border-t-teal-500 rounded-full animate-spin mb-3"></div>
            <span class="text-teal-500 font-mono text-[9px] tracking-widest animate-pulse uppercase">Syncing_Data...</span>
        </div>

        <!-- CONTENT (Fades when loading) -->
        <div :class="{'opacity-30 pointer-events-none blur-[1px]': loading}" class="transition-all duration-300 flex flex-col gap-3 flex-1">
            
            <!-- TOTAL LOGGED -->
            <div class="flex justify-between items-center text-xs font-mono bg-zinc-900/30 p-2 rounded border border-zinc-800">
                <span class="text-zinc-500 uppercase tracking-widest">Total Logged</span>
                <span class="text-teal-400 font-bold text-sm tracking-wider">{{ summary.totalLogged }}</span>
            </div>

            <!-- QUICK ADD FORM -->
            <div class="flex flex-col gap-2 bg-zinc-900/50 p-2 rounded border border-zinc-800 focus-within:border-teal-500/50 transition">
                <input 
                    v-model="logModel.taskName" 
                    placeholder="Task name..." 
                    class="w-full bg-transparent border-b border-zinc-800 text-xs font-mono text-zinc-300 focus:border-teal-500 outline-none pb-1 placeholder:text-zinc-700"
                >
                <div class="flex gap-2">
                    <input 
                        ref="timeInputRef"
                        v-model="logModel.timeInput" 
                        @keydown.enter="logTime" 
                        placeholder="1h 30m" 
                        class="flex-1 bg-transparent border-b border-zinc-800 text-xs font-mono text-teal-400 focus:border-teal-500 outline-none placeholder:text-zinc-700"
                    >
                    <button 
                        @click="logTime" 
                        :disabled="loading"
                        class="text-[10px] bg-teal-900/20 text-teal-500 font-bold border border-teal-500/30 px-3 rounded hover:bg-teal-500 hover:text-black transition disabled:opacity-50"
                    >
                        LOG
                    </button>
                </div>
            </div>

            <!-- EMPTY STATE -->
            <div v-if="summary.tasks.length === 0" class="text-zinc-700 text-[10px] font-mono italic text-center py-4 border border-dashed border-zinc-800 rounded">
                NO_RECORDS_FOUND
            </div>

            <!-- TASKS LIST -->
            <div v-else class="flex flex-col gap-2 max-h-56 overflow-y-auto custom-scrollbar pr-1">
                <div 
                    v-for="task in summary.tasks" 
                    :key="task.taskId" 
                    class="bg-zinc-900/40 border border-zinc-800 rounded p-2 flex flex-col gap-1.5 group/task hover:border-teal-500/30 transition"
                >
                    <!-- Task Header -->
                    <div class="flex justify-between items-start">
                        <span 
                            class="text-xs font-bold text-zinc-300 cursor-pointer hover:text-teal-400 transition truncate mr-2" 
                            @click="appendToTask(task.taskName)"
                            :title="task.taskName"
                        >
                            {{ task.taskName }}
                        </span>
                        <div class="flex items-center gap-2 flex-shrink-0">
                            <span class="text-[10px] font-mono font-bold text-teal-500 bg-teal-900/20 px-1 rounded">{{ task.formattedTime }}</span>
                            <button @click="appendToTask(task.taskName)" class="text-zinc-600 hover:text-teal-400 text-xs" title="Add time to task">+</button>
                        </div>
                    </div>

                    <!-- Tags -->
                    <div class="flex flex-wrap gap-1">
                        <div 
                            v-for="tag in task.tags" 
                            :key="tag.id" 
                            class="text-[8px] font-mono bg-zinc-800 border border-zinc-700 text-zinc-400 px-1 rounded flex items-center gap-1 group/tag"
                        >
                            <span class="text-teal-500/50">#</span>{{ tag.name || tag.tagName }}
                            <button @click="removeTag(task.taskId, tag.id)" class="text-zinc-600 hover:text-red-400 ml-1 opacity-0 group-hover/tag:opacity-100 transition">×</button>
                        </div>
                        <button @click="addTag(task.taskId)" class="text-[8px] font-mono text-zinc-600 hover:text-teal-400 border border-dashed border-zinc-700 px-1.5 rounded opacity-0 group-hover/task:opacity-100 transition">
                            + tag
                        </button>
                    </div>

                    <!-- Entries List -->
                    <div class="flex flex-col mt-1 pl-2 border-l border-zinc-800/50 space-y-0.5">
                        <div 
                            v-for="entry in task.entries" 
                            :key="entry.id" 
                            class="flex justify-between items-center group/entry hover:bg-zinc-800/30 px-1 rounded transition"
                        >
                            <div class="flex items-center gap-2">
                                <span class="text-[8px] text-zinc-600">>></span>
                                <span class="text-[10px] font-mono text-zinc-400">{{ entry.formattedTime }}</span>
                            </div>
                            <button @click="deleteEntry(entry.id)" class="text-zinc-600 hover:text-red-500 text-[10px] opacity-0 group-hover/entry:opacity-100 transition">×</button>
                        </div>
                    </div>
                </div>
            </div>

        </div>
    </div>
  </div>
</template>

<style scoped>
.custom-scrollbar::-webkit-scrollbar { width: 3px; }
.custom-scrollbar::-webkit-scrollbar-thumb { background: #14b8a6; } /* teal-500 */
</style>