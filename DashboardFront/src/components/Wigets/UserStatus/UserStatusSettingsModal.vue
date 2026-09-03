<script setup>
import { ref, watch } from 'vue'
import { request } from '@/api'
import EmojiPicker from '@/components/ui/EmojiPicker.vue'

const props = defineProps({
  isOpen: Boolean,
  dashboardId: Number
})

const emit = defineEmits(['close', 'refresh'])
const activeTab = ref('status') // 'status' | 'watchlist'

// Tab 1: My Status
const myStatus = ref({ statusText: '', statusEmoji: '🟢', statusMessage: '', statusColor: 'emerald' })
const isSavingStatus = ref(false)

// Tab 2: Watchlist
const extraUsers = ref([])
const newUser = ref('')
const isSavingList = ref(false)

watch(() => props.isOpen, (val) => {
  if (val) {
      loadMyStatus()
      loadWatchlist()
  }
})

// --- MY STATUS ---
const loadMyStatus = async () => {
    try { myStatus.value = await request('/users/status/me') } catch(e){}
}
const saveMyStatus = async () => {
    isSavingStatus.value = true
    try {
        await request('/users/status/me', { method: 'PUT', body: JSON.stringify(myStatus.value) })
        emit('refresh')
        emit('close')
    } catch(e) { alert(e.message) }
    finally { isSavingStatus.value = false }
}
const colors = ['emerald', 'amber', 'red', 'blue', 'purple', 'zinc']

// --- WATCHLIST ---
const loadWatchlist = async () => {
    try {
        const res = await request(`/users/status/settings/${props.dashboardId}`)
        // FIX: Читаем оба варианта
        extraUsers.value = res.extraUsers || res.ExtraUsers || []
    } catch(e) {}
}
const addExtraUser = () => {
    if (newUser.value && !extraUsers.value.includes(newUser.value)) {
        extraUsers.value.push(newUser.value)
        newUser.value = ''
    }
}
const removeExtraUser = (u) => {
    extraUsers.value = extraUsers.value.filter(x => x !== u)
}
const saveWatchlist = async () => {
    isSavingList.value = true
    try {
        await request(`/users/status/settings/${props.dashboardId}`, {
            method: 'POST',
            body: JSON.stringify({ extraUsers: extraUsers.value })
        })
        emit('refresh')
    } catch(e) { alert(e.message) }
    finally { isSavingList.value = false }
}
</script>

<template>
  <div v-if="isOpen" class="fixed inset-0 z-50 flex items-center justify-center p-4">
    <div class="absolute inset-0 bg-black/90 backdrop-blur-sm" @click="$emit('close')"></div>
    <div class="relative bg-zinc-950 border border-blue-500/30 w-full max-w-md flex flex-col rounded shadow font-sans overflow-hidden">
      
      <!-- Tabs Header -->
      <div class="flex border-b border-zinc-800 bg-zinc-900/50">
          <button @click="activeTab = 'status'" class="flex-1 py-3 text-xs font-mono font-bold transition border-b-2" :class="activeTab === 'status' ? 'text-blue-400 border-blue-500 bg-zinc-900' : 'text-zinc-500 border-transparent hover:text-zinc-300'">
              MY STATUS
          </button>
          <button @click="activeTab = 'watchlist'" class="flex-1 py-3 text-xs font-mono font-bold transition border-b-2" :class="activeTab === 'watchlist' ? 'text-blue-400 border-blue-500 bg-zinc-900' : 'text-zinc-500 border-transparent hover:text-zinc-300'">
              WATCHLIST
          </button>
      </div>

      <div class="p-6 space-y-6">
          
          <!-- TAB 1: STATUS -->
          <div v-if="activeTab === 'status'" class="space-y-4">
              <div class="flex gap-2">
                  <EmojiPicker v-model="myStatus.statusEmoji" placeholder="🟢" />
                  <input v-model="myStatus.statusText" class="flex-1 bg-zinc-950 border border-zinc-800 p-2 text-xs font-mono text-zinc-200 outline-none focus:border-blue-500" placeholder="Short Status (e.g. Coding)">
              </div>
              
              <div class="space-y-1">
                  <label class="text-[9px] text-zinc-500 font-mono uppercase">Status Color</label>
                  <div class="flex gap-2">
                      <div v-for="c in colors" :key="c" @click="myStatus.statusColor = c" 
                           class="w-6 h-6 rounded cursor-pointer border-2 transition"
                           :class="[
                               `bg-${c}-500`,
                               myStatus.statusColor === c ? 'border-white scale-110' : 'border-transparent opacity-50 hover:opacity-100'
                           ]"
                      ></div>
                  </div>
              </div>

              <div>
                  <textarea v-model="myStatus.statusMessage" class="w-full bg-zinc-950 border border-zinc-800 p-2 text-xs font-mono text-zinc-400 outline-none focus:border-blue-500 h-20 resize-none" placeholder="Long description (visible on hover)..."></textarea>
              </div>

              <button @click="saveMyStatus" :disabled="isSavingStatus" class="w-full py-2 bg-blue-900/20 border border-blue-500/50 text-blue-400 font-mono text-xs hover:bg-blue-500 hover:text-black transition">
                  {{ isSavingStatus ? 'SAVING...' : 'UPDATE STATUS' }}
              </button>
          </div>

          <!-- TAB 2: WATCHLIST -->
          <div v-if="activeTab === 'watchlist'" class="space-y-4">
              <div class="text-[10px] text-zinc-500 font-mono">
                  Team members are added automatically. Add extra users here (by username).
              </div>
              <div class="flex gap-2">
                  <input v-model="newUser" @keydown.enter="addExtraUser" class="flex-1 bg-zinc-950 border border-zinc-800 p-2 text-xs font-mono text-zinc-200 outline-none focus:border-blue-500" placeholder="Username...">
                  <button @click="addExtraUser" class="bg-zinc-900 border border-zinc-700 px-3 text-zinc-400 hover:text-blue-400 text-xs">+</button>
              </div>
              
              <div class="flex flex-wrap gap-2 max-h-40 overflow-y-auto">
                  <div v-for="u in extraUsers" :key="u" class="bg-zinc-900 border border-zinc-800 px-2 py-1 rounded flex items-center gap-2">
                      <span class="text-xs font-mono text-zinc-300">{{ u }}</span>
                      <button @click="removeExtraUser(u)" class="text-zinc-600 hover:text-red-500 text-[10px] font-bold">×</button>
                  </div>
              </div>

              <button @click="saveWatchlist" :disabled="isSavingList" class="w-full py-2 bg-zinc-900/50 border border-zinc-700 text-zinc-400 font-mono text-xs hover:text-white transition">
                  {{ isSavingList ? 'SAVING...' : 'SAVE LIST' }}
              </button>
          </div>

      </div>
    </div>
  </div>
</template>