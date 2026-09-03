<script setup>
import { ref, watch } from 'vue'
import { request } from '@/api'

const props = defineProps({ isOpen: Boolean, dashboardId: Number })
const emit = defineEmits(['close', 'refresh'])

// Расширенный конфиг
const config = ref({ 
    host: 'imap.gmail.com', 
    port: 993, 
    useSsl: true, 
    username: '', 
    password: '',
    mailbox: 'INBOX',
    allowSelfSigned: false 
})

const isSaving = ref(false)

watch(() => props.isOpen, (val) => {
  if (val) loadConfig()
})

const loadConfig = async () => {
    try {
        const data = await request(`/integrations/${props.dashboardId}/email/settings`)
        // Если на бэкенде уже есть настройки, мержим их с дефолтными
        if (data.host) {
            config.value = { ...config.value, ...data }
        }
    } catch(e) {
        console.warn("Failed to load email settings")
    }
}

const save = async () => {
    isSaving.value = true
    try {
        await request(`/integrations/${props.dashboardId}/email/settings`, { 
            method: 'PUT', 
            body: JSON.stringify(config.value) 
        })
        emit('refresh')
        emit('close')
    } catch(e) { 
        alert(e.message) 
    } finally { 
        isSaving.value = false 
    }
}
</script>

<template>
  <div v-if="isOpen" class="fixed inset-0 z-50 flex items-center justify-center p-4">
    <div class="absolute inset-0 bg-black/90 backdrop-blur-sm" @click="$emit('close')"></div>
    
    <div class="relative bg-zinc-950 border border-emerald-500/30 w-full max-w-md flex flex-col rounded shadow-[0_0_50px_rgba(0,0,0,0.5)] font-sans overflow-hidden">            
      <!-- Header -->
      <div class="p-3 border-b border-zinc-800 bg-zinc-900/50 flex justify-between items-center">
        <h2 class="text-emerald-500 font-mono font-bold tracking-widest text-sm uppercase">
            <span class="animate-pulse mr-2">●</span>IMAP_PROTOCOL_CONFIG
        </h2>
        <button @click="$emit('close')" class="text-zinc-500 hover:text-red-400 text-xs font-mono transition">[ESC]</button>
      </div>

      <div class="p-6 space-y-5">
        <!-- Host & Port Grid -->
        <div class="grid grid-cols-3 gap-4">
            <div class="col-span-2 space-y-1">
                <label class="label-cyber">IMAP Host</label>
                <input v-model="config.host" class="input-cyber" placeholder="imap.gmail.com">
            </div>
            <div class="space-y-1">
                <label class="label-cyber">Port</label>
                <input v-model.number="config.port" type="number" class="input-cyber" placeholder="993">
            </div>
        </div>

        <!-- Credentials -->
        <div class="space-y-4">
            <div class="space-y-1">
                <label class="label-cyber">Identity (Email)</label>
                <input v-model="config.username" class="input-cyber" placeholder="user@example.com">
            </div>
            <div class="space-y-1">
                <label class="label-cyber">Access Secret (Password/App Token)</label>
                <input v-model="config.password" type="password" class="input-cyber" placeholder="••••••••••••">
            </div>
        </div>

        <!-- Advanced Toggle Section -->
        <div class="grid grid-cols-2 gap-4 pt-2">
            <div class="flex items-center gap-3 bg-zinc-900/40 p-2 border border-zinc-800 rounded group hover:border-emerald-500/30 transition">
                <input type="checkbox" v-model="config.useSsl" id="ssl" class="accent-emerald-500 w-4 h-4 cursor-pointer">
                <label for="ssl" class="text-[10px] text-zinc-400 font-mono cursor-pointer uppercase select-none">Use SSL/TLS</label>
            </div>
            <div class="flex items-center gap-3 bg-zinc-900/40 p-2 border border-zinc-800 rounded group hover:border-red-500/30 transition">
                <input type="checkbox" v-model="config.allowSelfSigned" id="selfsigned" class="accent-red-500 w-4 h-4 cursor-pointer">
                <label for="selfsigned" class="text-[10px] text-zinc-400 font-mono cursor-pointer uppercase select-none">Allow Unsafe</label>
            </div>
        </div>

        <div class="space-y-1">
            <label class="label-cyber text-zinc-600">Mailbox Folder</label>
            <input v-model="config.mailbox" class="input-cyber opacity-60 focus:opacity-100" placeholder="INBOX">
        </div>
      </div>

      <!-- Footer -->
      <div class="p-4 border-t border-zinc-800 bg-zinc-900/30 flex justify-end gap-3">
         <button @click="$emit('close')" class="px-4 py-2 text-zinc-500 hover:text-zinc-300 font-mono text-xs transition">CANCEL</button>
         <button @click="save" :disabled="isSaving" class="btn-save">
            <span v-if="isSaving" class="animate-spin">/</span> 
            {{ isSaving ? 'INITIALIZING...' : 'ESTABLISH LINK' }}
         </button>
      </div>
    </div>
  </div>
</template>

<style scoped>
.label-cyber { 
    @apply block text-[10px] text-zinc-500 font-mono uppercase tracking-wider; 
}

.input-cyber { 
    @apply w-full bg-zinc-950 border border-zinc-800 p-2.5 text-xs font-mono text-emerald-100 focus:border-emerald-500/50 focus:ring-1 focus:ring-emerald-500/20 outline-none transition-all placeholder:text-zinc-800; 
}

.btn-save {
    @apply px-6 py-2 bg-emerald-900/20 border border-emerald-500/50 text-emerald-400 font-mono text-xs hover:bg-emerald-500 hover:text-black transition-all flex items-center gap-2 active:scale-95;
}

/* Chrome, Safari, Edge, Opera */
input::-webkit-outer-spin-button,
input::-webkit-inner-spin-button {
  -webkit-appearance: none;
  margin: 0;
}

/* Firefox */
input[type=number] {
  -moz-appearance: textfield;
}
</style>