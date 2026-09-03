<script setup>
import { ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { request } from '@/api'

const props = defineProps({
  isOpen: Boolean,
  currentId: Number,
  dashboards: { type: Array, default: () => [] }
})

const emit = defineEmits(['close', 'refresh'])
const router = useRouter()

const newTitle = ref('')
const isCreating = ref(false)

// Очистка инпута при открытии
watch(() => props.isOpen, () => {
  newTitle.value = ''
})

const selectDashboard = (id) => {
  if (id === props.currentId) {
    emit('close')
    return
  }
  router.push(`/${id}`)
  emit('close')
}

const createDashboard = async () => {
  if (!newTitle.value.trim()) return
  isCreating.value = true
  try {
    const res = await request('/dashboards', {
      method: 'POST',
      body: JSON.stringify({ title: newTitle.value })
    })
    
    // Обновляем список (родитель перечитает)
    emit('refresh')
    // Переходим на новый
    router.push(`/${res.id}`)
    emit('close')
  } catch (e) {
    alert(e.message)
  } finally {
    isCreating.value = false
  }
}

const logout = () => {
  if(confirm("Terminate Session?")) {
      localStorage.removeItem('jwt_token')
      router.push('/login')
  }
}
</script>

<template>
  <div v-if="isOpen" class="fixed inset-0 z-50 flex items-center justify-center p-4">
    <div class="absolute inset-0 bg-black/90 backdrop-blur-sm" @click="$emit('close')"></div>

    <div class="relative bg-zinc-950 border border-emerald-500/30 w-full max-w-md flex flex-col rounded shadow-[0_0_30px_rgba(16,185,129,0.1)] overflow-hidden font-sans">
      
      <!-- Header -->
      <div class="p-3 border-b border-zinc-800 bg-zinc-900/50 flex justify-between items-center">
        <h2 class="text-emerald-500 font-mono font-bold tracking-widest text-sm">
          <span class="animate-pulse">■</span> SYSTEM_NAVIGATION
        </h2>
        <button @click="$emit('close')" class="text-zinc-500 hover:text-red-400 text-xs font-mono">[ESC]</button>
      </div>

      <div class="p-6 space-y-6">
        
        <!-- List -->
        <div class="space-y-2">
            <label class="text-[10px] text-zinc-500 font-mono uppercase">Available Systems</label>
            <div class="flex flex-col gap-2 max-h-[40vh] overflow-y-auto custom-scrollbar">
                <div 
                    v-for="dash in dashboards" 
                    :key="dash.id"
                    @click="selectDashboard(dash.id)"
                    class="p-3 border rounded cursor-pointer transition flex items-center justify-between group"
                    :class="dash.id === currentId ? 'bg-emerald-900/20 border-emerald-500/50 text-emerald-100' : 'bg-zinc-900/30 border-zinc-800 hover:border-zinc-600 text-zinc-400'"
                >
                    <span class="font-mono text-sm font-bold">{{ dash.title }}</span>
                    <span v-if="dash.id === currentId" class="text-[10px] text-emerald-500 font-mono bg-emerald-500/10 px-1 rounded">ACTIVE</span>
                    <span v-else class="text-[10px] opacity-0 group-hover:opacity-100 transition text-emerald-400 font-mono">LOAD >></span>
                </div>
            </div>
        </div>

        <!-- Create New -->
        <div class="space-y-2 pt-4 border-t border-zinc-800">
            <label class="text-[10px] text-zinc-500 font-mono uppercase">Initialize New System</label>
            <div class="flex gap-2">
                <input 
                    v-model="newTitle" 
                    @keydown.enter="createDashboard"
                    class="flex-1 bg-zinc-950 border border-zinc-800 p-2 text-xs text-emerald-100 outline-none focus:border-emerald-500 transition font-mono uppercase placeholder:text-zinc-700" 
                    placeholder="DASHBOARD_NAME..."
                >
                <button 
                    @click="createDashboard" 
                    :disabled="isCreating"
                    class="bg-emerald-900/20 border border-emerald-500/30 text-emerald-400 px-4 text-xs font-mono hover:bg-emerald-500 hover:text-black transition"
                >
                    {{ isCreating ? '...' : 'CREATE' }}
                </button>
            </div>
        </div>

      </div>

      <!-- Footer / Logout -->
      <div class="p-3 border-t border-zinc-800 bg-zinc-900/30 flex justify-between items-center">
         <div class="text-[10px] text-zinc-600 font-mono">User ID: {{ currentId }}</div>
         <button @click="logout" class="text-red-500 hover:text-red-400 text-xs font-mono border border-red-900/30 px-3 py-1 rounded hover:bg-red-900/20 transition">
            TERMINATE SESSION
         </button>
      </div>

    </div>
  </div>
</template>

<style scoped>
.custom-scrollbar::-webkit-scrollbar { width: 4px; }
.custom-scrollbar::-webkit-scrollbar-track { background: transparent; }
.custom-scrollbar::-webkit-scrollbar-thumb { background: #3f3f46; border-radius: 2px; }
.custom-scrollbar::-webkit-scrollbar-thumb:hover { background: #10b981; }
</style>