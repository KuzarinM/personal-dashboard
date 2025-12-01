<script setup>
import { ref, computed, onMounted, onUnmounted } from 'vue'

const props = defineProps({
  event: Object,
  // Настройки срочности (в мс), по умолчанию: 24 часа и 7 дней
  urgencySettings: {
    type: Object,
    default: () => ({ critical: 86400000, warning: 604800000 })
  }
})

const timeLeft = ref({ days: 0, hours: 0, minutes: 0, seconds: 0, total: 0 })
let timerInterval = null

// --- РАСЧЕТ ВРЕМЕНИ ---
const calculateTime = () => {
  // Проверяем наличие даты. Parent (DashboardView) должен был нормализовать её в поле .date
  if (!props.event.date) return

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

// --- ЛОГИКА ЦВЕТОВ ---
const urgencyColor = computed(() => {
  const t = timeLeft.value.total
  // Если время вышло - серый
  if (t <= 0) return 'text-zinc-500 border-zinc-800 bg-zinc-900' 
  
  // Критический уровень (Красный)
  if (t < props.urgencySettings.critical) 
    return 'text-red-500 border-red-500/50 bg-red-900/10 shadow-[0_0_15px_rgba(239,68,68,0.2)] animate-pulse'
  
  // Предупреждение (Желтый/Оранжевый)
  if (t < props.urgencySettings.warning) 
    return 'text-amber-400 border-amber-500/40 bg-amber-900/10'
  
  // Норма (Изумрудный)
  return 'text-emerald-400 border-emerald-500/30 bg-emerald-900/10 shadow-[0_0_10px_rgba(16,185,129,0.1)]'
})

// --- ЛОГИКА ССЫЛКИ (Умный поиск) ---
const eventLink = computed(() => {
    // Собираем массив кандидатов, где может прятаться ссылка
    const candidates = [
        props.event.url,
        props.event.URL,
        props.event.location,
        props.event.LOCATION,
        props.event.description // Иногда ссылки прячут в описании
    ]

    for (const text of candidates) {
        // Пропускаем пустые поля
        if (!text || typeof text !== 'string') continue
        
        // Если нашли подстроку http/https
        if (text.includes('http://') || text.includes('https://')) {
            // Регулярное выражение для извлечения чистого URL
            // Ищет http(s):// до ближайшего пробела, кавычки или скобки
            const match = text.match(/(https?:\/\/[^\s\)"']+)/)
            if (match) return match[0]
        }
    }
    return null
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
    <!-- Техническая подложка (сетка) -->
    <div class="absolute inset-0 opacity-10 pointer-events-none" 
         style="background-image: linear-gradient(0deg, transparent 24%, rgba(255, 255, 255, .3) 25%, rgba(255, 255, 255, .3) 26%, transparent 27%, transparent 74%, rgba(255, 255, 255, .3) 75%, rgba(255, 255, 255, .3) 76%, transparent 77%, transparent), linear-gradient(90deg, transparent 24%, rgba(255, 255, 255, .3) 25%, rgba(255, 255, 255, .3) 26%, transparent 27%, transparent 74%, rgba(255, 255, 255, .3) 75%, rgba(255, 255, 255, .3) 76%, transparent 77%, transparent); background-size: 30px 30px;">
    </div>

    <!-- Заголовок -->
    <div class="flex justify-between items-center mb-3 relative z-10">
      <h3 class="font-bold tracking-widest uppercase text-[10px] flex items-center gap-2 font-mono">
        <!-- Иконка -->
        <span class="text-base">{{ event.icon || '⚡' }}</span>
        
        <!-- ВАРИАНТ 1: Если найдена ссылка, рендерим <a> -->
        <a 
          v-if="eventLink" 
          :href="eventLink" 
          target="_blank" 
          class="hover:underline decoration-dashed decoration-current underline-offset-4 cursor-pointer flex items-center gap-1 transition-opacity hover:opacity-80"
          title="Открыть ссылку"
        >
          {{ event.name }}
          <!-- Мелкая стрелочка, указывающая на внешнюю ссылку -->
          <span class="text-[8px] opacity-70">↗</span>
        </a>
        
        <!-- ВАРИАНТ 2: Просто текст -->
        <span v-else>
            {{ event.name }}
        </span>
      </h3>
      
      <!-- Бейдж DONE если время вышло -->
      <span v-if="timeLeft.total <= 0" class="text-[10px] font-mono bg-zinc-800 px-1">DONE</span>
    </div>

    <!-- Цифры таймера -->
    <div class="grid grid-cols-4 gap-0 text-center font-mono relative z-10">
      <!-- DAYS -->
      <div class="flex flex-col">
        <span class="text-2xl font-bold leading-none tabular-nums">{{ timeLeft.days }}</span>
        <span class="text-[9px] uppercase tracking-widest opacity-60">Day</span>
      </div>
      <!-- HOURS -->
      <div class="flex flex-col border-l border-white/10">
        <span class="text-2xl font-bold leading-none tabular-nums">{{ pad(timeLeft.hours) }}</span>
        <span class="text-[9px] uppercase tracking-widest opacity-60">Hrs</span>
      </div>
      <!-- MINUTES -->
      <div class="flex flex-col border-l border-white/10">
        <span class="text-2xl font-bold leading-none tabular-nums">{{ pad(timeLeft.minutes) }}</span>
        <span class="text-[9px] uppercase tracking-widest opacity-60">Min</span>
      </div>
      <!-- SECONDS -->
      <div class="flex flex-col border-l border-white/10">
        <span class="text-2xl font-bold leading-none tabular-nums">{{ pad(timeLeft.seconds) }}</span>
        <span class="text-[9px] uppercase tracking-widest opacity-60">Sec</span>
      </div>
    </div>
  </div>
</template>