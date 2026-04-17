<script setup>
import { ref, onMounted, onUnmounted, watch } from 'vue'
import { request } from '@/api'
import { useSignalR } from '@/composables/useSignalR'
import UserStatusSettingsModal from '@/components/UserStatusSettingsModal.vue'

const props = defineProps({ 
    dashboardId: Number,
    teamMembers: { type: Array, default: () => [] }
})

const { on, off } = useSignalR()
const statuses = ref([])
const loading = ref(true)
const isSettingsOpen = ref(false)
const myUsername = localStorage.getItem('username') || ''

// 1. Локальное время для реактивности UI
const now = ref(Date.now())

const fetchStatuses = async () => {
if (statuses.value.length === 0) loading.value = true
    try {
        let extraUsers = []
        try {
            const conf = await request(`/users/status/settings/${props.dashboardId}`)
            // FIX: Читаем и с маленькой, и с большой буквы
            extraUsers = conf.extraUsers || conf.ExtraUsers || [] 
        } catch(e) {}

        const allUsers = [...new Set([...props.teamMembers, ...extraUsers])]
        
        if (allUsers.length > 0) {
            statuses.value = await request('/users/status/batch', {
                method: 'POST',
                body: JSON.stringify(allUsers)
            })
        } else {
            statuses.value = []
        }
        // Обновляем таймер при получении данных
        now.value = Date.now()
    } catch(e) {
        console.error(e)
    } finally {
        loading.value = false
    }
}

// 2. Функция времени теперь зависит от now.value
const timeAgo = (dateStr) => {
    if (!dateStr) return ''
    
    // Этот вызов нужен, чтобы Vue подписался на изменения now
    const _ = now.value 

    let safeDateStr = dateStr
    if (!dateStr.endsWith('Z') && !dateStr.includes('+')) safeDateStr += 'Z'
    
    const targetDate = new Date(safeDateStr)
    // Используем new Date(), так как now.value обновляется не чаще раза в минуту,
    // а для точности лучше брать текущий момент, инициированный тиком
    const current = new Date()
    
    const diffMs = current - targetDate
    const diff = diffMs / 1000 / 60 

    if (diff < 0) return 'Just now'
    if (diff < 2) return 'Just now'
    if (diff < 60) return `${Math.floor(diff)}m ago`
    if (diff < 1440) return `${Math.floor(diff/60)}h ago`
    return `${Math.floor(diff/1440)}d ago`
}

const getColorClass = (color) => {
    const map = {
        'emerald': 'text-emerald-400 border-emerald-500/30 bg-emerald-900/10',
        'amber': 'text-amber-400 border-amber-500/30 bg-amber-900/10',
        'red': 'text-red-400 border-red-500/30 bg-red-900/10',
        'blue': 'text-blue-400 border-blue-500/30 bg-blue-900/10',
        'purple': 'text-purple-400 border-purple-500/30 bg-purple-900/10',
        'zinc': 'text-zinc-400 border-zinc-500/30 bg-zinc-900/10'
    }
    return map[color] || map['zinc']
}

const handleUpdate = () => fetchStatuses()

let uiInterval
onMounted(() => {
    fetchStatuses()
    
    // 3. Таймер только для UI (без сети) - обновляем now раз в минуту
    uiInterval = setInterval(() => {
        now.value = Date.now()
    }, 60000)

    on('userstatus', handleUpdate)
})

onUnmounted(() => {
    clearInterval(uiInterval)
    off('userstatus', handleUpdate)
})

watch(() => props.teamMembers, fetchStatuses, { deep: true })
</script>

<template>
  <div class="space-y-3 relative group/widget">
    <UserStatusSettingsModal 
        :is-open="isSettingsOpen" 
        :dashboard-id="dashboardId" 
        @close="isSettingsOpen = false" 
        @refresh="fetchStatuses" 
    />

    <!-- Header -->
    <div class="flex items-center justify-between text-[10px] font-mono font-bold text-blue-500 uppercase tracking-widest border-b border-blue-500/20 pb-1">
        <span class="flex items-center gap-2"><span>👥</span> TEAM_STATUS</span>
        <button @click="isSettingsOpen = true" class="text-zinc-600 hover:text-blue-400 opacity-0 group-hover/widget:opacity-100 transition" title="My Status">⚙</button>
    </div>

    <div v-if="loading && statuses.length === 0" class="text-zinc-600 text-[10px] font-mono italic animate-pulse">
        SYNCING_PERSONNEL...
    </div>

    <div v-else class="flex flex-col gap-2">
        <div v-for="user in statuses" :key="user.username" 
             class="relative group/card bg-zinc-900/30 border border-zinc-800 p-2 rounded flex items-start gap-3 transition hover:bg-zinc-900/50"
             :class="{'border-l-2 border-l-blue-500': user.username === myUsername}"
        >
            <div class="w-8 h-8 rounded bg-zinc-950 border border-zinc-800 flex items-center justify-center text-lg shadow-sm">
                {{ user.statusEmoji }}
            </div>

            <div class="flex-1 min-w-0">
                <div class="flex justify-between items-center">
                    <span class="text-xs font-bold font-mono text-zinc-300 truncate" :title="user.username">
                        {{ user.username }}
                        <span v-if="user.username === myUsername" class="text-[9px] text-blue-500 ml-1">(YOU)</span>
                    </span>
                    <!-- Время -->
                    <span class="text-[9px] text-zinc-600 font-mono">{{ timeAgo(user.updatedAt) }}</span>
                </div>
                
                <div class="mt-1 flex items-center gap-2">
                    <span class="text-[10px] font-mono px-1.5 py-0.5 rounded border" :class="getColorClass(user.statusColor)">
                        {{ user.statusText }}
                    </span>
                </div>
            </div>

            <div v-if="user.statusMessage" class="absolute left-0 bottom-full mb-1 w-full opacity-0 group-hover/card:opacity-100 pointer-events-none transition z-20 px-2">
                <div class="bg-black border border-zinc-700 text-zinc-300 text-[10px] p-2 rounded shadow-xl font-mono relative">
                    {{ user.statusMessage }}
                    <div class="absolute bottom-[-5px] left-4 w-2 h-2 bg-black border-b border-r border-zinc-700 transform rotate-45"></div>
                </div>
            </div>
        </div>
    </div>
  </div>
</template>