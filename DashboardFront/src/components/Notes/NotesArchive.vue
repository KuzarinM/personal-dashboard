<script setup>
defineProps({
  archivedNotes: Array,
  isLoading: Boolean,
  allowEdit: Boolean
})
defineEmits(['restore', 'delete-forever'])
</script>

<template>
  <div class="h-full bg-zinc-950/50 overflow-y-auto custom-scrollbar p-4">
    <div v-if="isLoading" class="text-zinc-600 text-xs font-mono animate-pulse">
      Loading Archive...
    </div>
    <div v-else-if="archivedNotes.length === 0" class="text-zinc-600 text-xs font-mono text-center py-10">
      Archive is empty
    </div>
    <div v-else class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
      <div v-for="(note, idx) in archivedNotes" :key="note.id" 
           class="bg-zinc-900 border border-zinc-800 p-3 rounded opacity-75 hover:opacity-100 transition flex flex-col gap-2">
        <div class="flex justify-between items-start border-b border-zinc-800 pb-2">
          <span class="font-mono text-xs font-bold text-zinc-400 truncate">{{ note.title }}</span>
          <span class="text-[9px] bg-zinc-800 px-1 rounded text-zinc-500">{{ note.type }}</span>
        </div>
        <div class="flex-1 text-[10px] font-mono text-zinc-600 h-16 overflow-hidden relative">
          {{ note.content.slice(0, 100) }}...
        </div>
        <div class="flex justify-end gap-2 border-t border-zinc-800 pt-2">
          <button v-if="allowEdit" @click="$emit('restore', idx)" 
                  class="text-emerald-500 hover:bg-emerald-900/20 px-2 py-1 rounded text-[10px] font-mono border border-emerald-500/20">
            RESTORE
          </button>
          <button v-if="allowEdit" @click="$emit('delete-forever', idx)" 
                  class="text-red-500 hover:bg-red-900/20 px-2 py-1 rounded text-[10px] font-mono border border-red-500/20">
            DELETE
          </button>
        </div>
      </div>
    </div>
  </div>
</template>