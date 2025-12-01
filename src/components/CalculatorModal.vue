<script setup>
import { ref, onMounted, onUnmounted, computed, watch } from 'vue'

const props = defineProps({
  isOpen: Boolean
})

const emit = defineEmits(['close'])

const current = ref('')
const previous = ref('')
const operation = ref(null)

// Красивый вывод с разделителями тысяч
const displayValue = computed(() => {
  if (!current.value && !previous.value) return '0'
  return current.value
})

const historyValue = computed(() => {
  if (!operation.value) return ''
  return `${previous.value} ${operation.value}`
})

// === LOGIC ===
const append = (num) => {
  if (num === '.' && current.value.includes('.')) return
  // Ограничение длины, чтобы не ломать верстку
  if (current.value.length > 12) return
  
  if (current.value === '0' && num !== '.') {
      current.value = String(num)
  } else {
      current.value = current.value + String(num)
  }
}

const chooseOperation = (op) => {
  if (current.value === '') return
  if (previous.value !== '') compute()
  operation.value = op
  previous.value = current.value
  current.value = ''
}

const compute = () => {
  let computation
  const prev = parseFloat(previous.value)
  const curr = parseFloat(current.value)
  
  if (isNaN(prev) || isNaN(curr)) return
  
  switch (operation.value) {
    case '+': computation = prev + curr; break
    case '-': computation = prev - curr; break
    case '*': computation = prev * curr; break
    case '/': computation = prev / curr; break
    default: return
  }
  
  current.value = String(computation)
  operation.value = null
  previous.value = ''
}

const clear = () => {
  current.value = ''
  previous.value = ''
  operation.value = null
}

const del = () => {
  current.value = current.value.toString().slice(0, -1)
}

// === KEYBOARD SUPPORT ===
const handleKeydown = (e) => {
  if (!props.isOpen) return

  if (e.key >= '0' && e.key <= '9') append(e.key)
  if (e.key === '.') append('.')
  
  if (e.key === '=' || e.key === 'Enter') { e.preventDefault(); compute(); }
  if (e.key === 'Backspace') del()
  if (e.key === 'Escape') emit('close')
  
  if (e.key === '+') chooseOperation('+')
  if (e.key === '-') chooseOperation('-')
  if (e.key === '*') chooseOperation('*')
  if (e.key === '/') { e.preventDefault(); chooseOperation('/'); }
}

onMounted(() => window.addEventListener('keydown', handleKeydown))
onUnmounted(() => window.removeEventListener('keydown', handleKeydown))
</script>

<template>
  <div v-if="isOpen" class="fixed inset-0 z-50 flex items-center justify-center p-4">
    <!-- Backdrop -->
    <div class="absolute inset-0 bg-black/80 backdrop-blur-sm" @click="$emit('close')"></div>
    
    <!-- Calculator Body -->
    <div class="relative bg-zinc-950 border border-emerald-500/30 w-full max-w-xs shadow-[0_0_50px_rgba(16,185,129,0.1)] rounded-sm overflow-hidden flex flex-col">
        
        <!-- Header -->
        <div class="flex justify-between items-center p-2 border-b border-zinc-800 bg-zinc-900/50">
            <span class="text-[10px] text-zinc-500 font-mono tracking-widest">CALC_MODULE</span>
            <button @click="$emit('close')" class="text-zinc-600 hover:text-red-500 transition font-mono text-xs">[X]</button>
        </div>

        <!-- Display -->
        <div class="p-4 text-right font-mono bg-zinc-900/30">
            <div class="text-zinc-600 text-xs h-4">{{ historyValue }}</div>
            <div class="text-3xl text-emerald-400 font-bold tracking-wider truncate">{{ displayValue }}</div>
        </div>

        <!-- Keypad -->
        <div class="grid grid-cols-4 gap-px bg-zinc-800 border-t border-zinc-800">
            <!-- Row 1 -->
            <button @click="clear" class="btn col-span-2 text-red-400">AC</button>
            <button @click="del" class="btn text-zinc-400">DEL</button>
            <button @click="chooseOperation('/')" class="btn op">÷</button>
            
            <!-- Row 2 -->
            <button @click="append(7)" class="btn">7</button>
            <button @click="append(8)" class="btn">8</button>
            <button @click="append(9)" class="btn">9</button>
            <button @click="chooseOperation('*')" class="btn op">×</button>

            <!-- Row 3 -->
            <button @click="append(4)" class="btn">4</button>
            <button @click="append(5)" class="btn">5</button>
            <button @click="append(6)" class="btn">6</button>
            <button @click="chooseOperation('-')" class="btn op">-</button>

            <!-- Row 4 -->
            <button @click="append(1)" class="btn">1</button>
            <button @click="append(2)" class="btn">2</button>
            <button @click="append(3)" class="btn">3</button>
            <button @click="chooseOperation('+')" class="btn op">+</button>

            <!-- Row 5 -->
            <button @click="append(0)" class="btn col-span-2">0</button>
            <button @click="append('.')" class="btn">.</button>
            <button @click="compute" class="btn bg-emerald-900/20 text-emerald-400 hover:bg-emerald-500/20">=</button>
        </div>
    </div>
  </div>
</template>

<style scoped>
.btn {
    @apply h-14 bg-zinc-950 text-zinc-300 font-mono text-lg hover:bg-zinc-900 transition active:bg-zinc-800 outline-none;
}
.op {
    @apply text-emerald-500/80 bg-zinc-900/50;
}
</style>