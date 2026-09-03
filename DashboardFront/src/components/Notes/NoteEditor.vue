<script setup>
import { ref, nextTick } from 'vue'

const props = defineProps({
  note: Object,
  allowEdit: Boolean
})

const emit = defineEmits(['save', 'pull', 'rename', 'toggle-type', 'focus', 'blur'])

const textareaRef = ref(null)

// --- HISTORY & UNDO/REDO ---
const recordState = (textarea) => {
  const content = textarea.value
  const cursor = textarea.selectionStart
  
  if (props.note.historyIndex < props.note.history.length - 1) {
    props.note.history = props.note.history.slice(0, props.note.historyIndex + 1)
  }
  
  const current = props.note.history[props.note.historyIndex]
  
  // ВАЖНО: Если текст не изменился, но курсор сдвинулся - просто обновляем курсор
  // Это делает Ctrl+Z супер точным!
  if (current && current.content === content) {
    current.cursor = cursor
    return
  }
  
  props.note.history.push({ content, cursor })
  props.note.historyIndex++
  
  if (props.note.history.length > 1500) {
    props.note.history.shift()
    props.note.historyIndex--
  }
}

const restoreState = () => {
  const state = props.note.history[props.note.historyIndex]
  props.note.content = state.content
  nextTick(() => {
    if (textareaRef.value) textareaRef.value.setSelectionRange(state.cursor, state.cursor)
  })
  emit('save', props.note)
}

// --- УМНЫЙ TAB (СМЕЩЕНИЕ ТЕКСТА) ---
const handleTab = (e, isOutdent = false) => {
  const textarea = e.target
  const start = textarea.selectionStart
  const end = textarea.selectionEnd
  const value = textarea.value

  // 1. Обычный Tab без выделения
  if (start === end && !isOutdent) {
    textarea.setRangeText('    ', start, end, 'end')
    textarea.dispatchEvent(new Event('input'))
    return
  }

  // 2. Блочный Indent / Outdent
  const lineStart = value.lastIndexOf('\n', start - 1) + 1
  const lineEnd = value.indexOf('\n', end)
  const actualEnd = lineEnd === -1 ? value.length : lineEnd

  const selectedBlock = value.substring(lineStart, actualEnd)
  const lines = selectedBlock.split('\n')

  let startOffset = 0
  let endOffset = 0

  const newLines = lines.map((line, index) => {
    if (!isOutdent) {
      endOffset += 4
      if (index === 0) startOffset += 4
      return '    ' + line
    } else {
      let removeCount = 0
      if (line.startsWith('\t')) {
        removeCount = 1
      } else {
        const match = line.match(/^ {1,4}/)
        if (match) removeCount = match[0].length
      }
      endOffset -= removeCount
      if (index === 0) startOffset -= removeCount
      return line.substring(removeCount)
    }
  })

  const newBlock = newLines.join('\n')

  if (newBlock !== selectedBlock) {
    textarea.setRangeText(newBlock, lineStart, actualEnd)
    const newStart = Math.max(lineStart, start + startOffset)
    const newEnd = Math.max(lineStart, end + endOffset)
    textarea.setSelectionRange(newStart, newEnd)
    textarea.dispatchEvent(new Event('input'))
  }
}

// --- KEY HANDLERS ---
const handleKeyDown = (e) => {
  if (!props.allowEdit) return

  const textarea = e.target

  // 1. UNDO / REDO (Обрабатываем ПЕРВЫМ, используем e.code для защиты от RU-раскладки)
  if (e.ctrlKey || e.metaKey) {
    if (e.code === 'KeyZ' && !e.shiftKey) {
      e.preventDefault()
      if (props.note.historyIndex > 0) {
        props.note.historyIndex--
        restoreState()
      }
      return
    } else if ((e.code === 'KeyZ' && e.shiftKey) || e.code === 'KeyY') {
      e.preventDefault()
      if (props.note.historyIndex < props.note.history.length - 1) {
        props.note.historyIndex++
        restoreState()
      }
      return
    }
  }

  // 2. УМНЫЙ BACKSPACE
  if (e.key === 'Backspace' && textarea.selectionStart === textarea.selectionEnd) {
    const start = textarea.selectionStart
    if (start >= 4) {
      const textBeforeCursor = textarea.value.substring(start - 4, start)
      if (textBeforeCursor === '    ') {
        e.preventDefault()
        recordState(textarea) // Делаем снимок перед удалением!
        textarea.setRangeText('', start - 4, start, 'end')
        textarea.dispatchEvent(new Event('input'))
        return
      }
    }
  }

  // 3. TAB и SHIFT+TAB
  if (e.key === 'Tab') {
    e.preventDefault()
    recordState(textarea) // Делаем снимок перед сдвигом!
    handleTab(e, e.shiftKey)
    return
  }
}

const onInput = (e) => {
  if (!props.allowEdit) return
  recordState(e.target)
  emit('save', props.note)
}

// --- CHECKLISTS ---
const getChecklistItems = () => { try { return JSON.parse(props.note.content) || [] } catch { return [] } }
const updateChecklist = (items) => { props.note.content = JSON.stringify(items); emit('save', props.note) }
const addChecklistItem = () => { if (!props.allowEdit) return; const items = getChecklistItems(); items.push({ text: '', done: false }); updateChecklist(items) }
const toggleCheckItem = (index) => { if (!props.allowEdit) return; const items = getChecklistItems(); items[index].done = !items[index].done; updateChecklist(items) }
const removeCheckItem = (index) => { if (!props.allowEdit) return; const items = getChecklistItems(); items.splice(index, 1); updateChecklist(items) }
const updateCheckItemText = (index, text) => { if (!props.allowEdit) return; const items = getChecklistItems(); items[index].text = text; updateChecklist(items) }
</script>

<template>
  <div class="flex flex-col h-full w-full relative">
    <!-- TOOLBAR -->
    <div class="flex items-center bg-[#252526] border-b border-zinc-800 px-2 h-8 select-none">
      <input :disabled="!allowEdit" v-model="note.title" @change="$emit('rename', note)" 
             class="bg-transparent text-xs font-mono text-zinc-400 py-1 px-2 outline-none flex-1 focus:text-emerald-400 disabled:opacity-50">
      
      <div class="flex items-center gap-2 border-l border-zinc-700 pl-2 ml-2">
        <span v-if="note.content !== note.serverContent" class="text-[9px] text-amber-500 font-mono pr-2 animate-pulse" title="Not synced to server yet">
          UNSAVED
        </span>
        <button v-if="note.content !== note.serverContent" @click="$emit('pull', note)" 
                class="text-[9px] bg-amber-900/50 text-amber-400 border border-amber-500/50 px-2 rounded hover:bg-amber-500 hover:text-black transition">
          PULL
        </button>
        <button v-if="allowEdit" @click="$emit('toggle-type', note)" 
                class="text-[9px] font-mono border border-zinc-600 px-2 rounded hover:bg-zinc-700 text-zinc-400 transition">
          {{ note.type === 'Text' ? 'TXT' : 'LIST' }}
        </button>
      </div>
    </div>

    <!-- EDITOR AREA -->
    <div class="flex flex-1 overflow-hidden relative">
      <!-- TEXT MODE -->
      <template v-if="note.type === 'Text'">
        <textarea 
          ref="textareaRef"
          :disabled="!allowEdit"
          v-model="note.content" 
          @input="onInput"
          @keydown="handleKeyDown"
          @focus="$emit('focus')"
          @blur="$emit('blur')"
          class="flex-1 h-full bg-[#1e1e1e] text-zinc-300 font-mono text-sm p-4 leading-6 focus:outline-none resize-none whitespace-pre overflow-y-auto custom-scrollbar border-none m-0 whitespace-pre-wrap break-words"
          spellcheck="false"
        ></textarea>
      </template>

      <!-- CHECKLIST MODE -->
      <div v-else class="flex-1 h-full bg-[#1e1e1e] overflow-y-auto custom-scrollbar p-4 space-y-2">
        <div v-for="(item, idx) in getChecklistItems()" :key="idx" class="flex items-center gap-3 group">
          <div @click="toggleCheckItem(idx)" class="w-5 h-5 border border-zinc-600 rounded flex items-center justify-center cursor-pointer transition" 
               :class="item.done ? 'bg-zinc-800 border-emerald-500/50' : 'hover:border-zinc-400'">
            <span v-if="item.done" class="text-emerald-500 text-xs">✓</span>
          </div>
          <input :disabled="!allowEdit" :value="item.text" 
                 @input="(e) => updateCheckItemText(idx, e.target.value)" 
                 @focus="$emit('focus')"
                 @blur="$emit('blur')"
                 class="flex-1 bg-transparent border-b border-transparent focus:border-zinc-700 outline-none text-sm font-mono text-zinc-300 transition" 
                 :class="{'line-through text-zinc-600': item.done}">
          <button v-if="allowEdit" @click="removeCheckItem(idx)" class="text-zinc-600 hover:text-red-500 opacity-0 group-hover:opacity-100 transition">×</button>
        </div>
        <button v-if="allowEdit" @click="addChecklistItem" class="text-zinc-500 hover:text-emerald-400 text-xs font-mono mt-2 flex items-center gap-2">
          <span>+</span> Add Item
        </button>
      </div>
    </div>
  </div>
</template>