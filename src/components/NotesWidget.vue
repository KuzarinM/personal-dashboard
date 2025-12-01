<script setup>
import { ref, onMounted, onUnmounted, nextTick, watch } from 'vue'

const props = defineProps({
  dashboardId: String,
  allowNotes: { type: Boolean, default: true }
})

const activeTabId = ref('links')
const tabs = ref([])
const isSaving = ref(false)
const textareaRefs = ref({}) 
const gutterRefs = ref({})

// === 1. HISTORY ENGINE ===

const createSnapshot = (tab, textarea) => {
    const currentContent = tab.content
    // Если textarea еще не примонтирована (при загрузке), курсор 0
    const currentCursor = textarea ? textarea.selectionStart : 0

    // Проверка на дубликаты
    const last = tab.history[tab.historyIndex]
    if (last && last.content === currentContent) return

    // Отрезаем будущее
    if (tab.historyIndex < tab.history.length - 1) {
        tab.history = tab.history.slice(0, tab.historyIndex + 1)
    }

    // Лимит
    if (tab.history.length > 60) tab.history.shift()

    tab.history.push({ content: currentContent, cursor: currentCursor })
    tab.historyIndex = tab.history.length - 1
}

// Прямое манипулирование DOM для Firefox
const applySnapshot = (tab, snapshot) => {
    const el = textareaRefs.value[tab.id]
    if (!el) return

    // 1. Сначала обновляем модель Vue (чтобы реактивность сработала)
    tab.content = snapshot.content

    // 2. Но для Firefox принудительно обновляем DOM ПРЯМО СЕЙЧАС,
    // не дожидаясь nextTick, иначе курсор прыгнет в конец.
    el.value = snapshot.content
    
    // 3. Ставим курсор (используем setSelectionRange - это стандарт)
    el.setSelectionRange(snapshot.cursor, snapshot.cursor)
    
    // 4. Возвращаем фокус
    el.focus()
}

const performUndo = (tab) => {
    if (tab.historyIndex > 0) {
        tab.historyIndex--
        const snapshot = tab.history[tab.historyIndex]
        applySnapshot(tab, snapshot)
        autoSave()
    }
}

const performRedo = (tab) => {
    if (tab.historyIndex < tab.history.length - 1) {
        tab.historyIndex++
        const snapshot = tab.history[tab.historyIndex]
        applySnapshot(tab, snapshot)
        autoSave()
    }
}

// === 2. API SYNC ===
const fetchNotes = async () => {
  if (!props.allowNotes) return 
  try {
    const res = await fetch(`/api/notes/${props.dashboardId}`)
    const data = await res.json()
    if (Array.isArray(data) && data.length > 0) {
      tabs.value = data.map(t => ({
          ...t,
          history: [{ content: t.content, cursor: 0 }],
          historyIndex: 0
      }))
    } else {
      const firstId = Date.now()
      tabs.value = [{ 
          id: firstId, title: 'Draft_1', content: '', 
          history: [{ content: '', cursor: 0 }], historyIndex: 0 
      }]
    }
  } catch (e) { console.error(e) }
}

let saveTimeout = null
const autoSave = () => {
  if (!props.allowNotes) return
  isSaving.value = true
  clearTimeout(saveTimeout)
  saveTimeout = setTimeout(async () => {
    try {
        const payload = tabs.value.map(({ id, title, content }) => ({ id, title, content }))
        await fetch(`/api/notes/${props.dashboardId}`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        })
    } catch(e) {}
    finally { isSaving.value = false }
  }, 1000)
}

// === 3. TABS UI ===
const addTab = () => {
  const newId = Date.now()
  tabs.value.push({ 
      id: newId, title: 'New', content: '', 
      history: [{ content: '', cursor: 0 }], historyIndex: 0 
  })
  activeTabId.value = newId
  autoSave()
}
const removeTab = (id, e) => {
  e.stopPropagation()
  tabs.value = tabs.value.filter(t => t.id !== id)
  if (id === activeTabId.value) activeTabId.value = tabs.value.length ? tabs.value[0].id : 'links'
  autoSave()
}
const editTitle = (tab) => {
  const t = prompt("Rename:", tab.title)
  if (t) { tab.title = t; autoSave(); }
}
const switchTab = (dir) => {
    if (!props.allowNotes) return
    const ids = ['links', ...tabs.value.map(t => t.id)]
    let idx = ids.indexOf(activeTabId.value) + dir
    if (idx < 0) idx = ids.length - 1; if (idx >= ids.length) idx = 0;
    activeTabId.value = ids[idx]
}
const handleGlobalKeydown = (e) => {
    // Используем e.code для Alt, чтобы работать на любой раскладке
    if (e.altKey || e.code === 'AltLeft' || e.code === 'AltRight') {
        if (e.code === 'ArrowLeft') { e.preventDefault(); switchTab(-1); }
        if (e.code === 'ArrowRight') { e.preventDefault(); switchTab(1); }
    }
}

// === 4. EDITOR LOGIC ===
let typingTimeout = null

// Обычный ввод текста
const onInput = (e, tab) => {
    autoSave()
    const textarea = e.target

    // Снимок сразу при Paste или пробеле/Enter
    if (e.inputType === 'insertFromPaste' || e.data === ' ' || e.inputType === 'insertLineBreak') {
        createSnapshot(tab, textarea)
        return
    }
    // Снимок с задержкой при обычном наборе
    clearTimeout(typingTimeout)
    typingTimeout = setTimeout(() => {
        createSnapshot(tab, textarea)
    }, 1000)
}

const handleKeydown = (e, tab) => {
  const textarea = e.target

  // --- TAB ---
  if (e.code === 'Tab') {
    e.preventDefault()
    // Сохраняем "до"
    if (tab.content !== tab.history[tab.historyIndex].content) createSnapshot(tab, textarea)

    const start = textarea.selectionStart
    const end = textarea.selectionEnd

    // Манипуляция строкой
    const newVal = tab.content.substring(0, start) + "\t" + tab.content.substring(end)
    
    // Прямое обновление DOM для надежности
    textarea.value = newVal
    textarea.setSelectionRange(start + 1, start + 1)
    
    // Обновляем Vue модель
    tab.content = newVal
    
    // Сохраняем "после"
    createSnapshot(tab, textarea)
    autoSave()
    return
  }

  // --- SHORTCUTS ---
  // Используем e.code, чтобы работать на РУССКОЙ раскладке (KeyZ всегда KeyZ)
  const isCtrl = e.ctrlKey || e.metaKey // Windows Ctrl or Mac Cmd
  
  if (isCtrl && !e.shiftKey && e.code === 'KeyZ') {
      // UNDO
      e.preventDefault()
      // Если есть несохраненный "хвост" ввода - сохраняем его перед откатом
      if (tab.content !== tab.history[tab.historyIndex].content) {
          createSnapshot(tab, textarea)
      }
      performUndo(tab)
      return
  }

  if (isCtrl && (e.code === 'KeyY' || (e.shiftKey && e.code === 'KeyZ'))) {
      // REDO
      e.preventDefault()
      performRedo(tab)
      return
  }
}

const handleScroll = (e, id) => {
    if (gutterRefs.value[id]) gutterRefs.value[id].scrollTop = e.target.scrollTop
}
const getLineCount = (c) => c ? c.split('\n').length : 1

watch(() => props.allowNotes, (val) => { if (!val) activeTabId.value = 'links'; else fetchNotes(); })
onMounted(() => { fetchNotes(); window.addEventListener('keydown', handleGlobalKeydown); })
onUnmounted(() => window.removeEventListener('keydown', handleGlobalKeydown))
</script>

<template>
  <div class="flex flex-col h-full min-h-[500px] bg-zinc-900/30 border border-zinc-800 rounded-sm overflow-hidden relative group/widget">
    <!-- Header -->
    <div class="flex items-center bg-zinc-950 border-b border-zinc-800 overflow-x-auto scrollbar-hide">
      <div @click="activeTabId = 'links'" class="relative px-4 py-3 text-[10px] font-mono cursor-pointer border-r border-zinc-800 flex items-center gap-2 flex-shrink-0" :class="activeTabId === 'links' ? 'text-emerald-400 bg-zinc-900' : 'text-zinc-500 hover:text-zinc-300'">
        <span class="font-bold">SYSTEM_LINKS</span>
        <div v-if="activeTabId === 'links'" class="absolute bottom-0 left-0 w-full h-[2px] bg-emerald-500"></div>
      </div>
      <template v-if="allowNotes">
          <div v-for="tab in tabs" :key="tab.id" @click="activeTabId = tab.id" @dblclick="editTitle(tab)" class="group/tab relative px-4 py-3 text-[10px] font-mono cursor-pointer border-r border-zinc-800 hover:bg-zinc-900 min-w-[120px] max-w-[200px] text-center select-none flex-shrink-0 truncate" :class="activeTabId === tab.id ? 'text-emerald-100 bg-zinc-900' : 'text-zinc-500'">
            {{ tab.title }}
            <span @click="(e) => removeTab(tab.id, e)" class="absolute top-1 right-1 text-zinc-700 hover:text-red-500 opacity-0 group-hover/tab:opacity-100 px-1 font-bold z-10">×</span>
            <div v-if="activeTabId === tab.id" class="absolute bottom-0 left-0 w-full h-[2px] bg-emerald-500/50"></div>
          </div>
          <button @click="addTab" class="px-4 text-zinc-600 hover:text-emerald-400 font-mono text-sm border-r border-zinc-800 hover:bg-zinc-900 flex-shrink-0">+</button>
          <div class="ml-auto px-4 text-[9px] text-zinc-600 font-mono hidden md:block opacity-0 group-hover/widget:opacity-100 transition duration-500">
             <span v-if="isSaving" class="text-emerald-500/50 animate-pulse">SAVING...</span>
             <span v-else>READY</span>
          </div>
      </template>
      <div v-else class="flex-1"></div>
    </div>
    <!-- Body -->
    <div class="flex-1 bg-zinc-900/20 relative overflow-hidden">
      <div v-if="activeTabId === 'links'" class="p-6 h-full overflow-y-auto scrollbar-thin scrollbar-thumb-zinc-700">
         <slot name="links-content"></slot>
      </div>
      <template v-else-if="allowNotes">
        <div v-for="tab in tabs" :key="tab.id" v-show="activeTabId === tab.id" class="flex h-full w-full">
            <div :ref="(el) => gutterRefs[tab.id] = el" class="bg-zinc-950 border-r border-zinc-800 text-zinc-600 font-mono text-sm py-4 px-2 text-right select-none overflow-hidden min-w-[3rem]" style="line-height: 1.5rem;">
                <div v-for="n in getLineCount(tab.content)" :key="n">{{ n }}</div>
            </div>
            <!-- ВАЖНО: :value + @input вместо v-model для полного контроля в Firefox -->
            <textarea 
                :ref="(el) => textareaRefs[tab.id] = el"
                :value="tab.content"
                @input="(e) => { tab.content = e.target.value; onInput(e, tab); }"
                @keydown="handleKeydown($event, tab)"
                @scroll="handleScroll($event, tab.id)"
                class="flex-1 h-full bg-[#1e1e1e] text-zinc-300 font-mono text-sm p-4 focus:outline-none resize-none whitespace-pre overflow-y-auto scrollbar-thin scrollbar-thumb-zinc-700"
                style="line-height: 1.5rem; font-family: 'JetBrains Mono', 'Fira Code', monospace; tab-size: 4;"
                placeholder="// Start typing..."
                spellcheck="false"
            ></textarea>
        </div>
      </template>
    </div>
  </div>
</template>