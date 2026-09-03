<script setup>
import { ref, computed, onMounted, onUnmounted, watch } from 'vue'

const props = defineProps({
  event: {
    type: Object,
    required: true
  },
  // Настройки приходят с бэкенда в МИНУТАХ
  urgencySettings: {
    type: Object,
    default: () => null // Обработаем null внутри
  }
})

const timeLeft = ref({ days: 0, hours: 0, minutes: 0, seconds: 0, total: 0 })
let timerInterval = null

// --- 1. ЛОГИКА РАСЧЕТА ВРЕМЕНИ ---
const calculateTime = () => {
  if (!props.event || !props.event.date) return

  // Приводим дату к Timestamp. 
  // Бэкенд теперь отдает UTC (Z на конце), new Date() это прекрасно понимает
  // и переводит в локальное время пользователя.
  const target = new Date(props.event.date).getTime()
  const now = Date.now()
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

// --- 2. ЛОГИКА ЦВЕТОВ (URGENCY) ---
const urgencyColor = computed(() => {
  const t = timeLeft.value.total
  
  // Если время вышло -> Серый (DONE)
  if (t <= 0) return 'text-zinc-500 border-zinc-800 bg-zinc-900'

  // Дефолтные настройки (в минутах), если пропс не передан
  const settings = props.urgencySettings || { critical: 1440, warning: 10080 }
  
  // Конвертируем минуты в миллисекунды
  const critMs = (settings.critical || 1440) * 60 * 1000
  const warnMs = (settings.warning || 10080) * 60 * 1000

  // Критический (Красный)
  if (t < critMs) 
    return 'text-red-500 border-red-500/50 bg-red-900/10 shadow-[0_0_15px_rgba(239,68,68,0.2)] animate-pulse'
  
  // Предупреждение (Желтый)
  if (t < warnMs) 
    return 'text-amber-400 border-amber-500/40 bg-amber-900/10'
  
  // Норма (Изумрудный)
  return 'text-emerald-400 border-emerald-500/30 bg-emerald-900/10 shadow-[0_0_10px_rgba(16,185,129,0.1)]'
})

// --- 3. ИЗВЛЕЧЕНИЕ ССЫЛОК (SMART REGEX) ---
const eventLink = computed(() => {
    if (!props.event) return null

    // Собираем массив полей, где может быть ссылка
    const candidates = [
        props.event.url,
        props.event.URL,
        props.event.location,
        props.event.description // Google Calendar часто кладет ссылки сюда
    ]

    for (const text of candidates) {
        if (!text || typeof text !== 'string') continue
        
        // A. Если описание содержит HTML (например <a href="...">)
        // Ищем содержимое href
        const hrefMatch = text.match(/href=["'](https?:\/\/[^"']+)["']/i)
        if (hrefMatch) return hrefMatch[1]

        // B. Если это просто текст
        // Ищем http/https до пробела, кавычки или скобки
        const rawMatch = text.match(/(https?:\/\/[^\s<>"';)]+)/i)
        if (rawMatch) return rawMatch[0]
    }
    return null
})

const pad = (num) => num.toString().padStart(2, '0')

// Запуск
onMounted(() => {
  calculateTime()
  timerInterval = setInterval(calculateTime, 1000)
})

onUnmounted(() => clearInterval(timerInterval))

// Если событие изменилось динамически (например при обновлении данных)
watch(() => props.event, calculateTime, { deep: true })
</script>

<template>
  <div 
    v-if="event"
    class="relative rounded-sm border p-4 transition-all duration-500 overflow-hidden group min-h-[90px]"
    :class="urgencyColor"
  >
    <!-- Фоновая сетка (декор) -->
    <div class="absolute inset-0 opacity-10 pointer-events-none"
          style="background-image: linear-gradient(0deg, transparent 24%, rgba(255, 255, 255, .3) 25%, rgba(255, 255, 255, .3) 26%, transparent 27%, transparent 74%, rgba(255, 255, 255, .3) 75%, rgba(255, 255, 255, .3) 76%, transparent 77%, transparent), linear-gradient(90deg, transparent 24%, rgba(255, 255, 255, .3) 25%, rgba(255, 255, 255, .3) 26%, transparent 27%, transparent 74%, rgba(255, 255, 255, .3) 75%, rgba(255, 255, 255, .3) 76%, transparent 77%, transparent); background-size: 30px 30px;">
    </div>

    <!-- Заголовок -->
    <div class="flex justify-between items-center mb-3 relative z-10">
      <h3 class="font-bold tracking-widest uppercase text-[10px] flex items-center gap-2 font-mono truncate w-full">
        <!-- Иконка -->
        <span class="text-base flex-shrink-0">{{ event.icon || '⚡' }}</span>
        
        <!-- Название (ссылка или текст) -->
        <a 
           v-if="eventLink" 
           :href="eventLink" 
           target="_blank" 
           class="hover:underline decoration-dashed decoration-current underline-offset-4 cursor-pointer flex items-center gap-1 transition-opacity hover:opacity-80 truncate"
           title="Open Link"
        >
          {{ event.name }}
          <span class="text-[8px] opacity-70">↗</span>
        </a>
        <span v-else class="truncate">
            {{ event.name }}
        </span>
      </h3>
      
      <!-- Бейдж DONE -->
      <span v-if="timeLeft.total <= 0" class="text-[10px] font-mono bg-zinc-800 px-1 ml-2 flex-shrink-0">DONE</span>
    </div>

    <!-- Циферблат -->
    <div class="grid grid-cols-4 gap-0 text-center font-mono relative z-10">
      <!-- DAYS -->
      <div class="flex flex-col">
        <span class="text-2xl font-bold leading-none tabular-nums">{{ timeLeft.days }}</span>
        <span class="text-[9px] uppercase tracking-widest opacity-60">Day</span>
      </div>
      <!-- HOURS -->
      <div class="flex flex-col border-l border-current border-opacity-20">
        <span class="text-2xl font-bold leading-none tabular-nums">{{ pad(timeLeft.hours) }}</span>
        <span class="text-[9px] uppercase tracking-widest opacity-60">Hrs</span>
      </div>
      <!-- MINUTES -->
      <div class="flex flex-col border-l border-current border-opacity-20">
        <span class="text-2xl font-bold leading-none tabular-nums">{{ pad(timeLeft.minutes) }}</span>
        <span class="text-[9px] uppercase tracking-widest opacity-60">Min</span>
      </div>
      <!-- SECONDS -->
      <div class="flex flex-col border-l border-current border-opacity-20">
        <span class="text-2xl font-bold leading-none tabular-nums">{{ pad(timeLeft.seconds) }}</span>
        <span class="text-[9px] uppercase tracking-widest opacity-60">Sec</span>
      </div>
    </div>
  </div>
</template>