<script setup>
import { ref, onMounted, onUnmounted } from 'vue'
import { request } from '@/api'
import AlarmOverlay from '@/components/Wigets/Reminder/AlarmOverlay.vue'

const props = defineProps({ dashboardId: Number })

const reminders = ref([])
const newMessage = ref('')
const timeInput = ref('') 
const permissionStatus = ref(Notification.permission)
const recurrence = ref('None') 
const loading = ref(false)
const isSubmitting = ref(false)

// State для активного будильника
const activeAlarm = ref(null) // Для оверлея
const ringingId = ref(null)   // Для подсветки в списке (если оверлей выключен)

// Настройка оверлея (храним в localStorage)
const useOverlay = ref(localStorage.getItem('reminders_overlay') !== 'false')

const toggleOverlay = () => {
    useOverlay.value = !useOverlay.value
    localStorage.setItem('reminders_overlay', useOverlay.value)
}

// --- TIME PARSER ---
const parseTimeInput = (input) => {
    const now = new Date()
    let target = new Date(now)
    let intervalMin = 0

    const s = input.toLowerCase().trim()

    // 1. Относительное время
    const matchRelative = s.match(/(?:(\d+)\s*h)?\s*(?:(\d+)\s*m)?\s*(?:(\d+)\s*s)?/)
    const hasRelative = s.includes('h') || s.includes('m') || s.includes('s')
    
    if (hasRelative && matchRelative) {
        const hours = parseInt(matchRelative[1] || '0')
        const mins = parseInt(matchRelative[2] || '0')
        const secs = parseInt(matchRelative[3] || '0')
        
        if (hours + mins + secs > 0) {
            target.setHours(target.getHours() + hours)
            target.setMinutes(target.getMinutes() + mins)
            target.setSeconds(target.getSeconds() + secs)
            
            // Интервал для БД (минимум 1 минута, если есть секунды - округляем вверх)
            // C# хранит int Minutes. 30 сек -> 1 мин.
            intervalMin = (hours * 60) + mins + (secs > 0 ? 1 : 0)
        } else { return null }
    } 
    // 2. Точное время
    else if (s.includes(':')) {
        const [h, m] = s.split(':').map(Number)
        if (!isNaN(h) && !isNaN(m)) {
            target.setHours(h, m, 0, 0)
            if (target <= now) target.setDate(target.getDate() + 1)
            intervalMin = 0
        } else { return null }
    } else { return null }

    return { target, intervalMin }
}

const formatDisplayTime = (date, recType) => {
    const timeStr = date.toLocaleTimeString([], {hour: '2-digit', minute:'2-digit'})
    if (recType === 'Daily') return `Every day ${timeStr}`
    if (recType === 'Interval') return `${timeStr} (Loop)`
    const now = new Date()
    if (date.toDateString() === now.toDateString()) return timeStr
    return `${date.toLocaleDateString([], {day:'numeric', month:'short'})} ${timeStr}`
}

// --- API ACTIONS ---

const fetchReminders = async () => {
    loading.value = true
    try {
        const data = await request(`/reminders/${props.dashboardId}`)
        reminders.value = data.map(r => {
            let dateStr = r.targetTime
            if (!dateStr.endsWith('Z') && !dateStr.includes('+')) dateStr += 'Z'
            return { ...r, localDate: new Date(dateStr) }
        })
    } catch(e) {} finally { loading.value = false }
}

const addReminder = async () => {
    if (!newMessage.value || !timeInput.value) return
    isSubmitting.value = true

    const parsed = parseTimeInput(timeInput.value)
    if (!parsed) {
        alert('Format: "18:30" or "30m"')
        isSubmitting.value = false
        return
    }

let finalRecurrence = recurrence.value
    if (finalRecurrence === 'Interval' && parsed.intervalMin === 0) {
        finalRecurrence = 'None' // <--- ИСПРАВЛЕНО (Сервер ждет None)
        alert('Loop requires relative time (e.g. "30m"). Switched to Once.')
    }

    try {
        await request(`/reminders/${props.dashboardId}`, {
            method: 'POST',
            body: JSON.stringify({
                message: newMessage.value,
                targetTime: parsed.target.toISOString(),
                // Убедитесь, что тут именно так:
                recurrenceType: finalRecurrence, 
                recurrenceIntervalMin: parsed.intervalMin
            })
        })
        newMessage.value = ''
        timeInput.value = ''
        recurrence.value = 'None'
        fetchReminders()
    } catch(e) { alert(e.message) } finally { isSubmitting.value = false }
}

const acknowledge = async (rem) => {
    // 1. Сбрасываем алармы
    activeAlarm.value = null
    ringingId.value = null
    
    // 2. Оптимистичное удаление ТОЛЬКО для одноразовых
    // Повторяющиеся оставляем, чтобы не мигали, пока сервер не пришлет новое время
    if (rem.recurrenceType === 'None') {
        reminders.value = reminders.value.filter(r => r.id !== rem.id)
    }

    try {
        // 3. Шлем запрос
        await request(`/reminders/${rem.id}/ack`, { method: 'POST' })
        
        // 4. Всегда обновляем список (для получения нового времени цикла)
        await fetchReminders()
    } catch(e) { 
        fetchReminders() 
    }
}

const deleteManual = async (id) => {
    reminders.value = reminders.value.filter(r => r.id !== id)
    try { await request(`/reminders/${id}`, { method: 'DELETE' }) } catch(e) {}
}

// --- ALARM LOGIC ---

const checkReminders = () => {
    const now = new Date()
    reminders.value.forEach(r => {
        // Если время пришло
        if (r.localDate <= now) {
            // Если мы уже звоним по этому ID, не спамим
            if (ringingId.value === r.id) return
            
            triggerAlarm(r)
        }
    })
}

const triggerAlarm = (rem) => {
    ringingId.value = rem.id

    // 1. ЗВУК
    const ctx = new (window.AudioContext || window.webkitAudioContext)()
    const osc = ctx.createOscillator(); const gain = ctx.createGain()
    osc.connect(gain); gain.connect(ctx.destination)
    osc.type = 'square'
    const t = ctx.currentTime
    // Тройной бип
    osc.frequency.setValueAtTime(880, t)
    gain.gain.setValueAtTime(0.1, t); gain.gain.setValueAtTime(0, t+0.1)
    gain.gain.setValueAtTime(0.1, t+0.2); gain.gain.setValueAtTime(0, t+0.3)
    gain.gain.setValueAtTime(0.1, t+0.4); gain.gain.setValueAtTime(0, t+0.5)
    osc.start(); osc.stop(t + 0.6)

    // 2. ЭКРАН (Опционально)
    if (useOverlay.value) {
        activeAlarm.value = rem
    }

    // 3. БРАУЗЕР
    if (Notification.permission === 'granted') {
        const n = new Notification("⏰ " + rem.message, {
            body: rem.recurrenceType !== 'None' ? "Recurring Task Triggered" : "Time is up!",
            requireInteraction: true,
            icon: '/favicon.svg'
        })
        // Клик по уведомлению тоже подтверждает
        n.onclick = () => { 
            window.focus()
            acknowledge(rem) 
        }
    }
}

const requestPermission = () => Notification.requestPermission().then(p => permissionStatus.value = p)

let interval
onMounted(() => {
    fetchReminders()
    interval = setInterval(checkReminders, 1000)
})
onUnmounted(() => clearInterval(interval))
</script>

<template>
  <div class="space-y-3 relative group/widget">
    
    <!-- FULLSCREEN OVERLAY (Optional) -->
    <Teleport to="body">
        <AlarmOverlay 
            :is-open="!!activeAlarm" 
            :message="activeAlarm?.message" 
            @dismiss="acknowledge(activeAlarm)" 
        />
    </Teleport>

    <!-- Header -->
    <div class="flex items-center justify-between text-[10px] font-mono font-bold text-pink-500 uppercase tracking-widest border-b border-pink-500/20 pb-1">
        <span class="flex items-center gap-2"><span>🔔</span> REMINDERS</span>
        
        <div class="flex items-center gap-2">
            <!-- Кнопка SCREEN -->
            <button 
                @click="toggleOverlay" 
                class="text-[8px] border px-1 rounded transition opacity-0 group-hover/widget:opacity-100"
                :class="useOverlay ? 'text-pink-400 border-pink-500/50 hover:bg-pink-900/20' : 'text-zinc-600 border-zinc-700 hover:text-zinc-400'"
                :title="useOverlay ? 'Disable Fullscreen Alarm' : 'Enable Fullscreen Alarm'"
            >
                SCREEN: {{ useOverlay ? 'ON' : 'OFF' }}
            </button>

            <!-- Кнопка Permission -->
            <button v-if="permissionStatus!=='granted'" @click="requestPermission" class="text-[8px] border border-pink-500/50 px-1 rounded hover:bg-pink-500 hover:text-black transition">ENABLE</button>
            <span v-else class="text-pink-500/50">{{ reminders.length }}</span>
        </div>
    </div>

    <!-- Input -->
    <div class="flex flex-col gap-2 bg-zinc-900/30 p-2 rounded border border-zinc-800">
        <input v-model="newMessage" class="bg-transparent border-b border-zinc-800 text-xs font-mono text-zinc-300 focus:border-pink-500 outline-none placeholder:text-zinc-700" placeholder="Task...">
        <div class="flex gap-2">
            <input v-model="timeInput" @keydown.enter="addReminder" class="flex-1 bg-transparent border-b border-zinc-800 text-xs font-mono text-pink-400 focus:border-pink-500 outline-none placeholder:text-zinc-600" placeholder="30m, 1h, 18:30">
            <select v-model="recurrence" class="bg-zinc-950 text-[10px] text-zinc-500 border border-zinc-800 rounded outline-none focus:border-pink-500 cursor-pointer w-16">
                <option value="None">Once</option>
                <option value="Daily">Daily</option>
                <option value="Interval">Loop</option>
            </select>
            <button @click="addReminder" :disabled="isSubmitting" class="text-[10px] text-zinc-500 hover:text-pink-500 font-bold border border-zinc-700 px-2 rounded hover:border-pink-500 transition">
                {{ isSubmitting ? '...' : 'SET' }}
            </button>
        </div>
    </div>

    <!-- List -->
    <div v-if="reminders.length === 0" class="text-zinc-700 text-[10px] font-mono italic text-center py-2">NO_ACTIVE_TASKS</div>
    <div v-else class="flex flex-col gap-1 max-h-40 overflow-y-auto custom-scrollbar">
        <div 
            v-for="rem in reminders" 
            :key="rem.id" 
            class="flex items-center justify-between p-2 rounded border transition group"
            :class="[
                ringingId === rem.id 
                    ? 'bg-red-900/20 border-red-500 animate-pulse' // Активный аларм
                    : 'bg-zinc-900/50 border-zinc-800 hover:border-pink-500/30' // Обычный
            ]"
        >
            <div class="flex flex-col overflow-hidden w-full cursor-pointer" @click="ringingId === rem.id ? acknowledge(rem) : null">
                <div class="flex justify-between items-center">
                    <span class="text-xs font-bold truncate" :class="ringingId === rem.id ? 'text-red-400' : 'text-zinc-300'">{{ rem.message }}</span>
                    <!-- Кнопка ACK если звонит -->
                    <button v-if="ringingId === rem.id" class="text-[9px] bg-red-500 text-black px-1 rounded font-bold hover:bg-red-400 ml-2">ACK</button>
                </div>
                
                <div class="flex items-center gap-1 text-[9px] font-mono">
                    <span :class="ringingId === rem.id ? 'text-red-300' : 'text-pink-400'">{{ formatDisplayTime(rem.localDate, rem.recurrenceType) }}</span>
                    <span v-if="rem.recurrenceType !== 'None'" class="text-zinc-600 bg-zinc-800 px-1 rounded text-[8px]">↻</span>
                </div>
            </div>
            
            <button @click="deleteManual(rem.id)" class="text-zinc-600 hover:text-red-500 text-xs opacity-0 group-hover:opacity-100 transition px-1 ml-2">×</button>
        </div>
    </div>
  </div>
</template>

<style scoped>
.custom-scrollbar::-webkit-scrollbar { width: 3px; }
.custom-scrollbar::-webkit-scrollbar-thumb { background: #ec4899; }
</style>