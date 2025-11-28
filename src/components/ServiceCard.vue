<script setup>
import { computed } from 'vue'

const props = defineProps({
  item: Object,
  isLocal: Boolean
})

const targetUrl = computed(() => {
  if (props.isLocal && props.item.urlLocal) return props.item.urlLocal
  return props.item.url
})

// Умная иконка
const iconUrl = computed(() => {
  // 1. Если задана явно - используем
  if (props.item.icon) return props.item.icon
  
  // 2. Иначе пытаемся достать авто-иконку через Google
  try {
    const domain = new URL(props.item.url).hostname
    return `https://www.google.com/s2/favicons?domain=${domain}&sz=64`
  } catch (e) {
    // 3. Фолбек, если URL кривой
    return 'https://cdn.jsdelivr.net/gh/walkxcode/dashboard-icons/png/globe.png'
  }
})
</script>

<template>
  <a 
    :href="targetUrl" 
    target="_blank"
    class="relative flex items-center gap-4 p-4 rounded-md bg-zinc-900/80 border border-zinc-800 
           hover:border-emerald-500/50 hover:bg-zinc-900 
           hover:shadow-[0_0_15px_rgba(16,185,129,0.15)] 
           transition-all duration-300 group overflow-hidden"
  >
    <div class="absolute top-0 left-0 w-1 h-1 bg-zinc-700 group-hover:bg-emerald-400 transition-colors"></div>
    <div class="absolute bottom-0 right-0 w-1 h-1 bg-zinc-700 group-hover:bg-emerald-400 transition-colors"></div>

    
    <div class="w-12 h-12 flex-shrink-0 bg-zinc-950 rounded border border-zinc-800 p-2 group-hover:border-emerald-500/30 transition-colors flex items-center justify-center">
      <!-- Добавили @error -->
      <img 
        :src="iconUrl" 
        :alt="item.name" 
        @error="$event.target.src = '/globe.svg'"
        class="max-w-full max-h-full object-contain filter grayscale group-hover:grayscale-0 transition-all duration-300 opacity-80 group-hover:opacity-100" 
      />
    </div>

  

    <div>
      <h3 class="font-bold font-mono text-zinc-200 group-hover:text-emerald-400 transition-colors tracking-wide text-sm">
        {{ item.name }}
      </h3>
      <p class="text-[10px] text-zinc-500 mt-1 font-mono group-hover:text-zinc-400 truncate max-w-[150px]">
        {{ item.desc }}
      </p>
    </div>
  </a>
</template>