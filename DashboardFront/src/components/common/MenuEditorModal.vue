<script setup>
import { ref, watch, computed } from 'vue'
import { request } from '@/api'
import EmojiPicker from '@/components/ui/EmojiPicker.vue'

const props = defineProps({
  isOpen: Boolean,
  dashboardId: Number,
  initialCategories: { type: Array, default: () => [] }
})

const emit = defineEmits(['close', 'refresh'])

const localCategories = ref([])
const selectedCatIndex = ref(0)
const isSaving = ref(false)

// Инициализация
watch(() => props.isOpen, (val) => {
  if (val) {
    // Глубокая копия
    localCategories.value = JSON.parse(JSON.stringify(props.initialCategories))
    if (localCategories.value.length > 0) {
      selectedCatIndex.value = 0
    }
  }
})

const selectedCategory = computed(() => {
  return localCategories.value[selectedCatIndex.value] || null
})

// --- ACTIONS: CATEGORIES ---

const addCategory = () => {
  localCategories.value.push({
    title: 'NEW SECTION',
    items: []
  })
  selectedCatIndex.value = localCategories.value.length - 1
}

const removeCategory = (index) => {
  if (!confirm('Delete category and all its items?')) return
  localCategories.value.splice(index, 1)
  if (selectedCatIndex.value >= localCategories.value.length) {
    selectedCatIndex.value = Math.max(0, localCategories.value.length - 1)
  }
}

const moveCategory = (index, dir) => {
  if (dir === -1 && index === 0) return
  if (dir === 1 && index === localCategories.value.length - 1) return
  
  const temp = localCategories.value[index]
  localCategories.value[index] = localCategories.value[index + dir]
  localCategories.value[index + dir] = temp
  
  if (selectedCatIndex.value === index) selectedCatIndex.value += dir
  else if (selectedCatIndex.value === index + dir) selectedCatIndex.value -= dir
}

// --- ACTIONS: ITEMS ---

const addItem = () => {
  if (!selectedCategory.value) return
  selectedCategory.value.items.push({
    name: 'New Link',
    url: '',
    urlLocal: '',
    desc: '', // Используем desc в UI
    icon: ''
  })
}

const removeItem = (index) => {
  selectedCategory.value.items.splice(index, 1)
}

const moveItem = (index, dir) => {
  const items = selectedCategory.value.items
  if (dir === -1 && index === 0) return
  if (dir === 1 && index === items.length - 1) return
  
  const temp = items[index]
  items[index] = items[index + dir]
  items[index + dir] = temp
}

// --- SAVE ---

const saveMenu = async () => {
  isSaving.value = true
  try {
    // ИСПРАВЛЕНИЕ: Маппинг данных перед отправкой
    // API ждет "description", а в UI мы используем "desc" (так приходит с бека при чтении)
    const payload = {
      categories: localCategories.value.map(cat => ({
        title: cat.title,
        items: cat.items.map(item => ({
          name: item.name,
          url: item.url,
          urlLocal: item.urlLocal,
          description: item.desc, // <--- ВОТ ЗДЕСЬ БЫЛА ОШИБКА. Конвертируем desc -> description
          icon: item.icon
        }))
      }))
    }

    await request(`/content/${props.dashboardId}/structure`, {
      method: 'PUT',
      body: JSON.stringify(payload)
    })
    
    emit('refresh')
  } catch (e) {
    alert('Error saving menu: ' + e.message)
  } finally {
    isSaving.value = false
  }
}
</script>

<template>
  <div v-if="isOpen" class="fixed inset-0 z-50 flex items-center justify-center p-4">
    <div class="absolute inset-0 bg-black/90 backdrop-blur-sm" @click="$emit('close')"></div>

    <div class="relative bg-zinc-950 border border-emerald-500/30 w-full max-w-6xl h-[85vh] flex flex-col rounded shadow-[0_0_30px_rgba(16,185,129,0.1)] overflow-hidden font-sans">
      
      <div class="flex items-center justify-between p-3 border-b border-zinc-800 bg-zinc-900/50">
        <h2 class="text-emerald-500 font-mono font-bold tracking-widest flex items-center gap-2 text-sm">
          <span class="animate-pulse">●</span> MENU_STRUCTURE_EDITOR
        </h2>
        <div class="flex gap-4">
             <button @click="saveMenu" :disabled="isSaving" class="text-emerald-400 hover:text-emerald-200 font-mono text-xs flex items-center gap-2 border border-emerald-500/30 px-3 py-1 rounded hover:bg-emerald-900/20 transition">
                <span v-if="isSaving" class="animate-spin">/</span> [SAVE CHANGES]
             </button>
             <button @click="$emit('close')" class="text-zinc-500 hover:text-red-400 transition font-mono text-xs">[ESC]</button>
        </div>
      </div>

      <div class="flex-1 flex overflow-hidden">
        
        <!-- LEFT COL -->
        <div class="w-1/3 border-r border-zinc-800 bg-zinc-900/20 flex flex-col">
            <div class="p-2 border-b border-zinc-800 bg-zinc-950/50 text-[10px] font-mono text-zinc-500 uppercase tracking-widest">
                Categories
            </div>
            <div class="flex-1 overflow-y-auto p-2 space-y-2 custom-scrollbar">
                <div 
                    v-for="(cat, idx) in localCategories" 
                    :key="idx"
                    @click="selectedCatIndex = idx"
                    class="p-3 border rounded cursor-pointer transition group relative"
                    :class="selectedCatIndex === idx ? 'bg-emerald-900/20 border-emerald-500/50' : 'bg-zinc-900/50 border-zinc-800 hover:border-zinc-600'"
                >
                    <div class="flex items-center gap-2 mb-2">
                        <span class="text-xs font-mono text-zinc-500">#{{ idx + 1 }}</span>
                        <input v-model="cat.title" class="bg-transparent text-sm font-bold text-zinc-200 w-full outline-none focus:text-emerald-400" placeholder="Category Name">
                    </div>
                    
                    <div class="flex justify-end gap-1 opacity-0 group-hover:opacity-100 transition">
                        <button @click.stop="moveCategory(idx, -1)" class="p-1 hover:text-emerald-400 text-zinc-600 text-[10px]" title="Move Up">▲</button>
                        <button @click.stop="moveCategory(idx, 1)" class="p-1 hover:text-emerald-400 text-zinc-600 text-[10px]" title="Move Down">▼</button>
                        <button @click.stop="removeCategory(idx)" class="p-1 hover:text-red-500 text-zinc-600 text-[10px]" title="Delete">✖</button>
                    </div>
                    
                    <div v-if="selectedCatIndex === idx" class="absolute left-0 top-0 bottom-0 w-1 bg-emerald-500"></div>
                </div>

                <button @click="addCategory" class="w-full py-3 border border-zinc-800 border-dashed text-zinc-500 font-mono text-xs hover:text-emerald-400 hover:border-emerald-500/50 transition">
                    [+] NEW CATEGORY
                </button>
            </div>
        </div>

        <!-- RIGHT COL -->
        <div class="w-2/3 bg-zinc-950/30 flex flex-col relative">
            <div class="p-2 border-b border-zinc-800 bg-zinc-950/50 text-[10px] font-mono text-zinc-500 uppercase tracking-widest flex justify-between">
                <span>Items in "{{ selectedCategory?.title || 'None' }}"</span>
                <span>Count: {{ selectedCategory?.items?.length || 0 }}</span>
            </div>

            <div v-if="!selectedCategory" class="flex-1 flex items-center justify-center text-zinc-600 font-mono text-xs">
                SELECT A CATEGORY ON THE LEFT
            </div>

            <div v-else class="flex-1 overflow-y-auto p-4 space-y-3 custom-scrollbar">
                
                <div v-for="(item, idx) in selectedCategory.items" :key="idx" class="bg-zinc-900/40 border border-zinc-800 p-3 rounded hover:border-zinc-700 transition grid grid-cols-12 gap-4 items-start group">
                    
                    <div class="col-span-1 flex flex-col items-center gap-2">
                        <div class="w-8 h-8 bg-zinc-950 border border-zinc-800 rounded flex items-center justify-center overflow-hidden">
                             <img v-if="item.icon && item.icon.startsWith('http')" :src="item.icon" class="w-full h-full object-cover" @error="$event.target.style.display='none'">
                             <span v-else class="text-lg">{{ item.icon || '🔗' }}</span>
                        </div>
                    </div>

                    <div class="col-span-10 grid grid-cols-2 gap-x-4 gap-y-2">
                        <div class="col-span-1">
                             <label class="block text-[9px] text-zinc-600 font-mono uppercase">Name</label>
                             <input v-model="item.name" class="input-cyber" placeholder="My Service">
                        </div>
                        <div class="col-span-1">
                             <label class="block text-[9px] text-zinc-600 font-mono uppercase">Icon (URL or Emoji)</label>
                             <EmojiPicker v-model="item.icon" placeholder="🚀" />
                        </div>

                        <div class="col-span-1">
                             <label class="block text-[9px] text-emerald-500/50 font-mono uppercase">Global URL</label>
                             <input v-model="item.url" class="input-cyber text-emerald-400" placeholder="https://example.com">
                        </div>
                        <div class="col-span-1">
                             <label class="block text-[9px] text-amber-500/50 font-mono uppercase">Local URL (Optional)</label>
                             <input v-model="item.urlLocal" class="input-cyber text-amber-400" placeholder="http://192.168.1.x:8080">
                        </div>

                        <!-- Описание: v-model="item.desc" это правильно, оно используется в инпуте -->
                        <div class="col-span-2">
                             <label class="block text-[9px] text-zinc-600 font-mono uppercase">Description</label>
                             <input v-model="item.desc" class="input-cyber text-zinc-500" placeholder="Short description...">
                        </div>
                    </div>

                    <div class="col-span-1 flex flex-col gap-2 pt-4">
                        <button @click="moveItem(idx, -1)" class="text-zinc-600 hover:text-emerald-400 text-xs">▲</button>
                        <button @click="moveItem(idx, 1)" class="text-zinc-600 hover:text-emerald-400 text-xs">▼</button>
                        <button @click="removeItem(idx)" class="text-zinc-600 hover:text-red-500 text-xs mt-auto">✖</button>
                    </div>
                </div>

                <button @click="addItem" class="w-full py-4 border border-zinc-800 border-dashed text-zinc-500 font-mono text-xs hover:text-emerald-400 hover:border-emerald-500/50 transition">
                    [+] ADD LINK
                </button>
            </div>
        </div>

      </div>
    </div>
  </div>
</template>

<style scoped>
.input-cyber {
    @apply w-full bg-transparent border-b border-zinc-800 text-zinc-300 text-xs py-1 focus:outline-none focus:border-emerald-500 transition font-mono;
}
.custom-scrollbar::-webkit-scrollbar { width: 4px; }
.custom-scrollbar::-webkit-scrollbar-track { background: transparent; }
.custom-scrollbar::-webkit-scrollbar-thumb { background: #3f3f46; border-radius: 2px; }
.custom-scrollbar::-webkit-scrollbar-thumb:hover { background: #10b981; }
</style>