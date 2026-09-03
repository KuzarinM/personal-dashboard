<script setup>
import { ref, computed, onMounted, onUnmounted } from 'vue'

const current = ref('')
const previous = ref('')
const operation = ref(null)

const displayValue = computed(() => (!current.value && !previous.value) ? '0' : current.value)
const historyValue = computed(() => operation.value ? `${previous.value} ${operation.value}` : '')

const append = (num) => {
  if (num === '.' && current.value.includes('.')) return
  if (current.value.length > 12) return
  if (current.value === '0' && num !== '.') current.value = String(num)
  else current.value = current.value + String(num)
}

const chooseOperation = (op) => {
  if (current.value === '') return
  if (previous.value !== '') compute()
  operation.value = op
  previous.value = current.value
  current.value = ''
}

const compute = () => {
  const prev = parseFloat(previous.value); const curr = parseFloat(current.value)
  if (isNaN(prev) || isNaN(curr)) return
  let res = 0
  switch (operation.value) {
    case '+': res = prev + curr; break; case '-': res = prev - curr; break
    case '*': res = prev * curr; break; case '/': res = prev / curr; break
  }
  current.value = String(res); operation.value = null; previous.value = ''
}

const clear = () => { current.value = ''; previous.value = ''; operation.value = null }
const del = () => { current.value = current.value.toString().slice(0, -1) }

// Keyboard support (global for this component instance)
const handleKeydown = (e) => {
    // Простая проверка фокуса, чтобы не печатать, когда вводят текст в другом месте
    if (document.activeElement.tagName === 'INPUT' || document.activeElement.tagName === 'TEXTAREA') return
    
    if (e.key >= '0' && e.key <= '9') append(e.key)
    if (e.key === '.' || e.key === ',') append('.')
    if (e.key === '=' || e.key === 'Enter') compute()
    if (e.key === 'Backspace') del()
    if (e.key === 'Escape') clear()
    if (['+','-','*','/'].includes(e.key)) chooseOperation(e.key)
}

// Слушаем клавиши только когда мышь над виджетом (чтобы не конфликтовать с другими)
const isHovered = ref(false)
onMounted(() => window.addEventListener('keydown', (e) => isHovered.value && handleKeydown(e)))
</script>

<template>
  <div 
    class="bg-zinc-900/50 border border-zinc-800 rounded-sm overflow-hidden flex flex-col group/calc"
    @mouseenter="isHovered = true" 
    @mouseleave="isHovered = false"
  >
        <!-- Header Style for Sidebar consistency -->
        <div class="flex items-center justify-between px-3 py-1 bg-zinc-950 border-b border-zinc-800">
            <span class="text-[10px] text-zinc-500 font-mono font-bold tracking-widest uppercase">SYSTEM_CALC</span>
            <span class="text-[10px] text-emerald-500/50" v-if="isHovered">ACTIVE</span>
        </div>

        <!-- Display -->
        <div class="p-3 text-right font-mono bg-zinc-900/30">
            <div class="text-zinc-600 text-[10px] h-3">{{ historyValue }}</div>
            <div class="text-2xl text-emerald-400 font-bold tracking-wider truncate">{{ displayValue || '0' }}</div>
        </div>

        <!-- Keypad -->
        <div class="grid grid-cols-4 gap-px bg-zinc-800 border-t border-zinc-800">
            <button @click="clear" class="btn col-span-2 text-red-400 hover:bg-red-900/20">AC</button>
            <button @click="del" class="btn text-zinc-400">DEL</button>
            <button @click="chooseOperation('/')" class="btn op">÷</button>
            
            <button @click="append(7)" class="btn">7</button><button @click="append(8)" class="btn">8</button><button @click="append(9)" class="btn">9</button>
            <button @click="chooseOperation('*')" class="btn op">×</button>
            
            <button @click="append(4)" class="btn">4</button><button @click="append(5)" class="btn">5</button><button @click="append(6)" class="btn">6</button>
            <button @click="chooseOperation('-')" class="btn op">-</button>
            
            <button @click="append(1)" class="btn">1</button><button @click="append(2)" class="btn">2</button><button @click="append(3)" class="btn">3</button>
            <button @click="chooseOperation('+')" class="btn op">+</button>
            
            <button @click="append(0)" class="btn col-span-2">0</button>
            <button @click="append('.')" class="btn">.</button>
            <button @click="compute" class="btn bg-emerald-900/20 text-emerald-400 hover:bg-emerald-500/20">=</button>
        </div>
  </div>
</template>

<style scoped>
.btn { @apply h-10 bg-zinc-950 text-zinc-300 font-mono text-sm hover:bg-zinc-900 transition active:bg-zinc-800 outline-none; }
.op { @apply text-emerald-500/80 bg-zinc-900/50; }
</style>