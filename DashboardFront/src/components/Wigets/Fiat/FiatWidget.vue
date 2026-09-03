<script setup>
import { ref, onMounted } from 'vue'
import { request } from '@/api'
import FiatSettingsModal from '@/components/Wigets/Fiat/FiatSettingsModal.vue'

const props = defineProps({
  dashboardId: Number
})

const rates = ref([])
const base = ref('USD')
const loading = ref(true)
const isSettingsOpen = ref(false)
const isInverse = ref(false)

// Рекурсивная функция для поиска исторических данных
// Если за "вчера" данных нет (404), пробуем "позавчера" и т.д.
const fetchHistoricalData = async (baseCode, daysBack = 1) => {
    if (daysBack > 5) return null // Сдаемся после 5 попыток

    const d = new Date()
    d.setDate(d.getDate() - daysBack)
    const dateStr = d.toISOString().split('T')[0] // YYYY-MM-DD

    try {
        const url = `https://cdn.jsdelivr.net/npm/@fawazahmed0/currency-api@${dateStr}/v1/currencies/${baseCode}.json`
        const res = await fetch(url)
        if (!res.ok) throw new Error('No data')
        const data = await res.json()
        return data[baseCode]
    } catch (e) {
        // Пробуем на день раньше
        return await fetchHistoricalData(baseCode, daysBack + 1)
    }
}

const fetchRates = async () => {
  loading.value = true
  try {
    const config = await request(`/integrations/${props.dashboardId}/fiat/settings`)
    
    const targets = (config.targets || ['EUR', 'RUB']).map(t => t.toLowerCase())
    const baseCode = (config.baseCurrency || 'USD').toLowerCase()
    const useInverse = config.useInverse || false
    
    base.value = baseCode.toUpperCase()
    isInverse.value = useInverse

    const validTargets = targets.filter(t => t !== baseCode)
    if (validTargets.length === 0) {
        rates.value = []; loading.value = false; return
    }

    // 1. Текущие курсы
    const resLatest = await fetch(`https://cdn.jsdelivr.net/npm/@fawazahmed0/currency-api@latest/v1/currencies/${baseCode}.json`)
    const dataLatest = await resLatest.json()
    const ratesLatest = dataLatest[baseCode]

    // 2. Исторические курсы (с фоллбеком)
    const ratesPrev = await fetchHistoricalData(baseCode)

    // 3. Расчет
    rates.value = validTargets.map(code => {
        let current = ratesLatest[code]
        if (current === undefined) return null

        // Если истории нет вообще, берем текущий (будет 0%)
        let prev = (ratesPrev && ratesPrev[code]) ? ratesPrev[code] : current
        
        // --- ИНВЕРСИЯ ---
        if (useInverse) {
            // Защита от деления на ноль
            if (current !== 0) current = 1 / current
            if (prev !== 0) prev = 1 / prev
        }

        let change = 0
        if (prev !== 0) {
            change = ((current - prev) / prev) * 100
        }

        return {
            code: code.toUpperCase(),
            val: current,
            change
        }
    }).filter(r => r !== null)

  } catch (e) {
    console.error("Fiat Widget Error:", e)
  } finally {
    loading.value = false
  }
}

onMounted(() => {
    fetchRates()
    setInterval(fetchRates, 3600000) 
})
</script>

<template>
  <div class="space-y-2 relative group/widget">
    
    <FiatSettingsModal 
        :is-open="isSettingsOpen" 
        :dashboard-id="dashboardId" 
        @close="isSettingsOpen = false" 
        @refresh="fetchRates" 
    />

    <!-- Header -->
    <div class="flex items-center justify-between text-[10px] font-mono font-bold text-green-500 uppercase tracking-widest border-b border-green-500/20 pb-1">
        <span>FOREX_DATA</span>
        <div class="flex items-center gap-2">
            <!-- Отображаем 1/USD если инверсия -->
            <span class="text-green-500/50">{{ isInverse ? '1/' : '' }}{{ base }}</span>
            <button 
                @click="isSettingsOpen = true" 
                class="text-zinc-600 hover:text-green-400 opacity-0 group-hover/widget:opacity-100 transition"
                title="Configure Currencies"
            >
                ⚙
            </button>
        </div>
    </div>

    <!-- Loading -->
    <div v-if="loading && rates.length === 0" class="text-zinc-600 text-[10px] font-mono italic animate-pulse">
        SYNCING_RATES...
    </div>

    <!-- Empty -->
    <div v-else-if="rates.length === 0" class="text-zinc-700 text-[10px] font-mono italic py-2 text-center border border-dashed border-zinc-800 cursor-pointer hover:border-green-500/50 hover:text-green-400 transition" @click="isSettingsOpen = true">
        [+] ADD PAIRS
    </div>

    <!-- List -->
    <div v-else class="space-y-1">
        <div v-for="rate in rates" :key="rate.code" class="flex items-center justify-between p-2 rounded hover:bg-zinc-900/50 transition border border-transparent hover:border-zinc-800 group">
            <div class="flex items-center gap-2">
                <span class="font-bold text-zinc-400 text-xs">{{ rate.code }}</span>
            </div>
            <div class="text-right">
                <div class="text-zinc-200 text-xs font-mono">
                    {{ rate.val > 5 ? rate.val.toFixed(2) : rate.val.toFixed(4) }}
                </div>
                
                <div class="text-[9px] font-mono" :class="rate.change >= 0 ? 'text-emerald-500' : 'text-red-500'">
                    {{ rate.change >= 0 ? '+' : '' }}{{ rate.change.toFixed(2) }}%
                </div>
            </div>
        </div>
    </div>
  </div>
</template>