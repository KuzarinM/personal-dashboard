<script setup>
import { ref, onMounted, onUnmounted } from 'vue'

const props = defineProps({
  dashboardId: String
})

const emit = defineEmits(['error-change'])

const chats = ref([])
const status = ref('loading') 
const knownMessageIds = ref(new Set())
const isConnectionError = ref(false)
const isFetching = ref(false)

let isFirstLoad = true
let interval = null // Переменная для таймера

// --- ОСНОВНАЯ ФУНКЦИЯ ЗАГРУЗКИ ---
const fetchTelegram = async () => {
  if (isFetching.value) return
  isFetching.value = true

  try {
    const res = await fetch(`/api/telegram/${props.dashboardId}?t=${Date.now()}`)
    
    if (!res.ok) throw new Error("Server Error")
    
    const data = await res.json()
    
    if (data.notConfigured) { 
        status.value = 'no_config'
        isConnectionError.value = false
        emit('error-change', false)
        return 
    }
    
    if (data.error) throw new Error(data.error)

    isConnectionError.value = false
    emit('error-change', false)

    const now = Date.now()
    const RECENT_THRESHOLD = 60 * 60 * 1000 

    const processedChats = data.map(chat => {
        const uniqueKey = `${chat.id}_${chat.msgId}`
        
        let msgDate = chat.date
        if (msgDate < 100000000000) msgDate = msgDate * 1000
        
        const isRecent = (now - msgDate) < RECENT_THRESHOLD
        
        let isSessionNew = false
        if (!isFirstLoad) {
            if (!knownMessageIds.value.has(uniqueKey)) {
                isSessionNew = true
            }
        } else {
            knownMessageIds.value.add(uniqueKey)
        }
        
        return { ...chat, date: msgDate, isRecent, isSessionNew }
    })
    
    const currentKeys = new Set(processedChats.map(c => `${c.id}_${c.msgId}`))
    knownMessageIds.value = currentKeys

    chats.value = processedChats
    status.value = chats.value.length > 0 ? 'active' : 'empty'
    isFirstLoad = false

  } catch (e) {
    console.error("TG Widget Error:", e)
    isConnectionError.value = true
    emit('error-change', true)
  } finally {
    isFetching.value = false
  }
}

const formatDate = (ts) => {
    if (!ts) return ''
    const date = new Date(ts)
    const now = new Date()
    if (date.getDate() === now.getDate() && date.getMonth() === now.getMonth()) {
        return date.toLocaleTimeString([], {hour: '2-digit', minute:'2-digit'})
    }
    return date.toLocaleDateString([], {day: 'numeric', month: 'short'})
}

// --- УПРАВЛЕНИЕ ТАЙМЕРОМ (VISIBILITY API) ---

const startPolling = () => {
    // Если таймер уже есть - не дублируем
    if (interval) return
    
    console.log('Tab active: Telegram polling started')
    fetchTelegram() // Сразу обновляем данные при возвращении
    interval = setInterval(fetchTelegram, 10000)
}

const stopPolling = () => {
    if (interval) {
        console.log('Tab hidden: Telegram polling paused')
        clearInterval(interval)
        interval = null
    }
}

const handleVisibilityChange = () => {
    if (document.hidden) {
        stopPolling()
    } else {
        startPolling()
    }
}

onMounted(() => {
  // Запускаем сразу, если страница видна
  if (!document.hidden) {
      startPolling()
  }
  // Слушаем переключение вкладок
  document.addEventListener('visibilitychange', handleVisibilityChange)
})

onUnmounted(() => {
  stopPolling()
  document.removeEventListener('visibilitychange', handleVisibilityChange)
})
</script>

<template>
  <div class="space-y-3 transition-all duration-500 relative">
    
    <!-- Заголовок -->
    <div class="flex items-center justify-between text-[10px] font-mono font-bold uppercase tracking-widest border-b pb-1"
         :class="isConnectionError ? 'text-red-500 border-red-500/20' : 'text-sky-500 border-sky-500/20'">
       <span class="flex items-center gap-2">
         <svg class="w-3 h-3 fill-current" viewBox="0 0 24 24"><path d="M11.944 0A12 12 0 0 0 0 12a12 12 0 0 0 12 12 12 12 0 0 0 12-12A12 12 0 0 0 12 0a12 12 0 0 0-.74 0zm5.784 8.014c.125 3.33-1.63 9.47-2.3 10.96-.282.63-.64.67-1.12.38l-4.2-3.13-2.07 1.94a.8.8 0 0 1-.65.31l.34-4.2 7.74-6.83c.36-.31-.08-.48-.52-.2l-9.6 5.9-4.08-1.25c-.9-.28-.91-.88.18-1.3 4.2-1.74 10.15-4.14 11.23-4.59.88-.36 1.7.2 1.05 1.29z"/></svg>
         TELEGRAM
       </span>
       <span v-if="isConnectionError" class="animate-pulse text-red-500">⚠ ERROR</span>
       <span v-else-if="status === 'active'" class="bg-sky-500/20 text-sky-400 px-1.5 rounded">{{ chats.length }}</span>
    </div>

    <!-- Состояния -->
    <div v-if="status === 'loading' && !isConnectionError" class="text-zinc-600 text-[10px] font-mono italic animate-pulse">SYNCING...</div>
    <div v-else-if="status === 'no_config'" class="text-zinc-700 text-[10px] font-mono border-l-2 border-zinc-800 pl-2">SESSION_NOT_SET</div>
    <div v-else-if="status === 'empty' && !isConnectionError" class="text-zinc-500 text-[10px] font-mono py-2 flex items-center gap-2"><span class="w-1.5 h-1.5 bg-zinc-700 rounded-full"></span> ALL_READ</div>
    <div v-else-if="status === 'auth_error'" class="text-red-500 text-[10px] font-mono">AUTH_ERROR</div>

    <!-- Список -->
    <div v-else-if="status === 'active'" class="flex flex-col gap-2" :class="{'opacity-50 grayscale': isConnectionError}">
      <div 
        v-for="chat in chats" 
        :key="chat.id"
        class="relative bg-zinc-900/50 border p-2 rounded flex items-center justify-between group transition-all duration-500"
        :class="[
            chat.isSessionNew ? 'border-amber-500 shadow-[0_0_10px_rgba(245,158,11,0.2)]' : 'border-zinc-800 hover:border-sky-500/30'
        ]"
      >
         <div v-if="chat.isSessionNew" class="absolute -top-1 -right-1 bg-amber-500 text-black text-[8px] font-bold px-1 rounded animate-bounce z-10">NEW</div>

         <div class="flex flex-col overflow-hidden mr-2 w-full">
             <div class="flex items-center justify-between w-full">
                 <span class="text-zinc-200 font-bold text-xs truncate group-hover:text-sky-400 transition">{{ chat.name }}</span>
                 <span class="text-[9px] font-mono flex-shrink-0 ml-2" :class="chat.isSessionNew ? 'text-amber-400' : 'text-zinc-600'">{{ formatDate(chat.date) }}</span>
             </div>
             <span class="text-[10px] text-zinc-500 truncate mt-0.5">
                 <span v-if="chat.isSessionNew" class="text-amber-500/70 font-bold">>> </span>
                 {{ chat.message }}
             </span>
         </div>
         
         <div class="flex-shrink-0 text-[10px] font-mono font-bold px-1.5 py-0.5 rounded border ml-2 transition-colors duration-500"
            :class="chat.isSessionNew ? 'bg-amber-900/30 text-amber-400 border-amber-500/20' : 'bg-sky-900/30 text-sky-400 border-sky-500/20'">
             {{ chat.count }}
         </div>
      </div>
    </div>
    
    <div v-if="isConnectionError && chats.length > 0" class="text-[9px] text-red-500/80 font-mono text-center">
       CONNECTION LOST - DATA MIGHT BE STALE
    </div>

  </div>
</template>