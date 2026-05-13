<script setup>
import { ref, watch, onMounted, onBeforeUnmount } from 'vue'
import { request } from '@/api'

import NotesTabs from './NotesTabs.vue'
import NoteEditor from './NoteEditor.vue'
import NotesArchive from './NotesArchive.vue'
import NotePreview from './NotePreview.vue'

const props = defineProps({
  dashboardId: Number,
  initialNotes: { type: Array, default: () =>[] },
  allowEdit: Boolean
})

const widgetRef = ref(null) // <--- Ссылка на корневой элемент виджета
const activeTabId = ref('links')
const notes = ref([])
const archivedNotes = ref([])
const showArchive = ref(false)
const isLoadingArchive = ref(false)
const previewUrl = ref(null)

const focusedNoteId = ref(null)

// --- EXPOSED METHODS ---
const openPreview = (url) => {
    previewUrl.value = url
    activeTabId.value = 'preview'
}
defineExpose({ openPreview })

// --- SORTING ---
const sortNotes = () => {
    notes.value.sort((a, b) => {
        if (a.pinned !== b.pinned) return b.pinned - a.pinned
        return b.id - a.id
    })
}

// --- INIT & REACTIVITY ---
watch(() => props.initialNotes, (newVal) => {
  if (!newVal) return
  
  if (notes.value.length === 0) {
      notes.value = newVal.map(n => ({
        ...n,
        pinned: Boolean(n.isPinned),
        type: n.type || 'Text',
        history: [{ content: n.content || '', cursor: 0 }],
        historyIndex: 0,
        serverContent: n.content || ''
      }))
      sortNotes()
      return
  }
  
  const serverIds = newVal.map(n => n.id)
  
  newVal.forEach(serverNote => {
      const localNote = notes.value.find(n => n.id === serverNote.id)
      
      if (localNote) {
          localNote.title = serverNote.title
          localNote.pinned = Boolean(serverNote.isPinned)
          localNote.isArchived = serverNote.isArchived
          localNote.type = serverNote.type
          localNote.publicId = serverNote.publicId
          
          const isFocused = focusedNoteId.value === localNote.id

          if (localNote.content === localNote.serverContent) {
              if (localNote.content !== serverNote.content && !isFocused) {
                  localNote.content = serverNote.content
              }
          }
          localNote.serverContent = serverNote.content
      } else {
          notes.value.push({
              ...serverNote,
              pinned: Boolean(serverNote.isPinned),
              type: serverNote.type || 'Text',
              history: [{ content: serverNote.content, cursor: 0 }],
              historyIndex: 0,
              serverContent: serverNote.content || ''
          })
      }
  })
  
  for (let i = notes.value.length - 1; i >= 0; i--) {
      if (!serverIds.includes(notes.value[i].id)) {
          notes.value.splice(i, 1)
      }
  }
  sortNotes()
}, { deep: true, immediate: true })

// --- HOTKEYS: ПЕРЕКЛЮЧЕНИЕ ТАБОВ (ALT + СТРЕЛКИ) ---
const handleTabSwitch = (direction) => {
    // 1 = вправо, -1 = влево
    if (showArchive.value) return // В архиве нет табов

    // 1. Собираем актуальный список вкладок (слева направо)
    const tabs = ['links']
    if (previewUrl.value) tabs.push('preview')
    notes.value.forEach(n => tabs.push(n.id)) // notes уже отсортированы (Pinned слева)!

    // 2. Ищем текущую позицию
    const currentIndex = tabs.indexOf(activeTabId.value)
    if (currentIndex === -1) return

    // 3. Вычисляем следующий индекс (с зацикливанием)
    let nextIndex = currentIndex + direction
    if (nextIndex < 0) nextIndex = tabs.length - 1
    if (nextIndex >= tabs.length) nextIndex = 0

    const nextTabId = tabs[nextIndex]
    
    // 4. Переключаем
    activeTabId.value = nextTabId

    // 5. Логика как при клике: уход с preview навсегда закрывает превьюшку
    if (nextTabId !== 'preview') {
        previewUrl.value = null
    }
}

const onWindowKeyDown = (e) => {
    if (!e.altKey) return
    if (e.key === 'ArrowLeft' || e.key === 'ArrowRight') {
        // Проверяем, находится ли фокус внутри виджета (чтобы не переключать табы, если юзер пишет в другом месте)
        // Если фокус просто на странице (body), тоже разрешаем переключение
        const isInsideWidget = widgetRef.value && widgetRef.value.contains(document.activeElement)
        const isBodyActive = document.activeElement === document.body
        
        if (isInsideWidget || isBodyActive) {
            e.preventDefault() // Обязательно! Отключаем стандартный браузерный "Назад/Вперед" по истории
            handleTabSwitch(e.key === 'ArrowRight' ? 1 : -1)
        }
    }
}

// Вешаем глобальный слушатель при загрузке
onMounted(() => window.addEventListener('keydown', onWindowKeyDown))
onBeforeUnmount(() => window.removeEventListener('keydown', onWindowKeyDown))

// --- API WRAPPERS ---
const apiCreate = async (type = 'Text') => {
    return await request('/notes', { method: 'POST', body: JSON.stringify({ dashboardId: props.dashboardId, title: 'New Note', type }) })
}
const apiUpdate = async (noteId, payload) => {
    await request(`/notes/${noteId}`, { method: 'PATCH', body: JSON.stringify(payload) })
}
const apiDelete = async (noteId) => {
    await request(`/notes/${noteId}`, { method: 'DELETE' })
}

const fetchArchived = async () => {
    isLoadingArchive.value = true
    try { archivedNotes.value = await request(`/notes/archive/${props.dashboardId}`) } 
    catch(e) { console.error(e) } 
    finally { isLoadingArchive.value = false }
}

// --- DEBOUNCED SAVE ---
const debounceTimers = new Map()

const onNoteSave = (note) => {
    if (!props.allowEdit) return
    clearTimeout(debounceTimers.get(note.id))
    
    const timer = setTimeout(async () => {
        try {
            await apiUpdate(note.id, { content: note.content })
            note.serverContent = note.content 
        } catch(e) { console.error(e) }
    }, 1000)
    debounceTimers.set(note.id, timer)
}

const pullServerData = (note) => {
    note.content = note.serverContent
    note.history = [{ content: note.serverContent, cursor: 0 }]
    note.historyIndex = 0
}

// --- ACTIONS ---
const addNote = async () => {
    if (!props.allowEdit) return
    try {
        const newNoteData = await apiCreate('Text')
        const newNote = { ...newNoteData, serverContent: '', history: [{ content: '', cursor: 0 }], historyIndex: 0 }
        notes.value.push(newNote)
        sortNotes()
        activeTabId.value = newNote.id
        showArchive.value = false 
        previewUrl.value = null
    } catch(e) { alert(e.message) }
}

const togglePin = (note) => {
    if (!props.allowEdit) return
    note.pinned = !note.pinned
    sortNotes()
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
        await apiUpdate(note.id, { isArchived: false })
        notes.value.push({...note, isArchived: false, serverContent: note.content, history:[], historyIndex:0})
        sortNotes()
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

const toggleNoteType = (note) => {
    if (!props.allowEdit) return
    if (note.type === 'Text') {
        const lines = note.content.split('\n').filter(l => l.trim())
        const items = lines.map(l => ({ text: l, done: false }))
        note.content = JSON.stringify(items); note.type = 'Checklist'; note.history = []
    } else {
        try {
          const items = JSON.parse(note.content) || []
          note.content = items.map(i => (i.done ? '[x] ' : '[ ] ') + i.text).join('\n')
        } catch { note.content = '' }
        note.type = 'Text'; note.history = [{ content: note.content, cursor: 0 }]; note.historyIndex = 0
    }
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
</script>

<template>
  <div 
    ref="widgetRef" 
    class="flex flex-col bg-zinc-900/30 border border-zinc-800 rounded-sm overflow-hidden h-full min-h-[500px] outline-none"
    tabindex="-1"
  >
    
    <!-- HEADER -->
    <NotesTabs 
      v-model:notes="notes"
      :activeTabId="activeTabId"
      :previewUrl="previewUrl"
      :showArchive="showArchive"
      :allowEdit="allowEdit"
      @select-links="selectLinks"
      @select-preview="activeTabId = 'preview'"
      @close-preview="previewUrl = null; activeTabId = 'links'"
      @select-note="(id) => { activeTabId = id; previewUrl = null }"
      @rename-note="renameNote"
      @toggle-pin="togglePin"
      @archive-note="archiveNote"
      @add-note="addNote"
      @toggle-archive-view="toggleArchiveView"
    />

    <!-- BODY -->
    <div class="flex-1 bg-[#1e1e1e] relative overflow-hidden flex flex-col">
      
      <!-- MODE: LINKS -->
      <div v-show="!showArchive && !previewUrl && activeTabId === 'links'" class="h-full bg-zinc-900/20">
        <slot name="links-content"></slot>
      </div>

      <!-- MODE: PREVIEW -->
      <NotePreview 
        v-if="previewUrl && activeTabId === 'preview'" 
        :url="previewUrl" 
      />

      <!-- MODE: ARCHIVE -->
      <NotesArchive 
        v-if="showArchive"
        :archivedNotes="archivedNotes"
        :isLoading="isLoadingArchive"
        :allowEdit="allowEdit"
        @restore="restoreNote"
        @delete-forever="deleteForever"
      />

      <!-- MODE: EDITOR -->
      <template v-if="!showArchive && !previewUrl">
        <template v-for="note in notes" :key="note.id">
          <NoteEditor 
            v-show="activeTabId === note.id"
            :note="note"
            :allowEdit="allowEdit"
            @save="onNoteSave"
            @pull="pullServerData"
            @rename="renameNote"
            @toggle-type="toggleNoteType"
            @focus="focusedNoteId = note.id"
            @blur="focusedNoteId = null"
          />
        </template>
      </template>

    </div>
  </div>
</template>