<script setup>
import { ref, shallowRef, watch } from 'vue'
import { Codemirror } from 'vue-codemirror'
import { json } from '@codemirror/lang-json'
import { oneDark } from '@codemirror/theme-one-dark'

const props = defineProps({
  isOpen: Boolean,
  configData: Object
})

const emit = defineEmits(['close', 'save'])

const extensions = [json(), oneDark]
const editorCode = ref('')
const view = shallowRef()
const isSaving = ref(false)
const saveError = ref('')

watch(() => props.isOpen, (newVal) => {
  if (newVal && props.configData) {
    editorCode.value = JSON.stringify(props.configData, null, 2)
    saveError.value = ''
  }
})

const handleReady = (payload) => { view.value = payload.view }

const handleSave = async () => {
  let payload
  try {
    payload = JSON.parse(editorCode.value)
  } catch (e) {
    saveError.value = 'СИНТАКСИЧЕСКАЯ ОШИБКА JSON'
    return
  }

  isSaving.value = true
  saveError.value = ''

  try {
    // Мы НЕ отправляем заголовок Authorization вручную.
    // Браузер сам подставит то, что запомнил при входе на дашборд.
    const res = await fetch(window.location.pathname.replace('dashboard', 'config').replace('/', '/api/config/'), {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload)
    })

    if (res.ok) {
      emit('save')
    } else {
      // Если 401 - браузер покажет окно. Если 500 - ошибка сервера.
      const err = await res.json()
      saveError.value = err.error || 'ОШИБКА СОХРАНЕНИЯ'
    }
  } catch (e) {
    saveError.value = 'ОШИБКА СЕТИ'
  } finally {
    isSaving.value = false
  }
}
</script>

<template>
  <div v-if="isOpen" class="fixed inset-0 z-50 flex items-center justify-center p-4">
    <div class="absolute inset-0 bg-black/90 backdrop-blur-sm" @click="$emit('close')"></div>

    <div class="relative bg-zinc-900 border border-emerald-500/30 w-full max-w-5xl h-[90vh] flex flex-col rounded shadow-[0_0_30px_rgba(16,185,129,0.15)] overflow-hidden">
      
      <div class="flex items-center justify-between p-3 border-b border-zinc-800 bg-zinc-950">
        <h2 class="text-emerald-500 font-mono font-bold tracking-widest flex items-center gap-2 text-sm">
          <span class="animate-pulse">_</span>CONFIG.JSON
        </h2>
        <button @click="$emit('close')" class="text-zinc-500 hover:text-red-400 transition font-mono text-xs">[ESC]</button>
      </div>

      <div class="flex-1 overflow-hidden relative text-sm">
        <Codemirror
          v-model="editorCode"
          placeholder="Code goes here..."
          :style="{ height: '100%' }"
          :autofocus="true"
          :indent-with-tab="true"
          :tab-size="2"
          :extensions="extensions"
          @ready="handleReady"
        />
      </div>

      <div class="p-4 border-t border-zinc-800 bg-zinc-950 flex flex-col md:flex-row gap-4 items-center justify-end">
        
        <!-- Вывод ошибки -->
        <div v-if="saveError" class="text-red-500 font-mono text-xs animate-pulse mr-auto">
          ERROR: {{ saveError }}
        </div>

        <button @click="$emit('close')" class="px-4 py-2 text-zinc-500 hover:text-zinc-300 font-mono text-xs transition">CANCEL</button>
        
        <button 
          @click="handleSave"
          :disabled="isSaving"
          class="px-6 py-2 bg-emerald-900/20 border border-emerald-500/50 text-emerald-400 font-mono text-xs hover:bg-emerald-500 hover:text-black transition rounded-sm flex items-center gap-2"
        >
          <span v-if="isSaving" class="animate-spin">/</span>
          {{ isSaving ? 'SAVING...' : 'SAVE CONFIG' }}
        </button>
      </div>
    </div>
  </div>
</template>