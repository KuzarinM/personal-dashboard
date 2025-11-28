<script setup>
import { ref, onMounted } from 'vue'

const prices = ref([])
const loading = ref(true)

const fetchPrices = async () => {
  try {
    // Получаем BTC, ETH, TON в USD
    const res = await fetch('https://api.coingecko.com/api/v3/simple/price?ids=bitcoin,ethereum,the-open-network&vs_currencies=usd&include_24hr_change=true')
    const data = await res.json()
    
    prices.value = [
      { name: 'BTC', val: data.bitcoin.usd, change: data.bitcoin.usd_24h_change },
      { name: 'ETH', val: data.ethereum.usd, change: data.ethereum.usd_24h_change },
      { name: 'TON', val: data['the-open-network'].usd, change: data['the-open-network'].usd_24h_change }
    ]
    loading.value = false
  } catch (e) {
    // Если лимиты API превышены, показываем фейковые данные или ошибку
    loading.value = false
  }
}

onMounted(() => {
  fetchPrices()
  // Обновляем раз в 5 минут
  setInterval(fetchPrices, 300000)
})
</script>

<template>
  <div class="bg-zinc-900/50 rounded-sm border border-zinc-800 p-4 font-mono text-xs space-y-3 relative overflow-hidden">
     <!-- Заголовок -->
     <div class="flex items-center justify-between text-zinc-500 uppercase tracking-widest border-b border-zinc-800 pb-2 mb-2">
        <span class="flex items-center gap-2">MARKET_DATA</span>
        <span class="text-emerald-500/30 text-[10px]">LIVE</span>
     </div>

     <div v-if="loading" class="text-center py-2 text-zinc-600 animate-pulse">CONNECTING...</div>

     <div v-else class="space-y-3">
       <div v-for="coin in prices" :key="coin.name" class="flex justify-between items-center group">
         <span class="text-zinc-400 font-bold">{{ coin.name }}</span>
         <div class="text-right">
           <div class="text-zinc-200">${{ coin.val.toLocaleString() }}</div>
           <div class="text-[10px]" :class="coin.change >= 0 ? 'text-emerald-500' : 'text-red-500'">
             {{ coin.change >= 0 ? '+' : '' }}{{ coin.change.toFixed(2) }}%
           </div>
         </div>
       </div>
     </div>
  </div>
</template>