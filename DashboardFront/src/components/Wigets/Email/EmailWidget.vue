<script setup>
import { ref, onMounted, onUnmounted } from 'vue'
import { request } from '@/api'
import { useSignalR } from '@/composables/useSignalR'
import EmailSettingsModal from '@/components/Wigets/Email/EmailSettingsModal.vue'

const props = defineProps({ dashboardId: Number })
const emit = defineEmits(['error-change'])

const { on, off } = useSignalR()
const emails = ref([])
const status = ref('loading')
const loading = ref(false)
const isSettingsOpen = ref(false)
const imapHost = ref('') // Храним хост для генерации ссылок

const formatDate = (dateValue) => {
    if (!dateValue) return ''
    const date = new Date(dateValue)
    const now = new Date()
    if (isNaN(date.getTime())) return '...'
    if (date.toDateString() === now.toDateString()) {
        return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
    }
    return date.toLocaleDateString([], { day: 'numeric', month: 'short' })
}

// Логика генерации ссылки на письмо
const getMailLink = (mail) => {
    // Если бэкенд прислал готовую ссылку (например, для специфических систем)
    if (mail.link) return mail.link

    const host = imapHost.value.toLowerCase()
    const msgId = mail.messageId // Ожидаем, что бэкенд пришлет Message-ID

    if (!msgId) return '#'

    // GMAIL
    if (host.includes('gmail.com')) {
        return `https://mail.google.com/mail/u/0/#search/rfc822msgid:${encodeURIComponent(msgId)}`
    }
    // OUTLOOK / OFFICE 365
    if (host.includes('outlook') || host.includes('office365')) {
        return `https://outlook.live.com/mail/0/deeplink/search?q=${encodeURIComponent(msgId)}`
    }
    // YANDEX
    if (host.includes('yandex')) {
        return `https://mail.yandex.ru/#search?request=${encodeURIComponent(msgId)}`
    }
    
    // Fallback: просто пытаемся открыть корень сервиса
    if (host.includes('mail.ru')) return 'https://e.mail.ru/inbox/'
    
    return '#'
}

const fetchEmails = async () => {
    if (loading.value) return
    loading.value = true
    try {
        // 1. Сначала узнаем настройки (хост)
        const settings = await request(`/integrations/${props.dashboardId}/email/settings`)
        imapHost.value = settings.host || ''

        // 2. Грузим письма
        const data = await request(`/integrations/${props.dashboardId}/email/messages`)
        
        if (data.notConfigured) {
            status.value = 'no_config'
            return
        }

        if (data.error) throw new Error(data.error)
                
        emails.value = data
        status.value = data.length > 0 ? 'active' : 'empty'
        emit('error-change', false)
    } catch(e) {
        status.value = 'error'
        emit('error-change', true)
    } finally {
        loading.value = false
    }
}

const handleUpdate = () => fetchEmails()

onMounted(() => {
    fetchEmails()
    on('email', handleUpdate)
})

onUnmounted(() => {
    off('email', handleUpdate)
})
</script>

<template>
  <div class="space-y-3 relative group/widget">
    <EmailSettingsModal 
         :is-open="isSettingsOpen" 
         :dashboard-id="dashboardId" 
         @close="isSettingsOpen = false" 
         @refresh="fetchEmails" 
     />

    <!-- Header -->
    <div class="flex items-center justify-between text-[10px] font-mono font-bold uppercase tracking-widest border-b pb-1"
         :class="status === 'error' ? 'text-red-500 border-red-500/20' : 'text-emerald-500 border-emerald-500/20'">
        <span class="flex items-center gap-2">
            <span class="text-lg leading-none">✉</span> MAIL_RELAY
        </span>
                
        <div class="flex items-center gap-2">
            <span v-if="status === 'active'" class="bg-emerald-500/20 text-emerald-400 px-1.5 rounded">{{ emails.length }}</span>
            <span v-if="status === 'error'" class="text-red-500 animate-pulse">OFFLINE</span>
            <button @click="isSettingsOpen = true" class="text-zinc-600 hover:text-emerald-400 opacity-0 group-hover/widget:opacity-100 transition">⚙</button>
        </div>
    </div>

    <!-- States -->
    <div v-if="status === 'loading'" class="text-zinc-600 text-[10px] font-mono italic animate-pulse">SYNCING_MAIL...</div>
    <div v-if="status === 'empty'" class="text-zinc-500 text-[10px] font-mono italic py-2">INBOX_ZERO</div>

    <!-- List -->
    <div v-if="status === 'active'" class="flex flex-col gap-2 max-h-[400px] overflow-y-auto pr-1 custom-scrollbar">
        <!-- Каждое письмо теперь ссылка <a> -->
        <a v-for="mail in emails" 
             :key="mail.id" 
             :href="getMailLink(mail)"
             target="_blank"
             class="bg-zinc-900/50 border border-zinc-800 p-2.5 rounded-sm flex flex-col gap-1.5 hover:border-emerald-500/50 hover:bg-zinc-900 transition group/mail relative overflow-hidden"
        >
            <!-- Индикатор ссылки (появляется при наведении) -->
            <div class="absolute top-0 right-0 p-1 opacity-0 group-hover/mail:opacity-100 transition">
                <span class="text-[8px] text-emerald-500 font-mono">OPEN_WEBMAIL ↗</span>
            </div>

            <div class="flex justify-between items-start relative z-10">
                <span class="text-emerald-100 font-bold text-xs truncate max-w-[70%] group-hover/mail:text-emerald-400 transition">
                    {{ mail.from }}
                </span>
                <span class="text-[9px] text-zinc-600 font-mono flex-shrink-0">{{ formatDate(mail.date) }}</span>
            </div>
            <span class="text-[10px] text-zinc-400 leading-relaxed line-clamp-2 relative z-10">
                {{ mail.subject || '(No Subject)' }}
            </span>
        </a>
    </div>
  </div>
</template>