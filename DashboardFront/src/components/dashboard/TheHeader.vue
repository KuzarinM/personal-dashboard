<script setup>
import { computed } from 'vue'
import { widgetRegistry } from '@/config/widgets'

const props = defineProps({
  title: String,
  isPublic: Boolean,
  floatingWidgets: Set,
  headerLayout: String,
  showSettings: { type: Boolean, default: true }, // <--- НОВЫЙ ПРОП
  socketConnected: Boolean
})

const isAdmin = localStorage.getItem('is_admin') === 'true'

const emit = defineEmits([ 'open-nav', 'open-links', 'open-settings', 'toggle-floating', 'logout' ])

const visibleButtons = computed(() => {
    if (!props.headerLayout) {
        return [
            { id: 'calculator', enabled: true },
            { id: 'telegram', enabled: true },
            { id: 'userstatus', enabled: true }
        ].filter(b => widgetRegistry[b.id]).map(b => ({ id: b.id, ...widgetRegistry[b.id] }))
    }
    try {
        const layout = JSON.parse(props.headerLayout)
        return layout.filter(b => b.enabled && widgetRegistry[b.id]).map(b => ({ id: b.id, ...widgetRegistry[b.id] }))
    } catch (e) { return [] }
})
</script>

<template>
  <header class="mb-6 flex flex-col md:flex-row items-center justify-between border-b border-zinc-800 pb-4 gap-4">
    <div class="flex items-center gap-4">
      <h1 class="text-2xl font-mono font-bold text-emerald-400 tracking-widest flex items-center gap-2">
         <span class="w-3 h-3 bg-emerald-500 rounded-full animate-pulse"></span>
         {{ title || 'SYSTEM' }}
      </h1>
      
      <!-- Public/Secure Badge -->
      <span v-if="isPublic" class="text-[10px] border border-emerald-500/30 text-emerald-500 px-1 rounded">PUBLIC</span>
      <span v-else class="text-[10px] border border-amber-500/30 text-amber-500 px-1 rounded">SECURE</span>

      <!-- SOCKET STATUS INDICATOR -->
      <div class="flex items-center gap-1.5 px-2 py-0.5 rounded border text-[9px] font-mono transition-colors duration-500"
           :class="socketConnected ? 'border-emerald-500/20 bg-emerald-900/10 text-emerald-400' : 'border-red-500/20 bg-red-900/10 text-red-400 animate-pulse'">
          <span class="w-1.5 h-1.5 rounded-full" :class="socketConnected ? 'bg-emerald-500 shadow-[0_0_5px_#10b981]' : 'bg-red-500'"></span>
          <span>{{ socketConnected ? 'NET_LINK: ON' : 'NET_LINK: LOST' }}</span>
      </div>

    </div>

    <div class="flex items-center gap-2 font-mono text-xs">

       <!-- ADMIN BUTTON -->
      <router-link v-if="isAdmin" to="/admin" class="btn-icon text-red-500 border-red-900/50 hover:bg-red-900/20 hover:border-red-500 mr-2">
          [ADMIN]
      </router-link>

      <!-- ... кнопки (NAV, CFG, WIDGETS, EXIT) без изменений ... -->
      <button @click="$emit('open-nav')" class="btn-icon bg-zinc-900 border-zinc-700 text-emerald-500 hover:text-emerald-300 hover:border-emerald-500 flex items-center gap-2"><span>📂</span> NAV: {{ title }}</button>
      <div class="w-px h-4 bg-zinc-800 mx-1"></div>
      <button v-if="showSettings" @click="$emit('open-settings')" class="btn-icon">[CFG]</button>
      
      <div v-if="visibleButtons.length" class="flex bg-zinc-900 rounded border border-zinc-700 ml-2">
          <button v-for="btn in visibleButtons" :key="btn.id" @click="$emit('toggle-floating', btn.id)"
              class="px-2 py-1 hover:bg-zinc-800 transition first:rounded-l last:rounded-r border-r border-zinc-800 last:border-0 relative group"
              :class="props.floatingWidgets.has(btn.id) ? 'text-emerald-400' : 'text-zinc-500'"
              :title="'Open ' + btn.name">
              {{ btn.icon }}
              <span class="absolute -top-5 left-1/2 -translate-x-1/2 text-[8px] text-zinc-500 opacity-0 group-hover:opacity-100 transition whitespace-nowrap font-mono pointer-events-none bg-zinc-950/80 px-1 rounded border border-zinc-800">
                  Alt+{{ btn.keyChar }}
              </span>
          </button>
      </div>

      <div class="w-px h-4 bg-zinc-800 mx-1"></div>
      <button @click="$emit('logout')" class="btn-icon text-red-400 hover:border-red-500">[EXIT]</button>
    </div>
  </header>
</template>

<style scoped>
.btn-icon { @apply text-[10px] font-mono border border-zinc-800 text-zinc-500 px-2 py-1 rounded hover:text-emerald-400 hover:border-emerald-500 transition; }
</style>