<script setup>
defineProps({
  url: String
})
defineEmits(['close'])
</script>

<template>
  <div class="h-full w-full bg-white flex flex-col animate-in fade-in">
    <div class="bg-zinc-950 border-b border-zinc-800 p-2 flex justify-between items-center text-xs font-mono text-zinc-500">
      <span class="truncate px-2">{{ url }}</span>
      <a :href="url" target="_blank" class="hover:text-emerald-400 px-2 flex items-center gap-1">
        OPEN EXTERNAL ↗
      </a>
    </div>
    <iframe :src="url" class="flex-1 w-full border-none bg-white"></iframe>
  </div>
</template>