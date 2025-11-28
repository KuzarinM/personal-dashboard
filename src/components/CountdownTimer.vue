<script setup>
import { ref, computed, onMounted, onUnmounted } from 'vue'

const props = defineProps({
  event: Object
})

const timeLeft = ref({ days: 0, hours: 0, minutes: 0, seconds: 0, total: 0 })
let timerInterval = null

const calculateTime = () => {
  const target = new Date(props.event.date).getTime()
  const now = new Date().getTime()
  const diff = target - now

  if (diff <= 0) {
    timeLeft.value = { days: 0, hours: 0, minutes: 0, seconds: 0, total: 0 }
    return
  }
  timeLeft.value = {
    days: Math.floor(diff / (1000 * 60 * 60 * 24)),
    hours: Math.floor((diff / (1000 * 60 * 60)) % 24),
    minutes: Math.floor((diff / 1000 / 60) % 60),
    seconds: Math.floor((diff / 1000) % 60),
    total: diff
  }
}

// Зеленый -> Желтый -> Красный
const urgencyColor = computed(() => {
  if (timeLeft.value.total <= 0) return 'text-zinc-500 border-zinc-800 bg-zinc-900' 
  if (timeLeft.value.total < 86400000) return 'text-red-500 border-red-500/50 bg-red-900/10 shadow-[0_0_15px_rgba(239,68,68,0.2)] animate-pulse'
  if (timeLeft.value.total < 604800000) return 'text-amber-400 border-amber-500/40 bg-amber-900/10'
  // Основной цвет теперь Изумрудный (Emerald)
  return 'text-emerald-400 border-emerald-500/30 bg-emerald-900/10 shadow-[0_0_10px_rgba(16,185,129,0.1)]'
})

const pad = (num) => num.toString().padStart(2, '0')

onMounted(() => {
  calculateTime()
  timerInterval = setInterval(calculateTime, 1000)
})

onUnmounted(() => clearInterval(timerInterval))
</script>

<template>
  <div 
    class="relative rounded-sm border p-4 transition-all duration-500 overflow-hidden group"
    :class="urgencyColor"
  >
    <!-- Техническая подложка -->
    <div class="absolute inset-0 opacity-10 pointer-events-none" 
         style="background-image: linear-gradient(0deg, transparent 24%, rgba(255, 255, 255, .3) 25%, rgba(255, 255, 255, .3) 26%, transparent 27%, transparent 74%, rgba(255, 255, 255, .3) 75%, rgba(255, 255, 255, .3) 76%, transparent 77%, transparent), linear-gradient(90deg, transparent 24%, rgba(255, 255, 255, .3) 25%, rgba(255, 255, 255, .3) 26%, transparent 27%, transparent 74%, rgba(255, 255, 255, .3) 75%, rgba(255, 255, 255, .3) 76%, transparent 77%, transparent); background-size: 30px 30px;">
    </div>

    <div class="flex justify-between items-center mb-3 relative z-10">
      <h3 class="font-bold tracking-widest uppercase text-[10px] flex items-center gap-2 font-mono">
        <span class="text-base">{{ event.icon || '⚡' }}</span>
        {{ event.name }}
      </h3>
      <span v-if="timeLeft.total <= 0" class="text-[10px] font-mono bg-zinc-800 px-1">DONE</span>
    </div>

    <div class="grid grid-cols-4 gap-0 text-center font-mono relative z-10">
      <div class="flex flex-col">
        <span class="text-2xl font-bold leading-none tabular-nums">{{ timeLeft.days }}</span>
        <span class="text-[9px] uppercase tracking-widest opacity-60">Day</span>
      </div>
      <div class="flex flex-col border-l border-white/10">
        <span class="text-2xl font-bold leading-none tabular-nums">{{ pad(timeLeft.hours) }}</span>
        <span class="text-[9px] uppercase tracking-widest opacity-60">Hrs</span>
      </div>
      <div class="flex flex-col border-l border-white/10">
        <span class="text-2xl font-bold leading-none tabular-nums">{{ pad(timeLeft.minutes) }}</span>
        <span class="text-[9px] uppercase tracking-widest opacity-60">Min</span>
      </div>
      <div class="flex flex-col border-l border-white/10">
        <span class="text-2xl font-bold leading-none tabular-nums">{{ pad(timeLeft.seconds) }}</span>
        <span class="text-[9px] uppercase tracking-widest opacity-60">Sec</span>
      </div>
    </div>
  </div>
</template>