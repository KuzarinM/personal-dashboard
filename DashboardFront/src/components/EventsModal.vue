<script setup>
import { ref, watch } from 'vue'
import { request } from '@/api'
import EmojiPicker from '@/components/ui/EmojiPicker.vue' 

const props = defineProps({
  isOpen: Boolean,
  dashboardId: Number,
  initialEvents: { type: Array, default: () => [] },
  initialCalendars: { type: Array, default: () => [] }
})

const emit = defineEmits(['close', 'refresh'])

const activeTab = ref('manual') // 'manual' | 'sources'
const localEvents = ref([])
const localCalendars = ref([])
const isSaving = ref(false)

// --- HELPERS ---

// Приводим дату к формату для input type="datetime-local" (YYYY-MM-DDTHH:mm)
const formatDateForInput = (isoString) => {
  if (!isoString) return ''
  const date = new Date(isoString)
  date.setMinutes(date.getMinutes() - date.getTimezoneOffset()) // Коррекция для локального инпута
  return date.toISOString().slice(0, 16)
}

// Инициализация данных при открытии
watch(() => props.isOpen, (val) => {
  if (val) {
    // Глубокое копирование событий
    localEvents.value = props.initialEvents.map(e => ({
      name: e.name,
      icon: e.icon || '📌',
      // Если дата уже Date объект, конвертируем, если строка - оставляем
      date: formatDateForInput(e.date) 
    }))

    // Глубокое копирование календарей
    localCalendars.value = props.initialCalendars.map(c => ({
      name: c.name,
      url: c.url,
      icon: c.icon || '📅'
    }))
  }
})

// --- ACTIONS: MANUAL EVENTS ---

const addEvent = () => {
  localEvents.value.push({ name: '', date: '', icon: '📌' })
}

const removeEvent = (index) => {
  localEvents.value.splice(index, 1)
}

const saveEvents = async () => {
  isSaving.value = true
  try {
    const payload = {
      events: localEvents.value.map(e => ({
        name: e.name,
        icon: e.icon,
        // Конвертируем обратно в ISO
        date: new Date(e.date).toISOString()
      }))
    }

    await request(`/dashboards/${props.dashboardId}/resources/manual-events`, {
      method: 'PUT',
      body: JSON.stringify(payload)
    })
    emit('refresh') // Обновляем дашборд
    // Не закрываем модалку, чтобы юзер видел результат, или можно emit('close')
  } catch (e) {
    alert('Error saving events: ' + e.message)
  } finally {
    isSaving.value = false
  }
}

// --- ACTIONS: CALENDARS ---

const addCalendar = () => {
  localCalendars.value.push({ name: '', url: '', icon: '📅' })
}

const removeCalendar = (index) => {
  localCalendars.value.splice(index, 1)
}

const saveCalendars = async () => {
  isSaving.value = true
  try {
    const payload = {
      calendars: localCalendars.value.map(c => ({
        name: c.name,
        url: c.url,
        icon: c.icon
      }))
    }

    await request(`/dashboards/${props.dashboardId}/resources/calendars`, {
      method: 'PUT',
      body: JSON.stringify(payload)
    })
    emit('refresh')
  } catch (e) {
    alert('Error saving calendars: ' + e.message)
  } finally {
    isSaving.value = false
  }
}
</script>

<template>
  <div v-if="isOpen" class="fixed inset-0 z-50 flex items-center justify-center p-4">
    <!-- Backdrop -->
    <div class="absolute inset-0 bg-black/90 backdrop-blur-sm" @click="$emit('close')"></div>

    <!-- Modal Window -->
    <div class="relative bg-zinc-950 border border-emerald-500/30 w-full max-w-2xl h-[80vh] flex flex-col rounded shadow-[0_0_30px_rgba(16,185,129,0.1)] overflow-hidden">
      
      <!-- Header -->
      <div class="flex items-center justify-between p-3 border-b border-zinc-800 bg-zinc-900/50">
        <h2 class="text-emerald-500 font-mono font-bold tracking-widest flex items-center gap-2 text-sm">
          <span class="animate-pulse">●</span> TIME_CONTROLLER
        </h2>
        <button @click="$emit('close')" class="text-zinc-500 hover:text-red-400 transition font-mono text-xs">[ESC]</button>
      </div>

      <!-- Tabs -->
      <div class="flex border-b border-zinc-800 font-mono text-xs">
        <button 
          @click="activeTab = 'manual'"
          class="flex-1 py-3 text-center transition hover:bg-zinc-900"
          :class="activeTab === 'manual' ? 'text-emerald-400 bg-zinc-900 border-b-2 border-emerald-500' : 'text-zinc-500'"
        >
          MANUAL TARGETS
        </button>
        <button 
          @click="activeTab = 'sources'"
          class="flex-1 py-3 text-center transition hover:bg-zinc-900"
          :class="activeTab === 'sources' ? 'text-emerald-400 bg-zinc-900 border-b-2 border-emerald-500' : 'text-zinc-500'"
        >
          EXTERNAL SOURCES (iCal)
        </button>
      </div>

      <!-- Content Area -->
      <div class="flex-1 overflow-y-auto p-6 custom-scrollbar bg-zinc-900/20">
        
        <!-- TAB 1: MANUAL EVENTS -->
        <div v-if="activeTab === 'manual'" class="space-y-4">
          <div v-if="localEvents.length === 0" class="text-center text-zinc-600 font-mono text-xs py-8 border border-dashed border-zinc-800">
            NO ACTIVE TARGETS ASSIGNED
          </div>

          <div v-for="(event, idx) in localEvents" :key="idx" class="grid grid-cols-12 gap-2 items-center bg-zinc-900/50 p-2 border border-zinc-800 rounded group">
            <!-- Icon -->
            <div class="col-span-1">
               <EmojiPicker v-model="event.icon" placeholder="📌" />
            </div>
            <!-- Name -->
            <div class="col-span-6">
               <label class="block text-[8px] text-zinc-600 font-mono uppercase">Designation</label>
               <input v-model="event.name" class="w-full bg-transparent text-zinc-200 font-mono text-xs border-b border-zinc-800 focus:border-emerald-500 focus:outline-none py-1" placeholder="Event Name">
            </div>
            <!-- Date -->
            <div class="col-span-4">
               <label class="block text-[8px] text-zinc-600 font-mono uppercase">Temporal Locus</label>
               <input v-model="event.date" type="datetime-local" class="w-full bg-transparent text-emerald-400 font-mono text-[10px] focus:outline-none py-1 color-scheme-dark">
            </div>
            <!-- Delete -->
            <div class="col-span-1 text-right">
              <button @click="removeEvent(idx)" class="text-zinc-600 hover:text-red-500 transition">×</button>
            </div>
          </div>

          <button @click="addEvent" class="w-full py-2 border border-zinc-800 border-dashed text-zinc-500 font-mono text-xs hover:text-emerald-400 hover:border-emerald-500/50 transition mt-4">
            [+] ADD TARGET
          </button>
        </div>

        <!-- TAB 2: CALENDARS -->
        <div v-if="activeTab === 'sources'" class="space-y-4">
          <p class="text-[10px] text-zinc-500 font-mono mb-4 border-l-2 border-emerald-500/20 pl-2">
            Supported formats: .ics, Google Calendar (Secret Address), iCal.
          </p>

          <div v-for="(cal, idx) in localCalendars" :key="idx" class="bg-zinc-900/50 p-3 border border-zinc-800 rounded group space-y-2">
            <div class="flex gap-2">
                <EmojiPicker v-model="cal.icon" placeholder="📅" />
                <input v-model="cal.name" class="flex-1 bg-zinc-950 border border-zinc-800 px-2 rounded text-xs font-mono text-zinc-300 focus:border-emerald-500 outline-none" placeholder="Source Name (e.g. Work)">
                <button @click="removeCalendar(idx)" class="text-zinc-600 hover:text-red-500 px-2">DELETE</button>
            </div>
            <div>
                <input v-model="cal.url" class="w-full bg-zinc-950 border border-zinc-800 px-2 py-1 rounded text-[10px] font-mono text-emerald-500/80 focus:border-emerald-500 outline-none" placeholder="https://calendar.google.com/calendar/ical/...">
            </div>
          </div>

          <button @click="addCalendar" class="w-full py-2 border border-zinc-800 border-dashed text-zinc-500 font-mono text-xs hover:text-emerald-400 hover:border-emerald-500/50 transition mt-4">
            [+] ADD SOURCE
          </button>
        </div>

      </div>

      <!-- Footer Actions -->
      <div class="p-4 border-t border-zinc-800 bg-zinc-950 flex justify-end gap-3">
        <button @click="$emit('close')" class="px-4 py-2 text-zinc-500 hover:text-zinc-300 font-mono text-xs transition">CANCEL</button>
        
        <button 
          v-if="activeTab === 'manual'"
          @click="saveEvents"
          :disabled="isSaving"
          class="px-6 py-2 bg-emerald-900/20 border border-emerald-500/50 text-emerald-400 font-mono text-xs hover:bg-emerald-500 hover:text-black transition rounded-sm flex items-center gap-2"
        >
          <span v-if="isSaving" class="animate-spin">/</span> SAVE TARGETS
        </button>

        <button 
          v-if="activeTab === 'sources'"
          @click="saveCalendars"
          :disabled="isSaving"
          class="px-6 py-2 bg-emerald-900/20 border border-emerald-500/50 text-emerald-400 font-mono text-xs hover:bg-emerald-500 hover:text-black transition rounded-sm flex items-center gap-2"
        >
          <span v-if="isSaving" class="animate-spin">/</span> SAVE SOURCES
        </button>
      </div>

    </div>
  </div>
</template>

<style scoped>
/* Хак для иконки календаря в инпуте, чтобы она была белой/серой */
input[type="datetime-local"]::-webkit-calendar-picker-indicator {
    filter: invert(1);
    opacity: 0.5;
    cursor: pointer;
}
</style>