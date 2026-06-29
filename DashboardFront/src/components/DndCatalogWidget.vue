<script setup>
import { ref, computed, onMounted, onBeforeUnmount } from 'vue'
import { request } from '@/api'
import { useSignalR } from '@/composables/useSignalR'

const catalog = ref({ items: [], spells: [] })
const activeTab = ref('items')
const searchQuery = ref('')
const loading = ref(true)

const { on, off } = useSignalR()

const editingItemIndex = ref(null)
const editingSpellIndex = ref(null)

const loadCatalog = async () => {
  loading.value = true
  try {
    const parsed = await request('/dnd/catalog')
    if (parsed) {
      // Инициализируем строковое представление тегов для редактора
      parsed.items = (parsed.items || []).map(i => ({
        ...i,
        tagsRaw: i.tags ? i.tags.join(', ') : ''
      }))
      catalog.value = parsed
    }
  } catch (e) {
    console.error('Failed parsing catalog', e)
  } finally {
    loading.value = false
  }
}

const saveCatalog = async () => {
  try {
    await request('/dnd/catalog', {
      method: 'PUT',
      body: JSON.stringify(catalog.value)
    })
  } catch (e) {
    console.error('Failed saving catalog to server', e)
  }
}

const handleCatalogSocketUpdate = () => {
  loadCatalog()
}

onMounted(() => {
  loadCatalog()
  on('dnd_catalog', handleCatalogSocketUpdate) // Подписка на SignalR-события изменений каталога
})

onBeforeUnmount(() => {
  off('dnd_catalog', handleCatalogSocketUpdate)
})

const filteredItems = computed(() => {
  const q = searchQuery.value.toLowerCase().trim()
  if (!q) return catalog.value.items || []
  return (catalog.value.items || []).filter(i => {
    return i.name.toLowerCase().includes(q) || 
           (i.desc && i.desc.toLowerCase().includes(q)) ||
           (i.tags && i.tags.some(t => t.toLowerCase().includes(q)))
  })
})

const filteredSpells = computed(() => {
  const q = searchQuery.value.toLowerCase().trim()
  if (!q) return catalog.value.spells || []
  return (catalog.value.spells || []).filter(s => {
    return s.name.toLowerCase().includes(q) || 
           (s.desc && s.desc.toLowerCase().includes(q))
  })
})

const startAddCategoryItem = () => {
  if (!catalog.value.items) catalog.value.items = []
  catalog.value.items.unshift({ name: 'Новый предмет', tags: ['снаряжение'], tagsRaw: 'снаряжение', url: '', desc: '' })
  editingItemIndex.value = 0
  activeTab.value = 'items'
}

const startAddCategorySpell = () => {
  if (!catalog.value.spells) catalog.value.spells = []
  catalog.value.spells.unshift({ name: 'Новое заклинание', level: 1, isRitual: false, url: '', desc: '' })
  editingSpellIndex.value = 0
  activeTab.value = 'spells'
}

const removeItem = (idx) => {
  catalog.value.items.splice(idx, 1)
  saveCatalog()
}

const removeSpell = (idx) => {
  catalog.value.spells.splice(idx, 1)
  saveCatalog()
}

const saveEdit = () => {
  if (editingItemIndex.value !== null) {
    const item = catalog.value.items[editingItemIndex.value]
    // Конвертируем строку тегов обратно в массив
    item.tags = item.tagsRaw ? item.tagsRaw.split(',').map(t => t.trim().toLowerCase()).filter(Boolean) : []
  }
  editingItemIndex.value = null
  editingSpellIndex.value = null
  saveCatalog()
}
</script>

<template>
  <div class="bg-zinc-900/50 border border-zinc-800 rounded-sm overflow-hidden flex flex-col relative group/catalog min-h-[500px]">
    
    <!-- Header -->
    <div class="flex items-center justify-between px-3 py-1.5 bg-zinc-950 border-b border-zinc-900">
      <span class="text-[10px] text-emerald-500 font-mono font-bold tracking-widest uppercase flex items-center gap-1.5">
        <span class="w-1.5 h-1.5 bg-emerald-500 rounded-full animate-pulse"></span>
        SHARED_CATALOG
      </span>
      <div class="flex gap-2 text-[9px] font-mono" v-if="!loading">
        <button @click="startAddCategoryItem" class="text-emerald-500 hover:text-emerald-400 transition outline-none">[+ПРЕДМЕТ]</button>
        <button @click="startAddCategorySpell" class="text-blue-500 hover:text-blue-400 transition outline-none">[+МАГИЯ]</button>
      </div>
    </div>

    <!-- Заглушка Загрузки -->
    <div v-if="loading" class="flex-1 flex flex-col items-center justify-center p-6 text-emerald-500 font-mono text-xs animate-pulse min-h-[400px]">
      ЗАГРУЗКА БАЗЫ КАТАЛОГА...
    </div>

    <!-- Заглушка Пустого состояния -->
    <div v-else-if="(!catalog.items || catalog.items.length === 0) && (!catalog.spells || catalog.spells.length === 0)" 
         class="flex-1 flex flex-col items-center justify-center p-6 text-zinc-500 text-xs font-mono text-center gap-4 min-h-[400px]">
      <span class="text-4xl">📚</span>
      <span class="uppercase tracking-widest text-[10px] font-bold text-zinc-400">Каталог пуст</span>
      <p class="text-[9px] text-zinc-600 max-w-[200px] leading-normal uppercase">База данных не содержит шаблонов. Добавьте первый предмет или заклинание кнопками сверху.</p>
    </div>

    <template v-else>
      <!-- Вкладки -->
      <div class="flex border-b border-zinc-900 text-[9px] font-mono bg-zinc-950/40 select-none">
        <button @click="activeTab = 'items'" :class="activeTab === 'items' ? 'text-emerald-400 bg-zinc-900/50' : 'text-zinc-500 hover:text-zinc-300'" class="flex-1 py-1.5 text-center transition outline-none">ПРЕДМЕТЫ</button>
        <button @click="activeTab = 'spells'" :class="activeTab === 'spells' ? 'text-emerald-400 bg-zinc-900/50' : 'text-zinc-500 hover:text-zinc-300'" class="flex-1 py-1.5 text-center transition outline-none">ЗАКЛИНАНИЯ</button>
      </div>

      <!-- Тело виджета -->
      <div class="p-3 text-sm font-mono flex-1 flex flex-col min-h-0">
        
        <!-- Поиск -->
        <div class="relative flex items-center bg-zinc-950 border border-zinc-800 rounded px-2 focus-within:border-emerald-500/50 transition mb-3">
          <span class="text-zinc-600 text-[10px] mr-1.5">🔍</span>
          <input v-model="searchQuery" placeholder="ПОИСК В ГЛОБАЛЬНОЙ БАЗЕ..." class="w-full bg-transparent text-[10px] py-1.5 focus:outline-none placeholder:text-zinc-700 font-mono">
        </div>

        <!-- СПИСОК ПРЕДМЕТОВ -->
        <!-- ОШИБКА ИСПРАВЛЕНА: Атрибут v-slot удален с тега div -->
        <div v-if="activeTab === 'items'" class="overflow-y-auto custom-scrollbar flex-1 space-y-2 pr-1 max-h-[400px]">
          <div v-for="(item, idx) in filteredItems" :key="idx" class="p-2.5 bg-zinc-950/20 border border-zinc-800 rounded-sm flex flex-col gap-1 relative">
            
            <div v-if="editingItemIndex !== idx" class="flex justify-between items-start">
              <div class="truncate">
                <span class="font-bold text-zinc-200">{{ item.name }}</span>
              </div>
              <div class="flex items-center gap-2">
                <button @click="editingItemIndex = idx" class="text-[8px] text-zinc-500 hover:text-emerald-400 outline-none">[ИЗМЕН.]</button>
                <button @click="removeItem(idx)" class="text-[9px] text-zinc-600 hover:text-red-500 font-bold outline-none">×</button>
              </div>
            </div>

            <!-- Форма редактирования inline -->
            <div v-else class="space-y-1.5 pt-1 border-t border-zinc-900 mt-1">
              <input v-model="item.name" class="input-inline" placeholder="Название">
              <input v-model="item.tagsRaw" class="input-inline" placeholder="Теги через запятую (магия, броня)">
              <input v-model="item.url" class="input-inline text-emerald-500/80" placeholder="Ссылка на Wiki">
              <input v-model="item.desc" class="input-inline text-zinc-400" placeholder="Краткое описание">
              <div class="flex gap-2">
                <button @click="saveEdit" class="flex-1 bg-emerald-950/40 border border-emerald-800 text-emerald-400 text-[9px] py-1 rounded-sm hover:bg-emerald-900/30">ОК</button>
              </div>
            </div>

            <p v-if="editingItemIndex !== idx && item.desc" class="text-[8px] text-zinc-500 leading-tight">{{ item.desc }}</p>
            <div v-if="editingItemIndex !== idx && item.tags && item.tags.length" class="flex flex-wrap gap-1 mt-0.5">
              <span v-for="tag in item.tags" :key="tag" class="text-[7px] text-zinc-500 bg-zinc-900 border border-zinc-800 px-1 rounded-sm">#{{ tag }}</span>
            </div>
          </div>
          <div v-if="filteredItems.length === 0" class="text-center py-4 text-zinc-600 text-[9px] italic border border-dashed border-zinc-900/40 rounded">Нет совпадений</div>
        </div>

        <!-- СПИСОК ЗАКЛИНАНИЙ -->
        <!-- ОШИБКА ИСПРАВЛЕНА: Атрибут v-slot удален с тега div -->
        <div v-if="activeTab === 'spells'" class="overflow-y-auto custom-scrollbar flex-1 space-y-2 pr-1 max-h-[400px]">
          <div v-for="(spell, idx) in filteredSpells" :key="idx" class="p-2.5 bg-zinc-950/20 border border-zinc-800 rounded-sm flex flex-col gap-1 relative">
            
            <div v-if="editingSpellIndex !== idx" class="flex justify-between items-start">
              <span class="font-bold text-zinc-200">{{ spell.name }} <span class="text-zinc-600 font-normal text-[8px]">({{ spell.level }} кр.)</span></span>
              <div class="flex items-center gap-2">
                <button @click="editingSpellIndex = idx" class="text-[8px] text-zinc-500 hover:text-emerald-400 outline-none">[ИЗМЕН.]</button>
                <button @click="removeSpell(idx)" class="text-[9px] text-zinc-600 hover:text-red-500 font-bold outline-none">×</button>
              </div>
            </div>

            <!-- Форма редактирования inline -->
            <div v-else class="space-y-1.5 pt-1 border-t border-zinc-900 mt-1">
              <input v-model="spell.name" class="input-inline" placeholder="Название">
              <input v-model.number="spell.level" type="number" min="0" max="9" class="input-inline" placeholder="Круг">
              <input v-model="spell.url" class="input-inline text-emerald-500/80" placeholder="Ссылка на Wiki">
              <input v-model="spell.desc" class="input-inline text-zinc-400" placeholder="Краткое описание">
              <label class="flex items-center gap-2 cursor-pointer text-[9px] text-zinc-400 mt-1 select-none">
                <input type="checkbox" v-model="spell.isRitual" class="accent-emerald-500">
                <span>РИТУАЛ</span>
              </label>
              <div class="flex gap-2">
                <button @click="saveEdit" class="flex-1 bg-emerald-950/40 border border-emerald-800 text-emerald-400 text-[9px] py-1 rounded-sm hover:bg-emerald-900/30">ОК</button>
              </div>
            </div>

            <p v-if="editingSpellIndex !== idx && spell.desc" class="text-[8px] text-zinc-500 leading-tight">{{ spell.desc }}</p>
            <div v-if="editingSpellIndex !== idx && spell.isRitual" class="flex">
              <span class="text-[7px] text-blue-400 bg-blue-900/20 border border-blue-500/20 px-0.5 rounded">РИТ</span>
            </div>
          </div>
          <div v-if="filteredSpells.length === 0" class="text-center py-4 text-zinc-600 text-[9px] italic border border-dashed border-zinc-900/40 rounded">Нет совпадений</div>
        </div>

      </div>
    </template>
  </div>
</template>

<style scoped>
.input-cyber { @apply w-full bg-zinc-900 border border-zinc-800 p-2 text-xs font-mono text-emerald-100 focus:border-emerald-500/50 outline-none transition; }
.input-cyber:focus { border-color: #10b981; }
.input-cyber::-webkit-outer-spin-button, .input-cyber::-webkit-inner-spin-button { -webkit-appearance: none; margin: 0; }
.input-cyber { -moz-appearance: textfield; }
.input-inline { @apply w-full bg-zinc-900 border border-zinc-800 text-[10px] p-1.5 focus:outline-none focus:border-emerald-500/50 text-zinc-300 font-mono h-6 rounded-sm; }
.custom-scrollbar::-webkit-scrollbar { width: 3px; }
.custom-scrollbar::-webkit-scrollbar-thumb { background: #10b981; }
</style>