<script setup>
import { ref } from 'vue'

const props = defineProps({
  title: String,
  initialX: { type: Number, default: 100 },
  initialY: { type: Number, default: 100 },
  widthClass: { type: String, default: 'w-80' } // <-- Новый проп для гибкости размеров
})

const emit = defineEmits(['close'])

const position = ref({ x: props.initialX, y: props.initialY })
const isDragging = ref(false)
const dragOffset = ref({ x: 0, y: 0 })
const zIndex = ref(50)

const startDrag = (e) => {
    isDragging.value = true
    dragOffset.value = { x: e.clientX - position.value.x, y: e.clientY - position.value.y }
    window.addEventListener('mousemove', onDrag)
    window.addEventListener('mouseup', stopDrag)
}

const onDrag = (e) => {
    if (!isDragging.value) return
    position.value = { x: e.clientX - dragOffset.value.x, y: e.clientY - dragOffset.value.y }
}

const stopDrag = () => {
    isDragging.value = false
    window.removeEventListener('mousemove', onDrag)
    window.removeEventListener('mouseup', stopDrag)
}
</script>

<template>
  <div 
    class="fixed bg-zinc-950 border border-emerald-500/30 shadow-[0_0_50px_rgba(0,0,0,0.5)] rounded-sm overflow-hidden flex flex-col px-1"
    :class="widthClass"
    :style="{ top: position.y + 'px', left: position.x + 'px', zIndex: zIndex }"
  >
    <!-- Header (Draggable) -->
    <div 
        @mousedown="startDrag"
        class="flex justify-between items-center p-2 border-b border-zinc-900 bg-zinc-900 cursor-move select-none active:bg-zinc-800 hover:text-emerald-400 transition-colors"
    >
        <span class="text-[10px] font-mono font-bold tracking-widest flex items-center gap-2">
            <span class="w-2 h-2 bg-emerald-500 rounded-full"></span>
            {{ title }}
        </span>
        <button @mousedown.stop @click="$emit('close')" class="text-zinc-600 hover:text-red-500 font-mono text-xs px-1 outline-none">×</button>
    </div>
    
    <!-- Content -->
    <div class="bg-zinc-950 max-h-[65vh] overflow-y-auto custom-scrollbar">
        <slot></slot>
    </div>
  </div>
</template>

<style scoped>
.custom-scrollbar::-webkit-scrollbar { width: 4px; }
.custom-scrollbar::-webkit-scrollbar-track { background: transparent; }
.custom-scrollbar::-webkit-scrollbar-thumb { background: #3f3f46; border-radius: 2px; }
</style>