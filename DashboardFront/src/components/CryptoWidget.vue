<script setup>
import { ref, onMounted } from 'vue'
import { request } from '@/api'
import CryptoSettingsModal from '@/components/CryptoSettingsModal.vue'

const props = defineProps({
  dashboardId: Number
})

const coins = ref([])
const loading = ref(true)
const isSettingsOpen = ref(false)

const fetchPrices = async () => {
  loading.value = true
  try {
    // 1. Конфиг
    const config = await request(`/integrations/${props.dashboardId}/crypto/settings`)
    const coinIds = config.coins || ['bitcoin', 'ethereum', 'the-open-network'] 

    if (coinIds.length === 0) {
        coins.value = []
        loading.value = false
        return
    }

    // 2. CoinGecko
    const idsString = coinIds.join(',')
    const res = await fetch(`https://api.coingecko.com/api/v3/simple/price?ids=${idsString}&vs_currencies=usd&include_24hr_change=true`)
    
    if (!res.ok) throw new Error('API Error')
    const data = await res.json()
    
    coins.value = coinIds.map(id => {
        const item = data[id]
        if (!item) return null 
        return {
            id: id,
            name: id.toUpperCase(), // Можно усложнить и хранить Symbol в БД, но так проще
            val: item.usd,
            change: item.usd_24h_change
        }
    }).filter(c => c !== null)

  } catch (e) {
    console.error(e)
  } finally {
    loading.value = false
  }
}

onMounted(() => {
    fetchPrices()
    setInterval(fetchPrices, 300000) 
})
</script>

<template>
  <div class="space-y-2 relative group/widget">
    
    <!-- MODAL -->
    <CryptoSettingsModal 
        :is-open="isSettingsOpen" 
        :dashboard-id="dashboardId" 
        @close="isSettingsOpen = false" 
        @refresh="fetchPrices" 
    />

    <!-- HEADER -->
    <div class="flex items-center justify-between text-[10px] font-mono font-bold text-indigo-500 uppercase tracking-widest border-b border-indigo-500/20 pb-1">
        <span>MARKET_DATA</span>
        
        <div class="flex items-center gap-2">
            <span class="text-indigo-500/50">USD</span>
            <!-- Settings Button (Visible on Hover) -->
            <button 
                @click="isSettingsOpen = true" 
                class="text-zinc-600 hover:text-indigo-400 opacity-0 group-hover/widget:opacity-100 transition"
                title="Configure Assets"
            >
                ⚙
            </button>
        </div>
    </div>

    <!-- BODY -->
    <div v-if="loading && coins.length === 0" class="text-zinc-600 text-[10px] font-mono italic animate-pulse">
        CALCULATING...
    </div>

    <div v-else-if="coins.length === 0" class="text-zinc-700 text-[10px] font-mono italic py-2 text-center border border-dashed border-zinc-800 cursor-pointer hover:border-indigo-500/50 hover:text-indigo-400 transition" @click="isSettingsOpen = true">
        [+] ADD ASSETS
    </div>

    <div v-else class="space-y-1">
        <div v-for="coin in coins" :key="coin.id" class="flex items-center justify-between p-2 rounded hover:bg-zinc-900/50 transition border border-transparent hover:border-zinc-800 group">
            <div class="flex items-center gap-2 overflow-hidden">
                <span class="font-bold text-zinc-400 text-xs truncate max-w-[90px]" :title="coin.id">{{ coin.name }}</span>
            </div>
            <div class="text-right flex-shrink-0">
                <div class="text-zinc-200 text-xs font-mono">${{ coin.val.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 6 }) }}</div>
                <div class="text-[9px] font-mono" :class="coin.change >= 0 ? 'text-emerald-500' : 'text-red-500'">
                    {{ coin.change >= 0 ? '+' : '' }}{{ coin.change?.toFixed(2) }}%
                </div>
            </div>
        </div>
    </div>
  </div>
</template>