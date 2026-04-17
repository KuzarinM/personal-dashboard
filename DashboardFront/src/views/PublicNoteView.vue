<script setup>
import { ref, onMounted, computed } from 'vue'
import { request } from '@/api'

const props = defineProps(['guid'])
const note = ref(null)
const error = ref('')

// Парсинг чеклиста (если это он)
const checklistItems = computed(() => {
    if (note.value?.type === 'Checklist') {
        try { return JSON.parse(note.value.content) } catch { return [] }
    }
    return []
})

onMounted(async () => {
    try {
        // Здесь используем fetch напрямую, так как request может требовать токен, а мы анонимы
        const res = await fetch(`/api/public/notes/${props.guid}`)
        if (!res.ok) throw new Error('Note not found')
        note.value = await res.json()
    } catch(e) {
        error.value = e.message
    }
})
</script>

<template>
    <div class="min-h-screen bg-zinc-950 text-zinc-300 p-8 font-sans flex justify-center">
        <div v-if="error" class="text-red-500 font-mono">{{ error }}</div>
        
        <div v-else-if="note" class="w-full max-w-3xl bg-zinc-900 border border-zinc-800 rounded p-6 shadow-2xl">
            <h1 class="text-2xl font-bold text-emerald-500 font-mono mb-4 border-b border-zinc-800 pb-2">
                {{ note.title }}
            </h1>

            <!-- TEXT MODE -->
            <div v-if="note.type === 'Text'" class="whitespace-pre-wrap font-mono text-sm leading-6">
                {{ note.content }}
            </div>

            <!-- CHECKLIST MODE -->
            <div v-else-if="note.type === 'Checklist'" class="space-y-2">
                <div v-for="(item, idx) in checklistItems" :key="idx" class="flex items-start gap-3 p-2 border border-zinc-800 rounded bg-zinc-950/50">
                    <div class="w-5 h-5 flex items-center justify-center border border-zinc-600 rounded bg-zinc-900 text-emerald-500">
                        <span v-if="item.done">✓</span>
                    </div>
                    <span :class="item.done ? 'text-zinc-600 line-through' : 'text-zinc-300'">{{ item.text }}</span>
                </div>
            </div>
        </div>
        
        <div v-else class="text-zinc-500 animate-pulse">Loading...</div>
    </div>
</template>