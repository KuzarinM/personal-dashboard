<script setup>
import { ref, watch } from 'vue'
import { request } from '@/api'

const props = defineProps({
  isOpen: Boolean,
  dashboardId: Number
})

const emit = defineEmits(['close', 'refresh'])

const query = ref('')
const results = ref([])
const isSearching = ref(false)
const isSaving = ref(false)
const selected = ref(null)

watch(() => props.isOpen, (val) => {
  if (val) {
    query.value = ''
    results.value = []
    selected.value = null
  }
})

const search = async () => {
  if (query.value.length < 2) return
  isSearching.value = true
  try {
    results.value = await request(`/weather/search?q=${encodeURIComponent(query.value)}`)
  } catch (e) { alert('Search failed') } 
  finally { isSearching.value = false }
}

const save = async () => {
  if (!selected.value) return
  isSaving.value = true
  try {
    const payload = {
      latitude: selected.value.latitude,
      longitude: selected.value.longitude,
      cityName: selected.value.name
    }
    await request(`/weather/dashboards/${props.dashboardId}/settings`, {
      method: 'PUT',
      body: JSON.stringify(payload)
    })
    emit('refresh')
    emit('close')
  } catch (e) { alert(e.message) } 
  finally { isSaving.value = false }
}
</script>

<template>
  <div v-if="isOpen" class="fixed inset-0 z-50 flex items-center justify-center p-4">
    <div class="absolute inset-0 bg-black/90 backdrop-blur-sm" @click="$emit('close')"></div>
    <div class="relative bg-zinc-950 border border-cyan-500/30 w-full max-w-md flex flex-col rounded shadow font-sans overflow-hidden">
      
      <div class="p-3 border-b border-zinc-800 bg-zinc-900/50 flex justify-between items-center">
        <h2 class="text-cyan-500 font-mono font-bold tracking-widest text-sm">ATMOSPHERE_CONFIG</h2>
        <button @click="$emit('close')" class="text-zinc-500 hover:text-red-400 text-xs font-mono">[ESC]</button>
      </div>

      <div class="p-6 space-y-4">
        <div class="flex gap-2">
            <input v-model="query" @keydown.enter="search" class="flex-1 bg-zinc-950 border border-zinc-800 p-2 text-xs text-cyan-100 outline-none focus:border-cyan-500 placeholder:text-zinc-700" placeholder="City name...">
            <button @click="search" :disabled="isSearching" class="bg-cyan-900/20 border border-cyan-500/30 text-cyan-400 px-3 text-xs font-mono hover:bg-cyan-500 hover:text-black transition">
                {{ isSearching ? '...' : 'SCAN' }}
            </button>
        </div>

        <div v-if="results.length" class="border border-zinc-800 rounded max-h-60 overflow-y-auto custom-scrollbar">
            <div v-for="(city, idx) in results" :key="idx" 
                 @click="selected = city"
                 class="p-2 cursor-pointer flex justify-between items-center transition"
                 :class="selected?.latitude === city.latitude ? 'bg-cyan-900/30 text-cyan-100' : 'hover:bg-zinc-900 text-zinc-400'">
                <div>
                    <div class="font-bold text-xs">{{ city.name }}</div>
                    <div class="text-[9px] opacity-70">{{ city.country }}</div>
                </div>
                <div class="text-[9px] font-mono opacity-50">{{ city.latitude.toFixed(2) }}</div>
            </div>
        </div>
      </div>

      <div class="p-4 border-t border-zinc-800 bg-zinc-900/30 flex justify-end">
         <button @click="save" :disabled="isSaving" class="px-6 py-2 bg-cyan-900/20 border border-cyan-500/50 text-cyan-400 font-mono text-xs hover:bg-cyan-500 hover:text-black transition flex items-center gap-2">
            <span v-if="isSaving" class="animate-spin">/</span> UPDATE COORDS
         </button>
      </div>
    </div>
  </div>
</template>