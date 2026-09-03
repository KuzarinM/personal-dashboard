<script setup>
import { ref } from 'vue'

const props = defineProps({
  notes: Array,
  activeTabId: [String, Number],
  previewUrl: String,
  showArchive: Boolean,
  allowEdit: Boolean
})

const emit = defineEmits([
  'select-links', 'select-preview', 'close-preview', 'select-note', 
  'rename-note', 'toggle-pin', 'archive-note', 'add-note', 
  'toggle-archive-view', 'update:notes'
])

const dragStartIdx = ref(null)

const onDragStart = (e, idx) => { dragStartIdx.value = idx }
const onDrop = (e, dropIdx) => { 
  const newNotes = [...props.notes]
  const item = newNotes.splice(dragStartIdx.value, 1)[0]
  newNotes.splice(dropIdx, 0, item)
  emit('update:notes', newNotes)
}
</script>

<template>
  <div class="flex items-center bg-zinc-950 border-b border-zinc-800 overflow-x-auto scrollbar-hide select-none h-[42px]">
    
    <!-- 1. LINKS TAB -->
    <div @click="$emit('select-links')"
         class="px-4 h-full flex items-center text-[10px] font-mono cursor-pointer border-r border-zinc-800 font-bold transition flex-shrink-0"
         :class="(!showArchive && activeTabId === 'links' && !previewUrl) ? 'text-emerald-400 bg-zinc-900' : 'text-zinc-500 hover:text-zinc-300'">
      LINKS
    </div>

    <!-- 2. PREVIEW TAB -->
    <div v-if="previewUrl"
         @click="$emit('select-preview')"
         class="px-4 h-full flex items-center text-[10px] font-mono cursor-pointer border-r border-zinc-800 font-bold transition flex-shrink-0 bg-zinc-900 text-emerald-400 relative group animate-in fade-in slide-in-from-left-2 border-b-2 border-emerald-500">
      <span class="mr-2">👁 PREVIEW</span>
      <button @click.stop="$emit('close-preview')" class="hover:text-red-500 font-bold">×</button>
    </div>
    
    <!-- 3. NOTES TABS -->
    <template v-if="!showArchive">
      <div v-for="(note, idx) in notes" :key="note.id" 
           draggable="true" @dragstart="onDragStart($event, idx)" @drop="onDrop($event, idx)" @dragover.prevent
           @click="$emit('select-note', note.id)" @dblclick="$emit('rename-note', note)"
           class="group relative px-4 h-full flex items-center text-[10px] font-mono cursor-pointer border-r border-zinc-800 max-w-[140px] transition pr-8 flex-shrink-0"
           :class="[ activeTabId === note.id && !previewUrl ? 'text-emerald-100 bg-zinc-900' : 'text-zinc-500 hover:bg-zinc-900/50', note.pinned ? 'border-l-2 border-l-amber-500/50 pl-3' : '' ]">
        <span class="truncate block w-full">
          <span v-if="note.type === 'Checklist'" class="text-blue-400 mr-1">☑</span>
          {{ note.title }}
        </span>
        <div class="absolute right-1 top-0 h-full flex items-center gap-1 opacity-0 group-hover:opacity-100 transition px-1 bg-zinc-900/80 backdrop-blur">
          <button @click.stop="$emit('toggle-pin', note)" class="hover:text-amber-400 p-1 transition transform hover:scale-110" title="Pin/Unpin">
            {{ note.pinned ? '★' : '☆' }}
          </button>
          <button v-if="allowEdit" @click.stop="$emit('archive-note', idx)" class="hover:text-sky-400 p-1 font-bold" title="Archive">↓</button>
        </div>
      </div>
      <button v-if="allowEdit" @click="$emit('add-note')" class="px-3 h-full text-zinc-600 hover:text-emerald-400 border-r border-zinc-800 hover:bg-zinc-900 transition font-mono text-xs flex-shrink-0">+</button>
    </template>

    <div class="flex-1"></div>

    <!-- ARCHIVE TOGGLE -->
    <button @click="$emit('toggle-archive-view')" 
            class="px-3 h-full border-l border-zinc-800 transition flex items-center justify-center gap-2 font-mono text-[10px]"
            :class="showArchive ? 'bg-zinc-800 text-sky-400' : 'text-zinc-600 hover:text-sky-400 hover:bg-zinc-900'">
      <span class="text-sm">📦</span>
      <span v-if="showArchive">ARCHIVE MODE</span>
    </button>
  </div>
</template>