<script setup>
import { ref, onBeforeUnmount } from 'vue'
import { request } from '@/api'
import PodcastSettingsModal from './PodcastSettingsModal.vue'

const props = defineProps({
  dashboardId: Number
})

const isSettingsOpen = ref(false)
const loading = ref(false)
const error = ref(null)

// Аудио состояние
const audioUrl = ref(null)
const audioPlayer = ref(null)
const isPlaying = ref(false)

// Отладочное состояние
const showDebug = ref(false)
const debugTab = ref('raw') // Установлена по умолчанию вкладка 'raw' (MD-отчет)
const debugContent = ref('')
const isLoadingDebug = ref(false)

const generatePodcast = async () => {
  loading.value = true
  error.value = null
  isPlaying.value = false

  if (audioUrl.value) {
    URL.revokeObjectURL(audioUrl.value)
    audioUrl.value = null
  }

  try {
    const token = localStorage.getItem('jwt_token')
    
    const response = await fetch(`/api/reports/podcast/audio?dashboardId=${props.dashboardId}`, {
      headers: {
        'Authorization': `Bearer ${token}`
      }
    })

    if (!response.ok) {
      throw new Error(`Synthesis Failed: HTTP ${response.status}`)
    }

    const blob = await response.blob()
    audioUrl.value = URL.createObjectURL(blob)

    setTimeout(() => {
      if (audioPlayer.value) {
        audioPlayer.value.play()
        isPlaying.value = true
      }
    }, 100)

    if (showDebug.value) {
      fetchDebugContent()
    }

  } catch (e) {
    error.value = e.message
  } finally {
    loading.value = false
  }
}

const onAudioPlay = () => {
  isPlaying.value = true
}

const onAudioPause = () => {
  isPlaying.value = false
}

const onAudioEnded = () => {
  isPlaying.value = false
}

// --- ОТЛАДОЧНЫЕ МЕТОДЫ ---

const toggleDebugPanel = () => {
  showDebug.value = !showDebug.value
  if (showDebug.value) {
    fetchDebugContent()
  }
}

const selectDebugTab = (tab) => {
  debugTab.value = tab
  fetchDebugContent()
}

const fetchDebugContent = async () => {
  isLoadingDebug.value = true
  debugContent.value = ''
  try {
    const endpoint = debugTab.value === 'script' ? '/reports/podcast' : '/reports/morning'
    const response = await fetch(`/api${endpoint}?dashboardId=${props.dashboardId}`, {
      headers: {
        'Authorization': `Bearer ${localStorage.getItem('jwt_token')}`
      }
    })
    
    // ИСПРАВЛЕНО: Заменено $"..." на стандартные обратные кавычки `...`
    if (!response.ok) throw new Error(`HTTP ${response.status}`)
    
    debugContent.value = await response.text()
  } catch (e) {
    debugContent.value = `[DEBUG] Failed to load content: ${e.message}`
  } finally {
    isLoadingDebug.value = false
  }
}

const downloadDebugFile = () => {
  if (!debugContent.value) return
  
  const isScript = debugTab.value === 'script'
  const blob = new Blob([debugContent.value], { type: 'text/plain;charset=utf-8' })
  const url = URL.createObjectURL(blob)
  
  const link = document.createElement('a')
  link.href = url
  link.download = isScript ? 'podcast_script.txt' : 'raw_morning_report.md'
  
  document.body.appendChild(link)
  link.click()
  
  document.body.removeChild(link)
  URL.revokeObjectURL(url)
}

onBeforeUnmount(() => {
  if (audioUrl.value) {
    URL.revokeObjectURL(audioUrl.value)
  }
})
</script>

<template>
  <div class="bg-zinc-900/50 border border-zinc-800 p-4 rounded-sm flex flex-col relative overflow-hidden group/podcast">
    
    <PodcastSettingsModal 
      :is-open="isSettingsOpen" 
      :dashboard-id="dashboardId" 
      @close="isSettingsOpen = false" 
    />

    <!-- Header -->
    <div class="text-[10px] text-amber-500 font-mono font-bold uppercase tracking-widest border-b border-amber-500/20 pb-1 mb-3 flex justify-between items-center">
      <span class="flex items-center gap-2">🎙️ MORNING_FM_PODCAST</span>
      <div class="flex items-center gap-2">
        <button 
          @click="toggleDebugPanel"
          class="text-zinc-600 hover:text-amber-500 transition duration-300 text-[10px] font-mono"
          title="Toggle Inspection Log"
        >
          [INSPECT]
        </button>
        <button 
          @click="isSettingsOpen = true" 
          class="text-zinc-600 hover:text-amber-400 opacity-0 group-hover/podcast:opacity-100 transition duration-300"
          title="Settings"
        >
          ⚙
        </button>
      </div>
    </div>

    <div class="flex flex-col gap-3 justify-center items-center py-2 relative z-10 w-full">
      
      <!-- Waveform Animation -->
      <div class="h-8 flex items-end gap-1 mb-1 justify-center animate-duration-500" :class="{ 'opacity-30': !isPlaying }">
        <span class="w-1 bg-amber-500 rounded-sm" :class="{ 'wave-bar bar-1': isPlaying, 'h-2': !isPlaying }"></span>
        <span class="w-1 bg-amber-500 rounded-sm" :class="{ 'wave-bar bar-2': isPlaying, 'h-4': !isPlaying }"></span>
        <span class="w-1 bg-amber-500 rounded-sm" :class="{ 'wave-bar bar-3': isPlaying, 'h-3': !isPlaying }"></span>
        <span class="w-1 bg-amber-500 rounded-sm" :class="{ 'wave-bar bar-4': isPlaying, 'h-5': !isPlaying }"></span>
        <span class="w-1 bg-amber-500 rounded-sm" :class="{ 'wave-bar bar-5': isPlaying, 'h-2': !isPlaying }"></span>
      </div>

      <!-- Compilation button if no audio -->
      <button 
        v-if="!audioUrl"
        @click="generatePodcast" 
        :disabled="loading"
        class="w-full py-2.5 bg-amber-900/20 border border-amber-500/40 text-amber-400 hover:bg-amber-500 hover:text-black font-mono text-xs font-bold transition flex items-center justify-center gap-2 rounded-sm"
      >
        <span v-if="loading" class="animate-spin">/</span>
        {{ loading ? 'COMPILING SCRIPT & SYNTHESIZING...' : 'COMPILE MORNING BRIEF' }}
      </button>

      <!-- Interactive Audio Player with controls if compiled -->
      <div v-else class="w-full space-y-2">
        <audio 
          ref="audioPlayer" 
          :src="audioUrl" 
          controls
          @play="onAudioPlay"
          @pause="onAudioPause"
          @ended="onAudioEnded"
          class="w-full accent-amber-500 h-8 rounded-sm overflow-hidden bg-zinc-950"
        ></audio>

        <div class="flex justify-end">
          <button 
            @click="generatePodcast"
            :disabled="loading"
            class="text-[9px] font-mono text-zinc-500 hover:text-amber-400 transition"
            title="Recompile Brief"
          >
            [RECOMPILE_AUDIO 🔄]
          </button>
        </div>
      </div>

      <!-- Error view -->
      <div v-if="error" class="text-[9px] text-red-500 font-mono border border-red-900/30 bg-red-900/10 p-2 rounded text-center w-full">
        ERROR: {{ error }}
      </div>

      <!-- Expandable Debug Panel -->
      <div v-if="showDebug" class="w-full mt-4 border-t border-zinc-800 pt-3 flex flex-col gap-2 animate-in fade-in slide-in-from-top-2 duration-300">
        <div class="flex items-center justify-between text-[9px] font-mono border-b border-zinc-900 pb-1.5">
          <div class="flex gap-2">
            <button 
              @click="selectDebugTab('raw')"
              class="transition"
              :class="debugTab === 'raw' ? 'text-amber-400 font-bold' : 'text-zinc-600 hover:text-zinc-400'"
            >
              RAW_REPORT_MD
            </button>
            <span class="text-zinc-800">|</span>
            <button 
              @click="selectDebugTab('script')"
              class="transition"
              :class="debugTab === 'script' ? 'text-amber-400 font-bold' : 'text-zinc-600 hover:text-zinc-400'"
            >
              PODCAST_SCRIPT
            </button>
          </div>
          <button 
            @click="downloadDebugFile" 
            class="text-zinc-500 hover:text-amber-500 transition"
            title="Download as File"
          >
            [SAVE_FILE]
          </button>
        </div>

        <div class="relative bg-zinc-950 border border-zinc-850 p-2 rounded-sm max-h-48 overflow-y-auto custom-scrollbar">
          <div v-if="isLoadingDebug" class="text-zinc-600 text-[10px] font-mono italic animate-pulse py-4 text-center">
            RETRIEVING DATASTREAM...
          </div>
          <pre 
            v-else 
            class="text-[10px] font-mono text-zinc-400 whitespace-pre-wrap leading-relaxed select-text"
          >{{ debugContent || '*(Empty)*' }}</pre>
        </div>
      </div>

    </div>
  </div>
</template>

<style scoped>
.wave-bar {
  width: 3px;
  animation: wave 1.2s ease-in-out infinite alternate;
}
.bar-1 { height: 10px; animation-delay: 0.1s; }
.bar-2 { height: 18px; animation-delay: 0.3s; }
.bar-3 { height: 12px; animation-delay: 0.0s; }
.bar-4 { height: 22px; animation-delay: 0.5s; }
.bar-5 { height: 8px;  animation-delay: 0.2s; }

@keyframes wave {
  0% { height: 4px; }
  100% { height: 26px; }
}

.custom-scrollbar::-webkit-scrollbar {
  width: 3px;
}
.custom-scrollbar::-webkit-scrollbar-thumb {
  background: #d97706; /* amber-600 */
  border-radius: 1px;
}
</style>