<script setup>
import { computed } from 'vue'

const props = defineProps({
  item: Object,
  isLocalUser: Boolean
})

const emit = defineEmits(['open-preview'])

const isResourceLocal = computed(() => {
  const url = props.item.url || ''
  return /(\.lan$)|(\.local$)|(:\/\/192\.168\.)|(:\/\/10\.)|(:\/\/127\.)|(localhost)/.test(url)
})

const isUnreachable = computed(() => !props.isLocalUser && isResourceLocal.value)
const targetUrl = computed(() => (props.isLocalUser && props.item.urlLocal) ? props.item.urlLocal : props.item.url)

console.log(props.item)

const isIconUrl = computed(() => !(props.item.icon &&  props.item.icon.length))
const iconValue = computed(() => {
  if (props.item.icon) return props.item.icon
  try { return `https://www.google.com/s2/favicons?domain=${new URL(props.item.url).hostname}&sz=64` } catch { return '' }
})

// Обработчик ПКМ
const handleRightClick = () => {
  if (!isUnreachable.value && targetUrl.value) {
    emit('open-preview', targetUrl.value)
  }
}
</script>

<template>
  <component
    :is="isUnreachable ? 'div' : 'a'"
    :href="!isUnreachable ? targetUrl : undefined"
    :target="!isUnreachable ? '_blank' : undefined"
    @contextmenu.prevent="handleRightClick" 
    class="relative flex items-center gap-4 p-4 rounded-sm border transition-all duration-300 group overflow-hidden"
    :class="[
      isUnreachable 
         ? 'bg-zinc-950/50 border-zinc-800/50 opacity-60 cursor-not-allowed grayscale' 
         : 'bg-zinc-900/80 border-zinc-800 hover:border-emerald-500/50 hover:bg-zinc-900 hover:shadow-[0_0_15px_rgba(16,185,129,0.15)]'
    ]"
  >
    <!-- Декор (уголки) -->
    <div class="absolute top-0 left-0 w-1 h-1 transition-colors" :class="isUnreachable ? 'bg-zinc-800' : 'bg-zinc-700 group-hover:bg-emerald-400'"></div>
    <div class="absolute bottom-0 right-0 w-1 h-1 transition-colors" :class="isUnreachable ? 'bg-zinc-800' : 'bg-zinc-700 group-hover:bg-emerald-400'"></div>
    
    <!-- Подсказка про ПКМ -->
    <div v-if="!isUnreachable" class="absolute top-1 right-1 opacity-0 group-hover:opacity-100 transition duration-500">
        <div class="bg-zinc-950 border border-zinc-800 text-[8px] text-zinc-500 px-1 rounded font-mono">R-CLICK PREVIEW</div>
    </div>

    <!-- Иконка -->
    <div class="w-10 h-10 flex-shrink-0 bg-zinc-950 rounded border p-1.5 flex items-center justify-center transition-colors"
         :class="isUnreachable ? 'border-zinc-800' : 'border-zinc-800 group-hover:border-emerald-500/30'">
      <img 
          v-if="isIconUrl"
          :src="iconValue" 
          :alt="item.name"
          @error="$event.target.style.display='none'"
          class="max-w-full max-h-full object-contain filter"
          :class="isUnreachable ? 'grayscale opacity-50' : 'grayscale group-hover:grayscale-0 opacity-80 group-hover:opacity-100'"
       />
       <span v-else class="text-zinc-700">{{ iconValue || '#' }}</span>
    </div>
    
    <!-- Текст -->
    <div class="min-w-0">
      <h3 class="font-bold font-mono transition-colors tracking-wide text-xs truncate"
          :class="isUnreachable ? 'text-zinc-500' : 'text-zinc-200 group-hover:text-emerald-400'">
        {{ item.name }}
      </h3>
      <p class="text-[10px] mt-0.5 font-mono truncate text-zinc-500 group-hover:text-zinc-400">
        {{ item.description || item.desc || '...' }}
      </p>
    </div>
  </component>
</template>