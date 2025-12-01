<script setup>
import { computed } from 'vue'

const props = defineProps({
  item: Object,
  isLocalUser: Boolean
})

// Простая проверка на локальный адрес
const isResourceLocal = computed(() => {
  const url = props.item.url || ''
  // Проверяем .lan, .local, 192.168, 10.x, 127.x, localhost
  return /(\.lan$)|(\.local$)|(:\/\/192\.168\.)|(:\/\/10\.)|(:\/\/127\.)|(:\/\/172\.1[6-9]\.)|(localhost)/.test(url)
})

// Ресурс недоступен? (Мы снаружи, а ресурс внутри)
const isUnreachable = computed(() => {
  return !props.isLocalUser && isResourceLocal.value
})

const targetUrl = computed(() => {
  if (props.isLocalUser && props.item.urlLocal) return props.item.urlLocal
  return props.item.url
})

const iconUrl = computed(() => {
  if (props.item.icon) return props.item.icon
  try {
    const domain = new URL(props.item.url).hostname
    return `https://www.google.com/s2/favicons?domain=${domain}&sz=64`
  } catch (e) {
    return '/globe.svg' // Заглушка
  }
})
</script>

<template>
  <component
    :is="isUnreachable ? 'div' : 'a'"
    :href="!isUnreachable ? targetUrl : undefined"
    :target="!isUnreachable ? '_blank' : undefined"
    class="relative flex items-center gap-4 p-4 rounded-md border transition-all duration-300 group overflow-hidden"
    :class="[
      isUnreachable 
        ? 'bg-zinc-950/50 border-zinc-800/50 opacity-60 cursor-not-allowed grayscale' 
        : 'bg-zinc-900/80 border-zinc-800 hover:border-emerald-500/50 hover:bg-zinc-900 hover:shadow-[0_0_15px_rgba(16,185,129,0.15)]'
    ]"
  >
    <!-- Декор углов -->
    <div class="absolute top-0 left-0 w-1 h-1 transition-colors" :class="isUnreachable ? 'bg-zinc-800' : 'bg-zinc-700 group-hover:bg-emerald-400'"></div>
    <div class="absolute bottom-0 right-0 w-1 h-1 transition-colors" :class="isUnreachable ? 'bg-zinc-800' : 'bg-zinc-700 group-hover:bg-emerald-400'"></div>
    
    <!-- Индикатор локальности (если недоступно) -->
    <div v-if="isUnreachable" class="absolute top-2 right-2 text-[8px] font-mono text-zinc-600 border border-zinc-800 px-1 rounded">
      LOCAL_ONLY
    </div>

    <div class="w-12 h-12 flex-shrink-0 bg-zinc-950 rounded border p-2 flex items-center justify-center transition-colors"
         :class="isUnreachable ? 'border-zinc-800' : 'border-zinc-800 group-hover:border-emerald-500/30'">
      <img 
         :src="iconUrl" 
         :alt="item.name" 
         @error="$event.target.style.display='none'"
         class="max-w-full max-h-full object-contain filter"
         :class="isUnreachable ? 'grayscale opacity-50' : 'grayscale group-hover:grayscale-0 opacity-80 group-hover:opacity-100'"
       />
    </div>
      
    <div>
      <h3 class="font-bold font-mono transition-colors tracking-wide text-sm"
          :class="isUnreachable ? 'text-zinc-500' : 'text-zinc-200 group-hover:text-emerald-400'">
        {{ item.name }}
      </h3>
      <p class="text-[10px] mt-1 font-mono truncate max-w-[150px]"
         :class="isUnreachable ? 'text-zinc-600' : 'text-zinc-500 group-hover:text-zinc-400'">
        {{ item.desc }}
      </p>
    </div>
  </component>
</template>