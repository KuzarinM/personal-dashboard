<script setup>
import { ref, watch } from 'vue'
import { request } from '@/api'

const props = defineProps({
  isOpen: Boolean,
  dashboardId: Number
})

const emit = defineEmits(['close', 'refresh'])

const config = ref({
  interestsPrompt: '',
  targets: []
})

const isSaving = ref(false)
const isLoading = ref(false)

watch(() => props.isOpen, (val) => {
  if (val) {
    loadSettings()
  }
})

const loadSettings = async () => {
  isLoading.value = true
  try {
    const data = await request(`/integrations/${props.dashboardId}/webscraper/settings`)
    
    // Безопасно парсим InterestsPrompt (обрабатываем оба варианта регистра)
    config.value.interestsPrompt = data.InterestsPrompt || data.interestsPrompt || ''
    
    // Извлекаем массив источников
    const rawTargets = data.Targets || data.targets || []
    
    // Маппим PascalCase свойства C# в camelCase свойства JS для реактивного рендеринга во Vue
    config.value.targets = rawTargets.map(t => ({
      name: t.Name !== undefined ? t.Name : (t.name || ''),
      url: t.Url !== undefined ? t.Url : (t.url || ''),
      targetType: t.TargetType !== undefined ? t.TargetType : (t.targetType || 'HTML'),
      enabled: t.Enabled !== undefined ? t.Enabled : (t.enabled !== false)
    }))
  } catch (e) {
    console.error("Failed to load scraper/podcast settings", e)
  } finally {
    isLoading.value = false
  }
}

const addTarget = () => {
  config.value.targets.push({
    name: 'New Source',
    url: '',
    targetType: 'HTML',
    enabled: true
  })
}

const removeTarget = (index) => {
  config.value.targets.splice(index, 1)
}

const save = async () => {
  isSaving.value = true
  try {
    await request(`/integrations/${props.dashboardId}/webscraper/settings`, {
      method: 'PUT',
      body: JSON.stringify(config.value)
    })
    emit('refresh')
    emit('close')
  } catch (e) {
    alert(e.message)
  } finally {
    isSaving.value = false
  }
}
</script>

<template>
  <div v-if="isOpen" class="fixed inset-0 z-50 flex items-center justify-center p-4">
    <div class="absolute inset-0 bg-black/90 backdrop-blur-sm" @click="$emit('close')"></div>
    <div class="relative bg-zinc-950 border border-amber-500/30 w-full max-w-2xl flex flex-col rounded shadow font-sans overflow-hidden max-h-[85vh]">
      
      <!-- Header -->
      <div class="p-3 border-b border-zinc-800 bg-zinc-900/50 flex justify-between items-center">
        <h2 class="text-amber-500 font-mono font-bold tracking-widest text-sm uppercase">🎙️ PODCAST_AND_SCRAPER_CONFIG</h2>
        <button @click="$emit('close')" class="text-zinc-500 hover:text-red-400 text-xs font-mono">[ESC]</button>
      </div>

      <div class="p-6 space-y-6 overflow-y-auto custom-scrollbar flex-1 bg-zinc-900/10">
        
        <!-- Loader during loading -->
        <div v-if="isLoading" class="text-center py-12 text-amber-500 font-mono text-xs italic animate-pulse">
          READING CONFIGURED STREAM...
        </div>

        <template v-else>
          <!-- 1. Interests Prompt -->
          <div class="space-y-2">
            <label class="block text-[10px] text-zinc-500 font-mono uppercase tracking-wider">Directives & Personal Interests (ALL CAPS AUTO-CONVERSION)</label>
            <textarea 
              v-model="config.interestsPrompt" 
              class="w-full bg-zinc-950 border border-zinc-800 p-3 text-xs font-mono text-amber-100 outline-none focus:border-amber-500/50 h-24 resize-none"
              placeholder="Опишите ваши текущие цели, задачи и фокус внимания. Например: ИНТЕРЕСУЮТ НОВОСТИ СВЯЗАННЫЕ С NET 8, КРИПТОВАЛЮТОЙ И ПОГОДОЙ В МОЕМ ГОРОДЕ..."
            ></textarea>
          </div>

          <!-- 2. Targets List -->
          <div class="space-y-4">
            <div class="flex justify-between items-center border-b border-zinc-800 pb-1">
              <span class="text-[10px] text-zinc-500 font-mono uppercase tracking-wider">Scraping Sources (Web & RSS)</span>
              <button @click="addTarget" class="text-[10px] text-amber-500 font-mono hover:text-amber-300 transition">[+] ADD SOURCE</button>
            </div>

            <div v-if="config.targets.length === 0" class="text-center py-6 text-zinc-600 font-mono text-xs border border-dashed border-zinc-800">
              NO EXTERNAL RESOURCES CONFIGURED
            </div>

            <div v-for="(target, idx) in config.targets" :key="idx" class="bg-zinc-900/40 border border-zinc-800 p-3 rounded flex flex-col gap-3 relative group">
              
              <button @click="removeTarget(idx)" class="absolute top-2 right-2 text-zinc-600 hover:text-red-500 font-mono text-xs">✖</button>

              <div class="grid grid-cols-12 gap-3 items-center">
                <!-- Enabled switch -->
                <div class="col-span-2">
                  <button 
                    @click="target.enabled = !target.enabled"
                    class="w-full py-1 text-[9px] font-mono font-bold border rounded transition"
                    :class="target.enabled ? 'bg-amber-500/20 border-amber-500/40 text-amber-400' : 'bg-zinc-950 border-zinc-800 text-zinc-600'"
                  >
                    {{ target.enabled ? 'ACTIVE' : 'MUTED' }}
                  </button>
                </div>

                <!-- Name -->
                <div class="col-span-6">
                  <input 
                    v-model="target.name" 
                    class="w-full bg-zinc-950 border border-zinc-800 p-1.5 text-xs font-mono text-zinc-300 focus:border-amber-500 outline-none" 
                    placeholder="Source Name"
                  >
                </div>

                <!-- Type (HTML / RSS) -->
                <div class="col-span-4">
                  <select 
                    v-model="target.targetType" 
                    class="w-full bg-zinc-950 border border-zinc-800 p-1.5 text-xs font-mono text-zinc-400 focus:border-amber-500 outline-none cursor-pointer"
                  >
                    <option value="HTML">WEB PAGE (HTML)</option>
                    <option value="RSS">RSS FEED (XML)</option>
                  </select>
                </div>
              </div>

              <!-- URL input -->
              <div>
                <input 
                  v-model="target.url" 
                  class="w-full bg-zinc-950 border border-zinc-800 p-1.5 text-xs font-mono text-zinc-400 focus:border-amber-500 outline-none" 
                  placeholder="https://example.com/feed.xml or https://news.ycombinator.com"
                >
              </div>

            </div>
          </div>
        </template>

      </div>

      <!-- Footer -->
      <div class="p-4 border-t border-zinc-800 bg-zinc-900/30 flex justify-end gap-3">
        <button @click="$emit('close')" class="px-4 py-2 text-zinc-500 hover:text-zinc-300 font-mono text-xs transition">CANCEL</button>
        <button @click="save" :disabled="isSaving" class="px-6 py-2 bg-amber-900/20 border border-amber-500/50 text-amber-400 font-mono text-xs hover:bg-amber-500 hover:text-black transition flex items-center gap-2">
          <span v-if="isSaving" class="animate-spin">/</span> SAVE CHANGES
        </button>
      </div>

    </div>
  </div>
</template>

<style scoped>
.custom-scrollbar::-webkit-scrollbar { width: 4px; }
.custom-scrollbar::-webkit-scrollbar-thumb { background: #3f3f46; border-radius: 2px; }
</style>