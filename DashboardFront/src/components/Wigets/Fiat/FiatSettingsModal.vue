<script setup>
import { ref, watch, computed } from 'vue'
import { request } from '@/api'

const props = defineProps({
  isOpen: Boolean,
  dashboardId: Number
})

const emit = defineEmits(['close', 'refresh'])

const baseCurrency = ref('usd')
const targets = ref([])
// Новая настройка
const useInverse = ref(false) 

const availableCurrencies = ref({}) 
const searchQuery = ref('')
const isSaving = ref(false)
const isLoadingList = ref(false)

watch(() => props.isOpen, (val) => {
  if (val) loadData()
})

const loadData = async () => {
    isLoadingList.value = true
    try {
        const res = await fetch('https://cdn.jsdelivr.net/npm/@fawazahmed0/currency-api@latest/v1/currencies.json')
        availableCurrencies.value = await res.json()

        const config = await request(`/integrations/${props.dashboardId}/fiat/settings`)
        baseCurrency.value = (config.baseCurrency || 'usd').toLowerCase()
        targets.value = (config.targets || []).map(t => t.toLowerCase())
        // Загружаем настройку
        useInverse.value = config.useInverse || false 
    } catch(e) {
        console.error(e)
    } finally {
        isLoadingList.value = false
    }
}

const toggleTarget = (code) => {
    if (code === baseCurrency.value) return
    if (targets.value.includes(code)) {
        targets.value = targets.value.filter(c => c !== code)
    } else {
        targets.value.push(code)
    }
}

const filteredList = computed(() => {
    const query = searchQuery.value.toLowerCase()
    return Object.entries(availableCurrencies.value)
        .filter(([code, name]) => {
            if (!name) return false
            return code.includes(query) || name.toLowerCase().includes(query)
        })
        .sort((a, b) => a[0].localeCompare(b[0]))
})

const save = async () => {
    isSaving.value = true
    try {
        await request(`/integrations/${props.dashboardId}/fiat/settings`, {
            method: 'PUT',
            body: JSON.stringify({
                baseCurrency: baseCurrency.value.toUpperCase(),
                targets: targets.value.map(t => t.toUpperCase()),
                useInverse: useInverse.value // Сохраняем
            })
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

    <div class="relative bg-zinc-950 border border-green-500/30 w-full max-w-md flex flex-col rounded shadow font-sans overflow-hidden">
      
      <div class="p-3 border-b border-zinc-800 bg-zinc-900/50 flex justify-between items-center">
        <h2 class="text-green-500 font-mono font-bold tracking-widest text-sm">FOREX_CONFIG</h2>
        <button @click="$emit('close')" class="text-zinc-500 hover:text-red-400 text-xs font-mono">[ESC]</button>
      </div>

      <div class="p-6 space-y-6 flex-1 overflow-y-auto custom-scrollbar">
        
        <!-- Base Currency -->
        <div class="space-y-2">
            <label class="text-[10px] text-zinc-500 font-mono uppercase">Base Currency</label>
            <select v-model="baseCurrency" class="w-full bg-zinc-950 border border-zinc-800 p-2 text-xs font-mono text-green-100 outline-none focus:border-green-500 uppercase">
                <option v-for="(name, code) in availableCurrencies" :key="code" :value="code">
                    {{ code.toUpperCase() }} - {{ name }}
                </option>
            </select>
        </div>

        <!-- Inverse Toggle -->
        <div class="flex items-center gap-3 bg-zinc-900/50 p-2 border border-zinc-800 rounded">
            <input type="checkbox" v-model="useInverse" id="inv" class="accent-green-500 cursor-pointer">
            <div class="flex flex-col">
                <label for="inv" class="text-xs text-zinc-300 font-mono cursor-pointer">Invert Rates</label>
                <span class="text-[9px] text-zinc-600">
                    {{ useInverse ? `Price of 1 Target in ${baseCurrency.toUpperCase()}` : `How much Target you get for 1 ${baseCurrency.toUpperCase()}` }}
                </span>
            </div>
        </div>

        <!-- Targets -->
        <div class="space-y-2">
            <label class="text-[10px] text-zinc-500 font-mono uppercase">Tracked Currencies</label>
            <input v-model="searchQuery" class="w-full bg-zinc-950 border border-zinc-800 p-2 mb-2 text-xs font-mono text-zinc-300 focus:border-green-500 outline-none" placeholder="Filter...">

            <div class="border border-zinc-800 rounded h-60 overflow-y-auto custom-scrollbar bg-zinc-900/30 p-1">
                <div v-if="isLoadingList" class="text-center py-4 text-zinc-600 text-xs">LOADING_LIST...</div>
                
                <div 
                    v-for="[code, name] in filteredList" 
                    :key="code"
                    @click="toggleTarget(code)"
                    class="flex items-center gap-3 p-2 hover:bg-zinc-800/50 cursor-pointer rounded"
                    :class="{'opacity-50 pointer-events-none': code === baseCurrency}"
                >
                    <div class="w-4 h-4 border border-zinc-600 flex items-center justify-center rounded-sm transition"
                         :class="targets.includes(code) ? 'bg-green-500 border-green-500' : ''">
                        <span v-if="targets.includes(code)" class="text-black text-[10px] font-bold">✓</span>
                    </div>
                    <div class="flex flex-col">
                        <span class="text-xs font-mono font-bold uppercase" :class="targets.includes(code) ? 'text-green-100' : 'text-zinc-400'">{{ code }}</span>
                        <span class="text-[9px] text-zinc-600 truncate w-40">{{ name }}</span>
                    </div>
                </div>
            </div>
        </div>

      </div>

      <div class="p-4 border-t border-zinc-800 bg-zinc-900/30 flex justify-end">
         <button @click="save" :disabled="isSaving" class="px-6 py-2 bg-green-900/20 border border-green-500/50 text-green-400 font-mono text-xs hover:bg-green-500 hover:text-black transition flex items-center gap-2">
            <span v-if="isSaving" class="animate-spin">/</span> SAVE CONFIG
         </button>
      </div>

    </div>
  </div>
</template>

<style scoped>
.custom-scrollbar::-webkit-scrollbar { width: 4px; }
.custom-scrollbar::-webkit-scrollbar-track { background: transparent; }
.custom-scrollbar::-webkit-scrollbar-thumb { background: #3f3f46; border-radius: 2px; }
.custom-scrollbar::-webkit-scrollbar-thumb:hover { background: #22c55e; }
</style>