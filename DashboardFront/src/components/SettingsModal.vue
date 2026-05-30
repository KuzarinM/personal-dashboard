<script setup>
import { ref, watch, computed } from 'vue'
import { request } from '@/api'
import { widgetRegistry } from '@/config/widgets'

const props = defineProps({
  isOpen: Boolean,
  dashboardId: Number,
  config: Object
})

const emit = defineEmits(['close', 'refresh'])

// Default Widgets List
const defaultWidgets = [
    { id: 'weather', name: 'Weather Module', enabled: true },
    { id: 'telegram', name: 'Telegram Uplink', enabled: true },
    { id: 'events', name: 'Event Targets', enabled: true },
    { id: 'monitoring', name: 'Uptime Monitor', enabled: true },
    { id: 'userstatus', name: 'Team Status', enabled: true },
    { id: 'email', name: 'Mail Uplink', enabled: true },
    { id: 'fiat', name: 'Forex Rates', enabled: false },
    { id: 'crypto', name: 'Crypto Markets', enabled: false },
    { id: 'hackernews', name: 'Hacker News Feed', enabled: false },
    { id: 'calculator', name: 'System Calculator', enabled: false },
    { id: 'reminders', name: 'Reminder', enabled: false},
    { id: 'timetracking', name: 'Time Tracker', enabled: false},
    { id: 'podcast', name: 'FM Morning Podcast', enabled: false }
]

const form = ref({
  title: '',
  isPublic: false,
  scheduleEnabled: true,
  scheduleStart: '09:00',
  scheduleEnd: '18:00',
  
  // Urgency Breakdown
  critDays: 1,
  critHours: 0,
  critMinutes: 0, // <--- NEW
  
  warnDays: 7,
  warnHours: 0,
  warnMinutes: 0, // <--- NEW
  
  widgets: [],
  headerButtons: []
})

// --- SCHEDULE DAYS LOGIC ---
const weekDays = [
    { val: 1, label: 'Mon' },
    { val: 2, label: 'Tue' },
    { val: 3, label: 'Wed' },
    { val: 4, label: 'Thu' },
    { val: 5, label: 'Fri' },
    { val: 6, label: 'Sat' },
    { val: 0, label: 'Sun' }
]
const selectedDays = ref(new Set([1,2,3,4,5])) // Храним выбранные дни

// --- ACCESS STATE ---
const inviteUsername = ref('')
const inviteRole = ref('Viewer')
const isInviting = ref(false)
const inviteMsg = ref('')
const teamList = ref([])

const isSaving = ref(false)
const allWidgetIds = Object.keys(widgetRegistry)

watch(() => props.isOpen, (val) => {
  if (val && props.config) {
    // Reset Invite
    inviteUsername.value = ''
    inviteRole.value = 'Viewer'
    inviteMsg.value = ''
    loadTeam()

    // 1. General
    form.value.title = props.config.title
    form.value.isPublic = props.config.isPublic
    
    // 2. Schedule
    if (props.config.schedule) {
        form.value.scheduleEnabled = props.config.schedule.enabled
        form.value.scheduleStart = props.config.schedule.start
        form.value.scheduleEnd = props.config.schedule.end
        
        // Convert Array [1,2,3] to Set for checkboxes
        selectedDays.value = new Set(props.config.schedule.days || [])
    }

    // 3. Urgency (Minutes -> D/H/M)
    if (props.config.urgency) {
        const c = props.config.urgency.critical
        const w = props.config.urgency.warning
        
        form.value.critDays = Math.floor(c / 1440)
        form.value.critHours = Math.floor((c % 1440) / 60)
        form.value.critMinutes = c % 60
        
        form.value.warnDays = Math.floor(w / 1440)
        form.value.warnHours = Math.floor((w % 1440) / 60)
        form.value.warnMinutes = w % 60
    }

    // 4. Layouts
    let loadedWidgets = []
    try { if (props.config.widgetLayout) loadedWidgets = JSON.parse(props.config.widgetLayout) } catch (e) { loadedWidgets = [] }
    
    let headerConfig = []
    try { if (props.config.headerLayout) headerConfig = JSON.parse(props.config.headerLayout) } catch(e) {}

    // Header Buttons Merge
    form.value.headerButtons = allWidgetIds.map(id => {
        const saved = headerConfig.find(h => h.id === id)
        return {
            id: id,
            enabled: saved ? saved.enabled : false, 
            name: widgetRegistry[id].name
        }
    })

    // Sidebar Widgets Merge
    const merged = []
    loadedWidgets.forEach(saved => {
        const def = defaultWidgets.find(d => d.id === saved.id)
        if (def) merged.push({ ...def, enabled: saved.enabled })
    })
    defaultWidgets.forEach(def => {
        if (!merged.find(m => m.id === def.id)) {
            merged.push({ ...def, enabled: false })
        }
    })
    form.value.widgets = merged.length > 0 ? merged : JSON.parse(JSON.stringify(defaultWidgets))
  }
})

const moveWidget = (index, dir) => {
    if (dir === -1 && index === 0) return
    if (dir === 1 && index === form.value.widgets.length - 1) return
    const temp = form.value.widgets[index]
    form.value.widgets[index] = form.value.widgets[index + dir]
    form.value.widgets[index + dir] = temp
}

// --- TEAM LOGIC ---
const loadTeam = async () => {
    try { teamList.value = await request(`/dashboards/${props.dashboardId}/access`) } 
    catch (e) { teamList.value = [] }
}

const removeUser = async (userId) => {
    if(!confirm("Revoke access for this user?")) return
    try {
        await request(`/dashboards/${props.dashboardId}/access/${userId}`, { method: 'DELETE' })
        loadTeam()
    } catch(e) { alert(e.message) }
}

const grantAccess = async () => {
    if (!inviteUsername.value) return
    isInviting.value = true
    inviteMsg.value = ''
    try {
        await request(`/dashboards/${props.dashboardId}/access`, {
            method: 'POST',
            body: JSON.stringify({ username: inviteUsername.value, role: inviteRole.value })
        })
        inviteMsg.value = `SUCCESS`
        inviteUsername.value = ''
        await loadTeam() 
    } catch (e) {
        inviteMsg.value = `ERROR: ${e.message}`
    } finally {
        isInviting.value = false
    }
}

// --- SAVE ---
const saveSettings = async () => {
  isSaving.value = true
  try {
    // 1. Calculate Total Minutes
    const critTotalMin = (form.value.critDays * 1440) + (form.value.critHours * 60) + form.value.critMinutes
    const warnTotalMin = (form.value.warnDays * 1440) + (form.value.warnHours * 60) + form.value.warnMinutes
    
    // 2. Convert Set back to String "1,2,3"
    const scheduleString = Array.from(selectedDays.value).join(',')

    const payload = {
      title: form.value.title,
      isPublic: form.value.isPublic,
      scheduleEnabled: form.value.scheduleEnabled,
      scheduleStart: form.value.scheduleStart,
      scheduleEnd: form.value.scheduleEnd,
      scheduleDays: scheduleString, // Send as string
      urgencyCritical: critTotalMin,
      urgencyWarning: warnTotalMin,
      widgetLayout: JSON.stringify(form.value.widgets.map(w => ({ id: w.id, enabled: w.enabled }))),
      headerLayout: JSON.stringify(form.value.headerButtons.map(b => ({ id: b.id, enabled: b.enabled })))
    }
    
    await request(`/dashboards/${props.dashboardId}`, {
      method: 'PATCH',
      body: JSON.stringify(payload)
    })
    
    emit('refresh')
    emit('close')
  } catch (e) {
    alert(e.message)
  } finally {
    isSaving.value = false
  }
}

// Toggle Helper for Days
const toggleDay = (val) => {
    if (selectedDays.value.has(val)) selectedDays.value.delete(val)
    else selectedDays.value.add(val)
}
</script>

<template>
  <div v-if="isOpen" class="fixed inset-0 z-50 flex items-center justify-center p-4">
    <div class="absolute inset-0 bg-black/90 backdrop-blur-sm" @click="$emit('close')"></div>
    <div class="relative bg-zinc-950 border border-emerald-500/30 w-full max-w-lg flex flex-col rounded shadow font-sans overflow-hidden">
      
      <!-- HEADER -->
      <div class="p-3 border-b border-zinc-800 bg-zinc-900/50 flex justify-between items-center">
        <h2 class="text-emerald-500 font-mono font-bold tracking-widest text-sm">CORE_SETTINGS</h2>
        <button @click="$emit('close')" class="text-zinc-500 hover:text-red-400 text-xs font-mono">[ESC]</button>
      </div>

      <div class="p-6 space-y-8 overflow-y-auto max-h-[80vh] custom-scrollbar">
        
        <!-- 1. GENERAL -->
        <div class="space-y-3">
            <div class="text-[10px] text-zinc-500 font-mono uppercase border-b border-zinc-800 pb-1">Identification</div>
            <div>
                <label class="label">Dashboard Title</label>
                <input v-model="form.title" class="input">
            </div>
            <div class="flex items-center gap-2">
                <input type="checkbox" v-model="form.isPublic" id="pub" class="accent-emerald-500 cursor-pointer">
                <label for="pub" class="text-xs text-zinc-300 cursor-pointer">Public Access (ReadOnly for guests)</label>
            </div>
        </div>

        <!-- 2. TEAM ACCESS -->
        <div class="space-y-3 bg-zinc-900/30 p-2 border border-zinc-800 rounded">
            <div class="text-[10px] text-emerald-500 font-mono uppercase border-b border-zinc-800 pb-1 flex justify-between">
                <span>Team Access</span>
            </div>
            
            <div v-if="teamList.length > 0" class="space-y-1 mb-3">
                <div v-for="member in teamList" :key="member.userId" class="flex justify-between items-center bg-zinc-950 px-2 py-1 border border-zinc-800 rounded">
                    <span class="text-xs font-mono text-zinc-300">
                        {{ member.username }} 
                        <span class="text-[9px] text-zinc-500">({{ member.role }})</span>
                    </span>
                    <button @click="removeUser(member.userId)" class="text-zinc-600 hover:text-red-500 text-[10px]">REMOVE</button>
                </div>
            </div>
            <div v-else class="text-[9px] text-zinc-600 font-mono italic mb-2">No extra members allowed yet.</div>

            <div class="flex flex-col gap-2 border-t border-zinc-800 pt-2">
                <label class="label">Invite User</label>
                <div class="flex gap-2">
                    <input v-model="inviteUsername" class="input flex-1" placeholder="username...">
                    <select v-model="inviteRole" class="bg-zinc-950 border border-zinc-800 text-emerald-100 text-xs font-mono outline-none focus:border-emerald-500 px-2">
                        <option value="Viewer">Viewer</option>
                        <option value="Editor">Editor</option>
                    </select>
                    <button @click="grantAccess" :disabled="isInviting || !inviteUsername" class="bg-emerald-900/20 border border-emerald-500/30 text-emerald-400 px-3 text-[10px] font-mono hover:bg-emerald-500 hover:text-black transition">
                        {{ isInviting ? '...' : 'ADD' }}
                    </button>
                </div>
                <div v-if="inviteMsg" class="text-[10px] font-mono px-1" :class="inviteMsg.startsWith('ERROR') ? 'text-red-400' : 'text-emerald-400'">
                    {{ inviteMsg }}
                </div>
            </div>
        </div>

        <!-- 3. HEADER TOOLBAR -->
        <div class="space-y-3">
            <div class="text-[10px] text-zinc-500 font-mono uppercase border-b border-zinc-800 pb-1">Header Toolbar</div>
            <div class="grid grid-cols-2 gap-2">
                <div v-for="btn in form.headerButtons" :key="btn.id" class="flex items-center gap-2 bg-zinc-900/30 border border-zinc-800 p-2 rounded hover:border-zinc-600 transition">
                    <input type="checkbox" v-model="btn.enabled" class="accent-emerald-500 w-3 h-3 cursor-pointer">
                    <span class="text-xs font-mono text-zinc-400 select-none" :class="{'opacity-50': !btn.enabled}">{{ btn.name }}</span>
                </div>
            </div>
        </div>
        
        <!-- 4. SIDEBAR LAYOUT -->
        <div class="space-y-3">
            <div class="text-[10px] text-zinc-500 font-mono uppercase border-b border-zinc-800 pb-1">Sidebar Layout</div>
            <div class="flex flex-col gap-2">
                <div v-for="(w, idx) in form.widgets" :key="w.id" class="flex items-center gap-3 bg-zinc-900/50 border border-zinc-800 p-2 rounded hover:border-zinc-600 transition">
                    <input type="checkbox" v-model="w.enabled" class="accent-emerald-500 w-4 h-4 cursor-pointer">
                    <span class="text-xs font-mono text-zinc-300 flex-1 select-none" :class="{'opacity-50 line-through': !w.enabled}">{{ w.name }}</span>
                    <div class="flex gap-1">
                        <button @click="moveWidget(idx, -1)" class="text-zinc-500 hover:text-emerald-400 text-[10px] border border-zinc-700 px-2 rounded disabled:opacity-20" :disabled="idx===0">▲</button>
                        <button @click="moveWidget(idx, 1)" class="text-zinc-500 hover:text-emerald-400 text-[10px] border border-zinc-700 px-2 rounded disabled:opacity-20" :disabled="idx===form.widgets.length-1">▼</button>
                    </div>
                </div>
            </div>
        </div>

        <!-- 5. URGENCY (Updated with Minutes) -->
<!-- 5. URGENCY -->
        <div class="space-y-4">
            <div class="text-[10px] text-zinc-500 font-mono uppercase border-b border-zinc-800 pb-1">
                Event Alerts Thresholds
            </div>
            <div class="grid grid-cols-2 gap-4">
                
                <!-- Critical -->
                <div class="bg-red-900/10 border border-red-500/20 p-3 rounded-sm flex flex-col justify-between">
                    <label class="block text-[10px] font-bold text-red-500 mb-3 uppercase flex items-center gap-2">
                        <span class="w-2 h-2 bg-red-500 rounded-full animate-pulse"></span> Critical
                    </label>
                    <div class="grid grid-cols-3 gap-2">
                        <div>
                            <label class="label text-center">Days</label>
                            <input v-model.number="form.critDays" type="number" min="0" class="input text-center border-red-900/50 focus:border-red-500 text-red-100">
                        </div>
                        <div>
                            <label class="label text-center">Hours</label>
                            <input v-model.number="form.critHours" type="number" min="0" max="23" class="input text-center border-red-900/50 focus:border-red-500 text-red-100">
                        </div>
                        <div>
                            <label class="label text-center">Min</label>
                            <input v-model.number="form.critMinutes" type="number" min="0" max="59" class="input text-center border-red-900/50 focus:border-red-500 text-red-100">
                        </div>
                    </div>
                </div>

                <!-- Warning -->
                <div class="bg-amber-900/10 border border-amber-500/20 p-3 rounded-sm flex flex-col justify-between">
                    <label class="block text-[10px] font-bold text-amber-500 mb-3 uppercase flex items-center gap-2">
                        <span class="w-2 h-2 bg-amber-500 rounded-full"></span> Warning
                    </label>
                    <div class="grid grid-cols-3 gap-2">
                        <div>
                            <label class="label text-center">Days</label>
                            <input v-model.number="form.warnDays" type="number" min="0" class="input text-center border-amber-900/50 focus:border-amber-500 text-amber-100">
                        </div>
                        <div>
                            <label class="label text-center">Hours</label>
                            <input v-model.number="form.warnHours" type="number" min="0" max="23" class="input text-center border-amber-900/50 focus:border-amber-500 text-amber-100">
                        </div>
                        <div>
                            <label class="label text-center">Min</label>
                            <input v-model.number="form.warnMinutes" type="number" min="0" max="59" class="input text-center border-amber-900/50 focus:border-amber-500 text-amber-100">
                        </div>
                    </div>
                </div>

            </div>
        </div>

        <!-- 6. SCHEDULE (Updated with Checkboxes) -->
        <div class="space-y-3">
            <div class="flex justify-between items-center border-b border-zinc-800 pb-1">
                <span class="text-[10px] text-zinc-500 font-mono uppercase">Work Schedule</span>
                <input type="checkbox" v-model="form.scheduleEnabled" class="accent-emerald-500 cursor-pointer">
            </div>
            <div v-if="form.scheduleEnabled" class="space-y-4">
                <div class="grid grid-cols-2 gap-4">
                    <div>
                        <label class="label">Start Time</label>
                        <input v-model="form.scheduleStart" type="time" class="input">
                    </div>
                    <div>
                        <label class="label">End Time</label>
                        <input v-model="form.scheduleEnd" type="time" class="input">
                    </div>
                </div>
                
                <!-- Working Days Checkboxes -->
                <div>
                    <label class="label">Working Days</label>
                    <div class="flex flex-wrap gap-2">
                        <div v-for="day in weekDays" :key="day.val" 
                             @click="toggleDay(day.val)"
                             class="cursor-pointer px-3 py-1 border rounded transition text-xs font-mono select-none"
                             :class="selectedDays.has(day.val) ? 'bg-emerald-900/20 border-emerald-500/50 text-emerald-400' : 'bg-zinc-900 border-zinc-800 text-zinc-500 hover:border-zinc-600'"
                        >
                            {{ day.label }}
                        </div>
                    </div>
                </div>
            </div>
        </div>

      </div>

      <!-- FOOTER -->
      <div class="p-4 border-t border-zinc-800 bg-zinc-900/30 flex justify-end">
         <button @click="saveSettings" :disabled="isSaving" class="px-6 py-2 bg-emerald-900/20 border border-emerald-500/50 text-emerald-400 font-mono text-xs hover:bg-emerald-500 hover:text-black transition flex items-center gap-2">
            <span v-if="isSaving" class="animate-spin">/</span> SAVE CONFIG
         </button>
      </div>

    </div>
  </div>
</template>

<style scoped>
.label { @apply block text-[9px] text-zinc-500 font-mono uppercase mb-1; }
.input { @apply w-full bg-zinc-950 border border-zinc-800 p-2 text-xs text-emerald-100 outline-none focus:border-emerald-500 transition font-mono; }
input[type=number]::-webkit-inner-spin-button, 
input[type=number]::-webkit-outer-spin-button { 
    -webkit-appearance: none; 
    margin: 0; 
}
input[type=number] {
    -moz-appearance: textfield; /* Для Firefox */
}
.custom-scrollbar::-webkit-scrollbar { width: 4px; }
.custom-scrollbar::-webkit-scrollbar-track { background: transparent; }
.custom-scrollbar::-webkit-scrollbar-thumb { background: #3f3f46; border-radius: 2px; }
.custom-scrollbar::-webkit-scrollbar-thumb:hover { background: #10b981; }
</style>