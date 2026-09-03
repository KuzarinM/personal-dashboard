<script setup>
import { ref, watch } from 'vue'
import { request } from '@/api'

const props = defineProps({
  isOpen: Boolean,
  dashboardId: Number
})

const emit = defineEmits(['close', 'refresh'])

const myCoins = ref([]) // Текущий список ID
const searchQuery = ref('')
const searchResults = ref([])
const isSearching = ref(false)
const isSaving = ref(false)

// --- LOAD ---
watch(() => props.isOpen, (val) => {
  if (val) {
    searchQuery.value = ''
    searchResults.value = []
    loadConfig()
  }
})

const loadConfig = async () => {
  try {
    const data = await request(`/integrations/${props.dashboardId}/crypto/settings`)
    myCoins.value = data.coins || []
  } catch (e) {
    myCoins.value = []
  }
}

// --- SEARCH (CoinGecko) ---
let debounceTimer
const onSearchInput = () => {
  clearTimeout(debounceTimer)
  if (searchQuery.value.length < 2) {
    searchResults.value = []
    return
  }
  isSearching.value = true
  debounceTimer = setTimeout(performSearch, 500)
}

const performSearch = async () => {
  try {
    const res = await fetch(`https://api.coingecko.com/api/v3/search?query=${searchQuery.value}`)
    const data = await res.json()
    // Берем первые 7 результатов
    searchResults.value = (data.coins || []).slice(0, 7)
  } catch (e) {
    console.error(e)
  } finally {
    isSearching.value = false
  }
}

// --- ACTIONS ---
const addCoin = (coin) => {
  if (!myCoins.value.includes(coin.id)) {
    myCoins.value.push(coin.id)
  }
  searchQuery.value = ''
  searchResults.value = []
}

const removeCoin = (id) => {
  myCoins.value = myCoins.value.filter(c => c !== id)
}

const save = async () => {
  isSaving.value = true
  try {
    await request(`/integrations/${props.dashboardId}/crypto/settings`, {
      method: 'PUT',
      body: JSON.stringify({ coins: myCoins.value })
    })
    emit('refresh')
    emit('close')
  } catch (e) {
    alert(e.message)
  } finally {
    isSaving.value = false
  }
}
</script>

<template>
  <div v-if="isOpen" class="fixed inset-0 z-50 flex items-center justify-center p-4">
    <div class="absolute inset-0 bg-black/90 backdrop-blur-sm" @click="$emit('close')"></div>

    <div class="relative bg-zinc-950 border border-indigo-500/30 w-full max-w-md flex flex-col rounded shadow font-sans overflow-hidden">
      
      <!-- Header -->
      <div class="p-3 border-b border-zinc-800 bg-zinc-900/50 flex justify-between items-center">
        <h2 class="text-indigo-500 font-mono font-bold tracking-widest text-sm">MARKET_CONFIG</h2>
        <button @click="$emit('close')" class="text-zinc-500 hover:text-red-400 text-xs font-mono">[ESC]</button>
      </div>

      <div class="p-6 space-y-6">
        
        <!-- Search -->
        <div class="space-y-2 relative">
            <label class="text-[10px] text-zinc-500 font-mono uppercase">Add Asset</label>
            <div class="relative">
                <input 
                    v-model="searchQuery" 
                    @input="onSearchInput"
                    class="w-full bg-zinc-950 border border-zinc-800 p-2 pl-8 text-xs font-mono text-indigo-100 focus:border-indigo-500 outline-none placeholder:text-zinc-700" 
                    placeholder="Search (e.g. BTC, Solana)..."
                >
                <span class="absolute left-2 top-2 text-zinc-600 text-xs">🔍</span>
                <span v-if="isSearching" class="absolute right-2 top-2 text-indigo-500 text-xs animate-spin">/</span>
            </div>

            <!-- Dropdown Results -->
            <div v-if="searchResults.length > 0" class="absolute z-10 w-full bg-zinc-900 border border-zinc-700 rounded shadow-xl mt-1 overflow-hidden">
                <div 
                    v-for="coin in searchResults" 
                    :key="coin.id" 
                    @click="addCoin(coin)"
                    class="p-2 hover:bg-indigo-900/30 cursor-pointer flex items-center gap-3 border-b border-zinc-800 last:border-0"
                >
                    <img :src="coin.thumb" class="w-4 h-4" alt="">
                    <div class="flex flex-col">
                        <span class="text-xs font-bold text-zinc-200">{{ coin.name }}</span>
                        <span class="text-[9px] text-zinc-500 font-mono">{{ coin.symbol }}</span>
                    </div>
                </div>
            </div>
        </div>

        <!-- Watchlist -->
        <div class="space-y-2">
            <label class="text-[10px] text-zinc-500 font-mono uppercase">Active Watchlist</label>
            <div class="flex flex-wrap gap-2">
                <div v-for="id in myCoins" :key="id" class="bg-zinc-900 border border-zinc-800 px-2 py-1 rounded flex items-center gap-2 group hover:border-indigo-500/50 transition">
                    <span class="text-xs font-mono text-indigo-200 uppercase">{{ id }}</span>
                    <button @click="removeCoin(id)" class="text-zinc-600 hover:text-red-500 text-[10px] font-bold px-1">×</button>
                </div>
                <div v-if="myCoins.length === 0" class="text-zinc-700 text-xs font-mono italic w-full text-center py-2 border border-dashed border-zinc-800">
                    List is empty
                </div>
            </div>
        </div>

      </div>

      <!-- Footer -->
      <div class="p-4 border-t border-zinc-800 bg-zinc-900/30 flex justify-end">
         <button @click="save" :disabled="isSaving" class="px-6 py-2 bg-indigo-900/20 border border-indigo-500/50 text-indigo-400 font-mono text-xs hover:bg-indigo-500 hover:text-black transition flex items-center gap-2">
            <span v-if="isSaving" class="animate-spin">/</span> SAVE CHANGES
         </button>
      </div>

    </div>
  </div>
</template>