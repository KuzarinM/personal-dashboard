<script setup>
import { ref } from 'vue'
import CountdownTimer from '@/components/CountdownTimer.vue'
import EventsModal from '@/components/EventsModal.vue'

const props = defineProps({
  dashboardId: Number,
  eventsGrouped: { type: Object, required: true },
  urgencySettings: { type: Object, default: () => null },
  // Данные для модалки редактирования
  manualEvents: { type: Array, default: () => [] },
  calendars: { type: Array, default: () => [] }
})

const emit = defineEmits(['refresh']) // Пробрасываем рефреш наверх
const isSettingsOpen = ref(false)

const onRefresh = () => {
    emit('refresh')
}
</script>

<template>
  <div class="space-y-4 relative group/widget">
    
    <!-- MODAL -->
    <EventsModal 
        :is-open="isSettingsOpen" 
        :dashboard-id="dashboardId" 
        :initial-events="manualEvents" 
        :initial-calendars="calendars" 
        @close="isSettingsOpen = false" 
        @refresh="onRefresh" 
    />

    <!-- TODAY -->
    <div class="flex items-center justify-between text-[10px] font-mono font-bold text-emerald-500 uppercase tracking-widest border-b border-emerald-500/20 pb-1">
        <span>TARGETS_TODAY</span>
        
        <!-- Settings Button (Only visible on top header) -->
        <button 
           @click="isSettingsOpen = true" 
           class="text-zinc-600 hover:text-emerald-400 opacity-0 group-hover/widget:opacity-100 transition"
           title="Manage Events"
        >
           ⚙
        </button>
    </div>

    <div v-if="eventsGrouped.today && eventsGrouped.today.length" class="space-y-2">
      <CountdownTimer v-for="ev in eventsGrouped.today" :key="ev.name + ev.date" :event="ev" :urgency-settings="urgencySettings" />
    </div>
    <div v-else class="text-zinc-700 font-mono text-[10px] py-2 pl-2 border-l border-zinc-800 italic">NO_ACTIVE_TARGETS</div>

    <!-- TOMORROW -->
    <div class="flex items-center justify-between text-[10px] font-mono font-bold text-zinc-500 uppercase tracking-widest border-b border-zinc-800 pb-1">
        <span>TOMORROW</span>
    </div>
    <div v-if="eventsGrouped.tomorrow && eventsGrouped.tomorrow.length" class="space-y-2">
       <CountdownTimer v-for="ev in eventsGrouped.tomorrow" :key="ev.name + ev.date" :event="ev" :urgency-settings="urgencySettings" />
    </div>
    <div v-else class="text-zinc-700 font-mono text-[10px] py-2 pl-2 border-l border-zinc-800 italic">SCHEDULE_CLEAR</div>
    
    <!-- UPCOMING -->
    <div class="flex items-center justify-between text-[10px] font-mono font-bold text-zinc-600 uppercase tracking-widest border-b border-zinc-800 pb-1">
        <span>UPCOMING</span>
    </div>
    <div v-if="eventsGrouped.upcoming && eventsGrouped.upcoming.length" class="space-y-2">
       <CountdownTimer v-for="ev in eventsGrouped.upcoming" :key="ev.name + ev.date" :event="ev" :urgency-settings="urgencySettings" />
    </div>
  </div>
</template>