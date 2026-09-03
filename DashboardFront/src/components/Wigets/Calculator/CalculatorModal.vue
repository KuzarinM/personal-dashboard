<script setup>
import { ref, onMounted, onUnmounted, computed } from 'vue'

const props = defineProps({ isOpen: Boolean })
const emit = defineEmits(['close'])

const current = ref('')
const previous = ref('')
const operation = ref(null)

// Позиция окна (по центру изначально)
const position = ref({ x: window.innerWidth / 2 - 160, y: 100 })
const isDragging = ref(false)
const dragOffset = ref({ x: 0, y: 0 })

// --- DRAG LOGIC ---
const startDrag = (e) => {
    isDragging.value = true
    dragOffset.value = {
        x: e.clientX - position.value.x,
        y: e.clientY - position.value.y
    }
    window.addEventListener('mousemove', onDrag)
    window.addEventListener('mouseup', stopDrag)
}

const onDrag = (e) => {
    if (!isDragging.value) return
    position.value = {
        x: e.clientX - dragOffset.value.x,
        y: e.clientY - dragOffset.value.y
    }
}

const stopDrag = () => {
    isDragging.value = false
    window.removeEventListener('mousemove', onDrag)
    window.removeEventListener('mouseup', stopDrag)
}

// --- CALC LOGIC ---
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

const handleKeydown = (e) => {
  if (!props.isOpen) return
  // Блокируем всплытие, чтобы не печаталось в редакторе (хотя фокус мы снимем в dashboard)
  e.stopPropagation() 
  
  if (e.key >= '0' && e.key <= '9') append(e.key)
  if (e.key === '.' || e.key === ',') append('.')
  if (e.key === '=' || e.key === 'Enter') { e.preventDefault(); compute(); }
  if (e.key === 'Backspace') clear()
  if (e.key === 'Escape') emit('close')
  if (['+','-','*','/'].includes(e.key)) { e.preventDefault(); chooseOperation(e.key); }
}

onMounted(() => window.addEventListener('keydown', handleKeydown))
onUnmounted(() => window.removeEventListener('keydown', handleKeydown))
</script>

<template>
  <!-- Убрали fixed inset-0 и затемнение. Теперь это просто div -->
  <div 
    v-if="isOpen" 
    class="fixed z-50 w-full max-w-xs bg-zinc-950 border border-emerald-500/30 shadow-[0_0_50px_rgba(16,185,129,0.15)] rounded-sm overflow-hidden flex flex-col"
    :style="{ top: position.y + 'px', left: position.x + 'px' }"
  >
        <!-- Header (Draggable) -->
        <div 
            @mousedown="startDrag"
            class="flex justify-between items-center p-2 border-b border-zinc-800 bg-zinc-900/80 cursor-move select-none active:bg-zinc-800"
        >
            <span class="text-[10px] text-emerald-500/50 font-mono tracking-widest flex items-center gap-2">
                <span class="w-2 h-2 bg-emerald-500 rounded-full animate-pulse"></span>
                CALC_FLOAT
            </span>
            <!-- Кнопка закрытия (не драгается) -->
            <button @mousedown.stop @click="$emit('close')" class="text-zinc-600 hover:text-red-500 transition font-mono text-xs">[X]</button>
        </div>

        <div class="p-4 text-right font-mono bg-zinc-950">
            <div class="text-zinc-600 text-xs h-4">{{ historyValue }}</div>
            <div class="text-3xl text-emerald-400 font-bold tracking-wider truncate">{{ displayValue }}</div>
        </div>

        <div class="grid grid-cols-4 gap-px bg-zinc-800 border-t border-zinc-800">
            <button @click="clear" class="btn col-span-2 text-red-400 hover:bg-red-900/10">AC</button>
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
.btn { @apply h-12 bg-zinc-950 text-zinc-300 font-mono text-lg hover:bg-zinc-900 transition active:bg-zinc-800 outline-none; }
.op { @apply text-emerald-500/80 bg-zinc-900/50; }
</style>