<script setup>
import { ref } from 'vue'

const props = defineProps({
  modelValue: String, // v-model
  placeholder: { type: String, default: 'Icon' }
})

const emit = defineEmits(['update:modelValue'])

const isOpen = ref(false)

// Тематический набор для дашборда
const emojiList = [
  // Statuses
  '🟢','🟡','🔴','⚪','🔵','🟣','⚫',
  // Work & Tech
  '💻','🖥️','⌨️','🖱️','🔋','🔌','📡','💾','💿','📱','⌚',
  '🛠️','⚙️','🔧','⛏️','🧱','🏗️',
  // Indicators
  '🔥','⚡','💧','❄️','🌪️','🌤️','🌙','🌟',
  '⚠️','🚫','✅','❌','❓','❗','🛑',
  // Activities & Life
  '🏠','🏢','🚗','✈️','🚀','🚲',
  '🍔','☕','🍺','🍕','🎮','🎧','📚','💊',
  '💰','💸','📅','🕒','💤','👀','🧠'
]

const selectEmoji = (emoji) => {
  emit('update:modelValue', emoji)
  isOpen.value = false
}

const onInput = (e) => {
  emit('update:modelValue', e.target.value)
}
</script>

<template>
  <div class="relative w-full">
    
    <!-- Overlay to close on click outside -->
    <div v-if="isOpen" class="fixed inset-0 z-40" @click="isOpen = false"></div>

    <!-- Input Field -->
    <input 
      :value="modelValue" 
      @input="onInput"
      @focus="isOpen = true"
      class="w-full bg-zinc-950 border border-zinc-800 p-2 text-center rounded focus:border-emerald-500 outline-none transition relative z-30"
      :placeholder="placeholder"
    >

    <!-- Dropdown Grid -->
    <div v-if="isOpen" class="absolute top-full left-0 mt-1 w-64 bg-zinc-900 border border-zinc-700 shadow-xl rounded p-2 z-50 grid grid-cols-7 gap-1 max-h-48 overflow-y-auto custom-scrollbar animate-in fade-in zoom-in duration-200">
        <button 
            v-for="emoji in emojiList" 
            :key="emoji"
            @click="selectEmoji(emoji)"
            class="hover:bg-zinc-700 rounded p-1 text-lg leading-none transition flex items-center justify-center aspect-square"
        >
            {{ emoji }}
        </button>
    </div>
  </div>
</template>

<style scoped>
.custom-scrollbar::-webkit-scrollbar { width: 4px; }
.custom-scrollbar::-webkit-scrollbar-track { background: transparent; }
.custom-scrollbar::-webkit-scrollbar-thumb { background: #3f3f46; border-radius: 2px; }
.custom-scrollbar::-webkit-scrollbar-thumb:hover { background: #10b981; }
</style>