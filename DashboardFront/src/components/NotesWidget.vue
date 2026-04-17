<script setup>
import { ref, watch, nextTick } from 'vue'
import { request } from '@/api'

const props = defineProps({
  dashboardId: Number,
  initialNotes: { type: Array, default: () =>[] },
  allowEdit: Boolean
})

const activeTabId = ref('links') // 'links', 'preview', или ID заметки
const notes = ref([])
const archivedNotes = ref([])
const showArchive = ref(false)
const isLoadingArchive = ref(false)

// --- PREVIEW STATE ---
const previewUrl = ref(null)

// Refs для управления курсором
const textareaRefs = ref({})

// --- EXPOSED METHODS (Для вызова из родителя) ---
const openPreview = (url) => {
    previewUrl.value = url
    activeTabId.value = 'preview'
}
defineExpose({ openPreview })

// --- SORTING ---
const sortNotes = () => {
    notes.value.sort((a, b) => {
        // 1. Сначала закрепленные (true > false)
        if (a.pinned !== b.pinned) return b.pinned - a.pinned
        // 2. Потом по ID (новые сверху, так как ID - это timestamp или автоинкремент)
        return b.id - a.id
    })
}

// --- INIT & REACTIVITY (ЗАЩИТА ОТ СБРОСА КУРСОРА) ---
watch(() => props.initialNotes, (newVal) => {
  if (!newVal) return
  // 1. Первый запуск
  if (notes.value.length === 0) {
      notes.value = newVal.map(n => ({
        ...n,
        type: n.type || 'Text',
        // Инициализация истории
        history: [{ content: n.content || '', cursor: 0 }],
        historyIndex: 0,
        serverContent: n.content || '' // <--- Добавили эталон сервера
      }))
      sortNotes()
      return
  }
  // 2. Слияние (Merge) при обновлении с сервера
  const serverIds = newVal.map(n => n.id)
  
  // А. Обновляем существующие и добавляем новые
  newVal.forEach(serverNote => {
      const localNote = notes.value.find(n => n.id === serverNote.id)
      
      if (localNote) {
          // Метаданные обновляем всегда
          localNote.title = serverNote.title
          localNote.pinned = serverNote.pinned
          localNote.isArchived = serverNote.isArchived
          localNote.type = serverNote.type
          localNote.publicId = serverNote.publicId
          
          const el = textareaRefs.value[localNote.id]
          const isFocused = el && document.activeElement === el

          // Контент обновляем только если нет локальных несохраненных изменений
          // и элемент в данный момент не находится в фокусе
          if (localNote.content === localNote.serverContent) {
              if (localNote.content !== serverNote.content && !isFocused) {
                  localNote.content = serverNote.content
                  // Для простоты - не трогаем историю, как в оригинале
              }
          }
          
          // Всегда запоминаем актуальный серверный контент для проверок
          localNote.serverContent = serverNote.content
      } else {
          // Новая заметка
          notes.value.push({
              ...serverNote,
              type: serverNote.type || 'Text',
              history: [{ content: serverNote.content, cursor: 0 }],
              historyIndex: 0,
              serverContent: serverNote.content || ''
          })
      }
  })
  // Б. Удаляем те, которых нет на сервере
  for (let i = notes.value.length - 1; i >= 0; i--) {
      if (!serverIds.includes(notes.value[i].id)) {
          notes.value.splice(i, 1)
      }
  }
  sortNotes()
}, { deep: true, immediate: true })

// --- API WRAPPERS ---
const apiCreate = async (type = 'Text') => {
    return await request('/notes', { 
        method: 'POST', 
        body: JSON.stringify({ dashboardId: props.dashboardId, title: 'New Note', type }) 
    })
}

const apiUpdate = async (noteId, payload) => {
    await request(`/notes/${noteId}`, { method: 'PATCH', body: JSON.stringify(payload) })
}

const apiDelete = async (noteId) => {
    await request(`/notes/${noteId}`, { method: 'DELETE' })
}

const fetchArchived = async () => {
    isLoadingArchive.value = true
    try {
        archivedNotes.value = await request(`/notes/archive/${props.dashboardId}`)
    } catch(e) { console.error(e) } 
    finally { isLoadingArchive.value = false }
}

// --- HISTORY & UNDO/REDO (ОРИГИНАЛ) ---
const recordState = (note, textarea) => {
    const content = textarea.value
    const cursor = textarea.selectionStart
    
    // Обрезаем "будущее", если были откаты
    if (note.historyIndex < note.history.length - 1) {
        note.history = note.history.slice(0, note.historyIndex + 1)
    }
    
    const current = note.history[note.historyIndex]
    if (current && current.content === content) return
    note.history.push({ content, cursor })
    note.historyIndex++
    if (note.history.length > 1500) {
        note.history.shift(); note.historyIndex--
    }
}

// --- KEY HANDLERS (TAB & UNDO) (ОРИГИНАЛ) ---
const handleTab = (e) => {
    const textarea = e.target
    const start = textarea.selectionStart
    const end = textarea.selectionEnd
    // Вставка 4 пробелов
    const spaces = "    "
    textarea.setRangeText(spaces, start, end, "end")
    
    // Триггерим input событие вручную, чтобы обновить v-model и историю
    textarea.dispatchEvent(new Event('input'))
}

const handleKeyDown = (e, note) => {
    if (!props.allowEdit) return
    // TAB
    if (e.key === 'Tab') {
        e.preventDefault()
        handleTab(e)
        return
    }
    // UNDO/REDO (Ctrl+Z / Ctrl+Y)
    if (e.ctrlKey || e.metaKey) {
        if (e.key === 'z' && !e.shiftKey) {
            e.preventDefault()
            if (note.historyIndex > 0) {
                note.historyIndex--
                restoreState(note)
            }
        } else if ((e.key === 'z' && e.shiftKey) || e.key === 'y') {
            e.preventDefault()
            if (note.historyIndex < note.history.length - 1) {
                note.historyIndex++
                restoreState(note)
            }
        }
    }
}

const restoreState = (note) => {
    const state = note.history[note.historyIndex]
    note.content = state.content
    nextTick(() => {
        const el = textareaRefs.value[note.id]
        if (el) el.setSelectionRange(state.cursor, state.cursor)
    })
    onContentChange(note) // Save to server
}

const onInput = (e, note) => {
    if (!props.allowEdit) return
    recordState(note, e.target)
    onContentChange(note)
}

// --- DEBOUNCED SAVE ---
// У каждого note свой таймер, чтобы они не перебивали друг друга
const debounceTimers = new Map()

const onContentChange = (note) => {
    if (!props.allowEdit) return
    clearTimeout(debounceTimers.get(note.id))
    
    const timer = setTimeout(async () => {
        try {
            await apiUpdate(note.id, { content: note.content })
            note.serverContent = note.content // Приравниваем эталон после успешного сохранения
        } catch(e) {
            console.error(e)
        }
    }, 1000)
    debounceTimers.set(note.id, timer)
}

// Принудительное стягивание текста с сервера (если случился конфликт)
const pullServerData = (note) => {
    note.content = note.serverContent
    note.history =[{ content: note.serverContent, cursor: 0 }]
    note.historyIndex = 0
}

// --- ACTIONS ---
const addNote = async () => {
    if (!props.allowEdit) return
    try {
        const newNoteData = await apiCreate('Text')
        const newNote = {
            ...newNoteData,
            serverContent: '',
            history: [{ content: '', cursor: 0 }],
            historyIndex: 0
        }
        notes.value.push(newNote) // Сначала пушим
        sortNotes() // Потом сортируем
        activeTabId.value = newNote.id
        showArchive.value = false 
        previewUrl.value = null
    } catch(e) { alert(e.message) }
}

const togglePin = (note) => {
    if (!props.allowEdit) return
    note.pinned = !note.pinned
    sortNotes() // Мгновенная сортировка
    apiUpdate(note.id, { isPinned: note.pinned })
}

const archiveNote = async (index) => {
    if (!props.allowEdit) return
    const note = notes.value[index]
    notes.value.splice(index, 1)
    if (activeTabId.value === note.id) activeTabId.value = 'links'
    try {
        await apiUpdate(note.id, { isArchived: true })
        if (archivedNotes.value.length > 0) archivedNotes.value.unshift({ ...note, isArchived: true })
    } catch(e) { notes.value.splice(index, 0, note); alert('Error'); }
}

const restoreNote = async (index) => {
    const note = archivedNotes.value[index]; archivedNotes.value.splice(index, 1)
    try { 
        await apiUpdate(note.id, { isArchived: false }); 
        notes.value.push({...note, isArchived: false, serverContent: note.content, history:[], historyIndex:0}); 
        sortNotes(); 
    } catch(e){}
}

const deleteForever = async (index) => {
    const note = archivedNotes.value[index]; archivedNotes.value.splice(index, 1)
    try { await apiDelete(note.id) } catch(e){}
}

const renameNote = (note) => {
    if (!props.allowEdit) return
    const t = prompt("Name:", note.title)
    if(t) { note.title = t; apiUpdate(note.id, { title: t }) }
}

// --- CHECKLISTS ---
const getChecklistItems = (note) => { try { return JSON.parse(note.content) || [] } catch { return[] } }
const updateChecklist = (note, items) => { note.content = JSON.stringify(items); onContentChange(note) }
const addChecklistItem = (note) => { if (!props.allowEdit) return; const items = getChecklistItems(note); items.push({ text: '', done: false }); updateChecklist(note, items) }
const toggleCheckItem = (note, index) => { if (!props.allowEdit) return; const items = getChecklistItems(note); items[index].done = !items[index].done; updateChecklist(note, items) }
const removeCheckItem = (note, index) => { if (!props.allowEdit) return; const items = getChecklistItems(note); items.splice(index, 1); updateChecklist(note, items) }
const updateCheckItemText = (note, index, text) => { if (!props.allowEdit) return; const items = getChecklistItems(note); items[index].text = text; updateChecklist(note, items) }

const toggleNoteType = (note) => {
    if (!props.allowEdit) return
    if (note.type === 'Text') {
        const lines = note.content.split('\n').filter(l => l.trim()); const items = lines.map(l => ({ text: l, done: false })); note.content = JSON.stringify(items); note.type = 'Checklist'; note.history =[]
    } else {
        const items = getChecklistItems(note); note.content = items.map(i => (i.done ? '[x] ' : '[ ] ') + i.text).join('\n'); note.type = 'Text'; note.history =[{ content: note.content, cursor: 0 }]; note.historyIndex = 0
    }
    
    // Обновляем на сервере и подтягиваем эталон, чтобы не было конфликта
    apiUpdate(note.id, { type: note.type, content: note.content }).then(() => {
        note.serverContent = note.content
    })
}

// --- NAVIGATION UI ---
const selectLinks = () => {
    if (!showArchive.value) {
        activeTabId.value = 'links'
        previewUrl.value = null
    }
}

const toggleArchiveView = () => {
    showArchive.value = !showArchive.value
    if (showArchive.value) {
        activeTabId.value = null; previewUrl.value = null; fetchArchived()
    } else {
        activeTabId.value = 'links'
    }
}

// --- DRAG (Visual Reorder Only) ---
const dragStartIdx = ref(null)
const onDragStart = (e, idx) => { dragStartIdx.value = idx }
const onDrop = (e, dropIdx) => { 
    const item = notes.value.splice(dragStartIdx.value, 1)[0]
    notes.value.splice(dropIdx, 0, item) 
}
</script>

<template>
  <div class="flex flex-col bg-zinc-900/30 border border-zinc-800 rounded-sm overflow-hidden h-full min-h-[500px]">
    
    <!-- HEADER -->
    <div class="flex items-center bg-zinc-950 border-b border-zinc-800 overflow-x-auto scrollbar-hide select-none h-[42px]">
            
      <!-- 1. LINKS TAB -->
      <div @click="selectLinks"
            class="px-4 h-full flex items-center text-[10px] font-mono cursor-pointer border-r border-zinc-800 font-bold transition flex-shrink-0"
            :class="(!showArchive && activeTabId === 'links' && !previewUrl) ? 'text-emerald-400 bg-zinc-900' : 'text-zinc-500 hover:text-zinc-300'">
           LINKS
      </div>

      <!-- 2. PREVIEW TAB (Active only when url exists) -->
      <div v-if="previewUrl"
            @click="activeTabId = 'preview'"
           class="px-4 h-full flex items-center text-[10px] font-mono cursor-pointer border-r border-zinc-800 font-bold transition flex-shrink-0 bg-zinc-900 text-emerald-400 relative group animate-in fade-in slide-in-from-left-2 border-b-2 border-emerald-500">
           <span class="mr-2">👁 PREVIEW</span>
           <button @click.stop="previewUrl = null; activeTabId = 'links'" class="hover:text-red-500 font-bold">×</button>
      </div>
      
      <!-- 3. NOTES TABS -->
      <template v-if="!showArchive">
          <div v-for="(note, idx) in notes" :key="note.id" 
               draggable="true" @dragstart="onDragStart($event, idx)" @drop="onDrop($event, idx)" @dragover.prevent
               @click="activeTabId = note.id; previewUrl = null" @dblclick="renameNote(note)"
               class="group relative px-4 h-full flex items-center text-[10px] font-mono cursor-pointer border-r border-zinc-800 max-w-[140px] transition pr-8 flex-shrink-0"
               :class="[ activeTabId === note.id && !previewUrl ? 'text-emerald-100 bg-zinc-900' : 'text-zinc-500 hover:bg-zinc-900/50', note.pinned ? 'border-l-2 border-l-amber-500/50 pl-3' : '' ]"
          >
            <span class="truncate block w-full">
                <span v-if="note.type === 'Checklist'" class="text-blue-400 mr-1">☑</span>
                {{ note.title }}
            </span>
            
            <div class="absolute right-1 top-0 h-full flex items-center gap-1 opacity-0 group-hover:opacity-100 transition px-1 bg-zinc-900/80 backdrop-blur">
                <button @click.stop="togglePin(note)" class="hover:text-amber-400 p-1 transition transform hover:scale-110" title="Pin/Unpin">
                    {{ note.pinned ? '★' : '☆' }}
                </button>
                <button v-if="allowEdit" @click.stop="archiveNote(idx)" class="hover:text-sky-400 p-1 font-bold" title="Archive">↓</button>
            </div>
          </div>
          <button v-if="allowEdit" @click="addNote" class="px-3 h-full text-zinc-600 hover:text-emerald-400 border-r border-zinc-800 hover:bg-zinc-900 transition font-mono text-xs flex-shrink-0">+</button>
      </template>

      <div class="flex-1"></div>

      <!-- ARCHIVE TOGGLE -->
      <button @click="toggleArchiveView" 
               class="px-3 h-full border-l border-zinc-800 transition flex items-center justify-center gap-2 font-mono text-[10px]"
              :class="showArchive ? 'bg-zinc-800 text-sky-400' : 'text-zinc-600 hover:text-sky-400 hover:bg-zinc-900'">
              <span class="text-sm">📦</span>
              <span v-if="showArchive">ARCHIVE MODE</span>
      </button>
    </div>

    <!-- BODY -->
    <div class="flex-1 bg-[#1e1e1e] relative overflow-hidden flex flex-col">
      
      <!-- MODE: LINKS -->
      <div v-show="!showArchive && !previewUrl && activeTabId === 'links'" class="h-full bg-zinc-900/20">
          <slot name="links-content"></slot>
      </div>

      <!-- MODE: PREVIEW (IFRAME) -->
      <div v-if="previewUrl && activeTabId === 'preview'" class="h-full w-full bg-white flex flex-col">
          <div class="bg-zinc-950 border-b border-zinc-800 p-2 flex justify-between items-center text-xs font-mono text-zinc-500">
              <span class="truncate px-2">{{ previewUrl }}</span>
              <a :href="previewUrl" target="_blank" class="hover:text-emerald-400 px-2 flex items-center gap-1">OPEN EXTERNAL ↗</a>
          </div>
          <iframe :src="previewUrl" class="flex-1 w-full border-none bg-white"></iframe>
      </div>

      <!-- MODE: ARCHIVE -->
      <div v-if="showArchive" class="h-full bg-zinc-950/50 overflow-y-auto custom-scrollbar p-4">
          <div v-if="isLoadingArchive" class="text-zinc-600 text-xs font-mono animate-pulse">Loading Archive...</div>
          <div v-else-if="archivedNotes.length === 0" class="text-zinc-600 text-xs font-mono text-center py-10">Archive is empty</div>
          <div v-else class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
              <div v-for="(note, idx) in archivedNotes" :key="note.id" class="bg-zinc-900 border border-zinc-800 p-3 rounded opacity-75 hover:opacity-100 transition flex flex-col gap-2">
                  <div class="flex justify-between items-start border-b border-zinc-800 pb-2">
                      <span class="font-mono text-xs font-bold text-zinc-400 truncate">{{ note.title }}</span>
                      <span class="text-[9px] bg-zinc-800 px-1 rounded text-zinc-500">{{ note.type }}</span>
                  </div>
                  <div class="flex-1 text-[10px] font-mono text-zinc-600 h-16 overflow-hidden relative">
                      {{ note.content.slice(0, 100) }}...
                  </div>
                  <div class="flex justify-end gap-2 border-t border-zinc-800 pt-2">
                      <button v-if="allowEdit" @click="restoreNote(idx)" class="text-emerald-500 hover:bg-emerald-900/20 px-2 py-1 rounded text-[10px] font-mono border border-emerald-500/20">RESTORE</button>
                      <button v-if="allowEdit" @click="deleteForever(idx)" class="text-red-500 hover:bg-red-900/20 px-2 py-1 rounded text-[10px] font-mono border border-red-500/20">DELETE</button>
                  </div>
              </div>
          </div>
      </div>

      <!-- MODE: EDITOR -->
      <template v-if="!showArchive && !previewUrl">
          <template v-for="note in notes" :key="note.id">
            <div v-show="activeTabId === note.id" class="flex flex-col h-full w-full relative">
               
               <!-- TOOLBAR -->
               <div class="flex items-center bg-[#252526] border-b border-zinc-800 px-2 h-8 select-none">
                   <input :disabled="!allowEdit" v-model="note.title" @change="renameNote(note)" class="bg-transparent text-xs font-mono text-zinc-400 py-1 px-2 outline-none flex-1 focus:text-emerald-400 disabled:opacity-50">
                   
                   <div class="flex items-center gap-2 border-l border-zinc-700 pl-2 ml-2">
                       <!-- UI ИНДИКАТОР КОНФЛИКТА/СЕЙВА -->
                       <span v-if="note.content !== note.serverContent" class="text-[9px] text-amber-500 font-mono pr-2 animate-pulse" title="Not synced to server yet">
                           UNSAVED
                       </span>
                       <button v-if="note.content !== note.serverContent" @click="pullServerData(note)" class="text-[9px] bg-amber-900/50 text-amber-400 border border-amber-500/50 px-2 rounded hover:bg-amber-500 hover:text-black transition" title="Force pull from server">
                           PULL
                       </button>

                       <button v-if="allowEdit" @click="toggleNoteType(note)" class="text-[9px] font-mono border border-zinc-600 px-2 rounded hover:bg-zinc-700 text-zinc-400 transition">
                           {{ note.type === 'Text' ? 'TXT' : 'LIST' }}
                       </button>
                   </div>
               </div>

               <div class="flex flex-1 overflow-hidden relative">
                   <!-- TEXT MODE -->
                   <template v-if="note.type === 'Text'">
                       <textarea 
                            :ref="(el) => textareaRefs[note.id] = el"
                            :disabled="!allowEdit"
                            v-model="note.content" 
                            @input="(e) => onInput(e, note)"
                            @keydown.tab.prevent="handleTab"
                            @keydown="handleKeyDown($event, note)"
                            class="flex-1 h-full bg-[#1e1e1e] text-zinc-300 font-mono text-sm p-4 leading-6 focus:outline-none resize-none whitespace-pre overflow-y-auto custom-scrollbar border-none m-0 whitespace-pre-wrap break-words"
                            spellcheck="false"
                       ></textarea>
                   </template>

                   <!-- CHECKLIST MODE -->
                   <div v-else class="flex-1 h-full bg-[#1e1e1e] overflow-y-auto custom-scrollbar p-4 space-y-2">
                       <div v-for="(item, idx) in getChecklistItems(note)" :key="idx" class="flex items-center gap-3 group">
                           <div @click="toggleCheckItem(note, idx)" class="w-5 h-5 border border-zinc-600 rounded flex items-center justify-center cursor-pointer transition" :class="item.done ? 'bg-zinc-800 border-emerald-500/50' : 'hover:border-zinc-400'">
                               <span v-if="item.done" class="text-emerald-500 text-xs">✓</span>
                           </div>
                           <input :disabled="!allowEdit" :value="item.text" @input="(e) => updateCheckItemText(note, idx, e.target.value)" class="flex-1 bg-transparent border-b border-transparent focus:border-zinc-700 outline-none text-sm font-mono text-zinc-300 transition" :class="{'line-through text-zinc-600': item.done}">
                           <button v-if="allowEdit" @click="removeCheckItem(note, idx)" class="text-zinc-600 hover:text-red-500 opacity-0 group-hover:opacity-100 transition">×</button>
                       </div>
                       <button v-if="allowEdit" @click="addChecklistItem(note)" class="text-zinc-500 hover:text-emerald-400 text-xs font-mono mt-2 flex items-center gap-2"><span>+</span> Add Item</button>
                   </div>
               </div>

            </div>
          </template>
      </template>

    </div>
  </div>
</template>