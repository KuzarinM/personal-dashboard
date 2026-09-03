<script setup>
defineProps({
  message: String,
  isOpen: Boolean
})
defineEmits(['dismiss'])
</script>

<template>
  <div v-if="isOpen" class="fixed inset-0 z-[100] flex flex-col items-center justify-center bg-black/90 backdrop-blur-md animate-in fade-in zoom-in duration-300">
    <!-- Pulse Background -->
    <div class="absolute inset-0 bg-red-500/20 animate-pulse pointer-events-none"></div>
    
    <div class="relative z-10 text-center space-y-6 p-10 border-2 border-red-500 bg-black rounded-lg shadow-[0_0_100px_rgba(239,68,68,0.5)] max-w-2xl w-full mx-4">
        <div class="text-red-500 font-mono text-xl tracking-[0.5em] animate-bounce">ALARM TRIGGERED</div>
        
        <h1 class="text-4xl md:text-6xl font-bold text-white font-mono break-words">
            {{ message }}
        </h1>

        <button 
            @click="$emit('dismiss')" 
            class="mt-8 px-10 py-4 bg-red-600 hover:bg-red-500 text-black font-bold font-mono text-xl rounded transition transform hover:scale-105 shadow-lg shadow-red-500/50"
        >
            ACKNOWLEDGE
        </button>
    </div>
  </div>
</template>