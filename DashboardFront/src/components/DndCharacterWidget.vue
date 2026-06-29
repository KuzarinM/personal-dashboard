<script setup>
import { ref, computed, onMounted, onBeforeUnmount } from 'vue'
import { request } from '@/api'
import { useSignalR } from '@/composables/useSignalR'
import DndSettingsModal from '@/components/DndSettingsModal.vue'

const props = defineProps({
  dashboardId: Number
})

const { on, off } = useSignalR()

const character = ref(null)
const loading = ref(true)
const activeTab = ref('core')
const isSettingsOpen = ref(false)
const showCoinAdjust = ref(false)

const equippedSearch = ref('')
const backpackSearch = ref('')

// Полная каноничная структура по умолчанию из 18 навыков D&D 5e
const defaultCharacter = {
  name: 'Элион Вестник',
  race: 'Высший Эльф',
  gender: 'Мужской',
  class: 'Волшебник',
  level: 5,
  alignment: 'Хаотично-добрый',
  languagesRaw: 'Общий, Эльфийский, Драконий',
  hp: { current: 32, max: 38 },
  ac: 12,
  initiative: 3,
  speed: 30,
  passivePerception: 14,
  
  // Добавленные новые свойства для Кастеров
  spellAttackBonus: 7, // Магическая Сила (Бонус атаки)
  spellSaveDc: 15,     // Магическая Защита (Сложность спасброска)
  inspiration: false,  // Героическое вдохновение
  
  stats: [
    { name: 'СИЛ', value: 8, mod: -1 },
    { name: 'ЛОВ', value: 16, mod: 3 },
    { name: 'ТЕЛ', value: 14, mod: 2 },
    { name: 'ИНТ', value: 18, mod: 4 },
    { name: 'МУД', value: 12, mod: 1 },
    { name: 'ХАР', value: 10, mod: 0 }
  ],
  skills: [
    // Сила (СИЛ)
    { name: 'Атлетика', stat: 'СИЛ', bonus: -1, prof: false },
    // Ловкость (ЛОВ)
    { name: 'Акробатика', stat: 'ЛОВ', bonus: 3, prof: false },
    { name: 'Ловкость рук', stat: 'ЛОВ', bonus: 6, prof: true },
    { name: 'Скрытность', stat: 'ЛОВ', bonus: 6, prof: true },
    // Интеллект (ИНТ)
    { name: 'История', stat: 'ИНТ', bonus: 4, prof: false },
    { name: 'Магия', stat: 'ИНТ', bonus: 7, prof: true },
    { name: 'Природа', stat: 'ИНТ', bonus: 4, prof: false },
    { name: 'Расследование', stat: 'ИНТ', bonus: 4, prof: false },
    { name: 'Религия', stat: 'ИНТ', bonus: 4, prof: false },
    // Мудрость (МУД)
    { name: 'Внимательность (Восприятие)', stat: 'МУД', bonus: 4, prof: true },
    { name: 'Выживание', stat: 'МУД', bonus: 1, prof: false },
    { name: 'Медицина', stat: 'МУД', bonus: 1, prof: false },
    { name: 'Проницательность', stat: 'МУД', bonus: 4, prof: true },
    { name: 'Уход за животными', stat: 'МУД', bonus: 1, prof: false },
    // Харизма (ХАР)
    { name: 'Выступление', stat: 'ХАР', bonus: 0, prof: false },
    { name: 'Запугивание', stat: 'ХАР', bonus: 0, prof: false },
    { name: 'Обман', stat: 'ХАР', bonus: 0, prof: false },
    { name: 'Убеждение', stat: 'ХАР', bonus: 0, prof: false }
  ],
  spellSlots: [
    { level: 1, max: 4, used: 2 },
    { level: 2, max: 3, used: 1 },
    { level: 3, max: 2, used: 0 },
    { level: 4, max: 0, used: 0 },
    { level: 5, max: 0, used: 0 },
    { level: 6, max: 0, used: 0 },
    { level: 7, max: 0, used: 0 },
    { level: 8, max: 0, used: 0 },
    { level: 9, max: 0, used: 0 }
  ],
  spells: [
    { name: 'Щит', level: 1, isPrepared: true, isRitual: false, url: 'https://dnd5.club/spells/shield' },
    { name: 'Обнаружение магии', level: 1, isPrepared: false, isRitual: true, url: 'https://dnd5.club/spells/detect_magic' },
    { name: 'Зеркальное отражение', level: 2, isPrepared: true, isRitual: false, url: 'https://dnd5.club/spells/mirror_image' },
    { name: 'Огненный шар', level: 3, isPrepared: true, isRitual: false, url: 'https://dnd5.club/spells/fireball' }
  ],
  coins: { cp: 50, sp: 24, ep: 4, gp: 80, pp: 1 },
  inventory: [
    { name: 'Доспех Мага', tags: ['броня', 'магия'], qty: 1, url: 'https://dnd5.club/spells/mage_armor', desc: 'КД становится 13 + модификатор ЛОВ.', isEquipped: true },
    { name: 'Кинжал +1', tags: ['оружие', 'атака'], qty: 1, url: 'https://dnd5.club/weapons/dagger', desc: 'Дает +1 к атакам и урону.', isEquipped: true },
    { name: 'Фокусировка (кристалл)', tags: ['фокус', 'магия'], qty: 1, url: 'https://dnd5.club/items/focus', desc: 'Магический фокус.', isEquipped: false },
    { name: 'Книга заклинаний', tags: ['книга', 'магия'], qty: 1, url: 'https://dnd5.club/items/spellbook', desc: 'Кожаный фолиант формул.', isEquipped: false },
    { name: 'Зелье лечения', tags: ['зелье', 'лечение', 'расходник'], qty: 2, url: 'https://dnd5.club/items/potion_of_healing', desc: 'Действие: Восстанавливает 2d4+2 хитов.', isEquipped: false }
  ],
  rests: { shortRemaining: 2, shortMax: 2, longRemaining: 1, longMax: 1 },
  effects: [
    { name: 'Ускорение (Haste)', type: 'turns', value: 8 },
    { name: 'Сглаз (Hex)', type: 'shortRest', value: 0 },
    { name: 'Доспехи мага', type: 'longRest', value: 0 }
  ]
}

// СОХРАНЕНИЕ НА БЭКЕНД
const saveCharacterData = async () => {
  try {
    await request(`/dnd/${props.dashboardId}/character`, {
      method: 'PUT',
      body: JSON.stringify(character.value)
    })
  } catch (e) {
    console.error('Failed saving character data to backend', e)
  }
}

// ЗАГРУЗКА ДАННЫХ С БЭКЕНДА
const loadCharacterData = async () => {
  loading.value = true
  try {
    const parsed = await request(`/dnd/${props.dashboardId}/character`)
    
    // Если на бэкенде есть сохраненный персонаж
    if (parsed && parsed.name && parsed.stats) {
      // Обеспечиваем безопасное слияние новых полей (магические статы и вдохновение)
      parsed.inspiration = parsed.inspiration !== undefined ? parsed.inspiration : false
      parsed.spellAttackBonus = parsed.spellAttackBonus !== undefined ? parsed.spellAttackBonus : 7
      parsed.spellSaveDc = parsed.spellSaveDc !== undefined ? parsed.spellSaveDc : 15
      
      character.value = parsed
    } else {
      character.value = null
    }
  } catch (e) {
    console.error('Failed loading character data from backend', e)
    character.value = null
  } finally {
    loading.value = false
  }
}

const handleDndUpdate = () => {
  loadCharacterData()
}

onMounted(() => {
  loadCharacterData()
  on('dnd_character', handleDndUpdate)
})

onBeforeUnmount(() => {
  off('dnd_character', handleDndUpdate)
})

const handleConfigSave = (newData) => {
  character.value = newData
  saveCharacterData()
  makeSystemRoll('Конфигурация', 'Лист персонажа успешно сохранен на сервере!')
}

// --- ХИТЫ ---
const hpModifier = ref(1)
const adjustHp = (amount) => {
  if (!character.value) return
  let target = character.value.hp.current + amount
  if (target > character.value.hp.max) target = character.value.hp.max
  if (target < 0) target = 0
  character.value.hp.current = target
  saveCharacterData()
}

const hpPercent = computed(() => {
  if (!character.value) return 0
  const max = character.value.hp.max || 1
  return Math.max(0, Math.min(100, (character.value.hp.current / max) * 100))
})

// --- ВДОХНОВЕНИЕ ---
const toggleInspiration = () => {
  if (!character.value) return
  character.value.inspiration = !character.value.inspiration
  saveCharacterData()
}

// --- ЯЧЕЙКИ И ПОДГОТОВКА ЗАКЛИНАНИЙ ---
const toggleSpellSlot = (slotIdx, dotIdx) => {
  if (!character.value) return
  const slot = character.value.spellSlots[slotIdx]
  if (dotIdx <= slot.used) {
    slot.used--
  } else {
    slot.used++
  }
  saveCharacterData()
}

const activeSpellSlots = computed(() => {
  if (!character.value) return []
  return (character.value.spellSlots || []).filter(s => s.max > 0)
})

const togglePrep = (spell) => {
  spell.isPrepared = !spell.isPrepared
  saveCharacterData()
}

// --- ИНВЕНТАРЬ ---
const filteredEquipped = computed(() => {
  if (!character.value) return []
  const items = (character.value.inventory || []).filter(item => item.isEquipped)
  if (!equippedSearch.value.trim()) return items
  const q = equippedSearch.value.toLowerCase()
  return items.filter(item => {
    const nameMatch = item.name?.toLowerCase().includes(q)
    const descMatch = item.desc?.toLowerCase().includes(q)
    const tagMatch = item.tags ? item.tags.some(t => t.toLowerCase().includes(q)) : false
    return nameMatch || descMatch || tagMatch
  })
})

const filteredBackpack = computed(() => {
  if (!character.value) return []
  const items = (character.value.inventory || []).filter(item => !item.isEquipped)
  if (!backpackSearch.value.trim()) return items
  const q = backpackSearch.value.toLowerCase()
  return items.filter(item => {
    const nameMatch = item.name?.toLowerCase().includes(q)
    const descMatch = item.desc?.toLowerCase().includes(q)
    const tagMatch = item.tags ? item.tags.some(t => t.toLowerCase().includes(q)) : false
    return nameMatch || descMatch || tagMatch
  })
})

const toggleEquipState = (item) => {
  item.isEquipped = !item.isEquipped
  saveCharacterData()
}

const totalGoldValue = computed(() => {
  if (!character.value) return 0
  const c = character.value.coins || { cp: 0, sp: 0, ep: 0, gp: 0, pp: 0 }
  return (c.pp * 10) + c.gp + (c.ep * 0.5) + (c.sp * 0.1) + (c.cp * 0.01)
})

const consolidateCoins = () => {
  if (!character.value) return
  const c = character.value.coins
  let totalCp = (c.pp * 1000) + (c.gp * 100) + (c.ep * 50) + (c.sp * 10) + c.cp
  const pp = Math.floor(totalCp / 1000)
  totalCp %= 1000
  const gp = Math.floor(totalCp / 100)
  totalCp %= 100
  const sp = Math.floor(totalCp / 10)
  const cp = totalCp % 10
  character.value.coins = { cp, sp, ep: 0, gp, pp }
  saveCharacterData()
  makeSystemRoll('Обмен Валюты', `Деньги консолидированы: ${gp} зм, ${sp} см!`)
}

const modifyCoin = (type, amt) => {
  if (!character.value) return
  if (character.value.coins[type] + amt >= 0) {
    character.value.coins[type] += amt
    saveCharacterData()
  }
}

const getAttackBonus = (item) => {
  if (!character.value) return 0
  const strMod = character.value.stats.find(s => s.name === 'СИЛ')?.mod || 0
  const dexMod = character.value.stats.find(s => s.name === 'ЛОВ')?.mod || 0
  const isFinesse = item.tags ? item.tags.includes('фехтовальное') : false
  const bestMod = isFinesse ? Math.max(strMod, dexMod) : strMod
  const profBonus = 3 
  return bestMod + profBonus
}

// --- ДЕТАЛИ ---
const languagesList = computed(() => {
  if (!character.value || !character.value.languagesRaw) return []
  return character.value.languagesRaw.split(',').map(l => l.trim())
})

// --- ЭФФЕКТЫ ---
const newEffectName = ref('')
const newEffectType = ref('turns')
const newEffectValue = ref(5)

const addEffect = () => {
  if (!character.value || !newEffectName.value.trim()) return
  character.value.effects.push({
    name: newEffectName.value.trim(),
    type: newEffectType.value,
    value: newEffectType.value === 'turns' ? (parseInt(newEffectValue.value) || 1) : 0
  })
  newEffectName.value = ''
  newEffectValue.value = 5
  saveCharacterData()
}

const removeEffect = (idx) => {
  if (!character.value) return
  character.value.effects.splice(idx, 1)
  saveCharacterData()
}

const hasTurnEffects = computed(() => {
  return character.value && character.value.effects && character.value.effects.some(e => e.type === 'turns')
})

const nextTurn = () => {
  if (!character.value || !character.value.effects) return
  character.value.effects = character.value.effects.map(e => {
    if (e.type === 'turns') {
      return { ...e, value: e.value - 1 }
    }
    return e
  }).filter(e => e.type !== 'turns' || e.value > 0)
  saveCharacterData()
  makeSystemRoll('Раунд пройден', 'Счётчик времени эффектов уменьшен на 1.')
}

const getEffectLabel = (eff) => {
  if (eff.type === 'turns') return `${eff.value} ход.`
  if (eff.type === 'shortRest') return 'До кор. отд.'
  if (eff.type === 'longRest') return 'До дл. отд.'
  return 'Постоянно'
}

// --- ОТДЫХИ ---
const triggerShortRest = () => {
  if (!character.value) return
  if (character.value.rests.shortRemaining > 0) {
    character.value.rests.shortRemaining--
    const healAmount = Math.ceil(character.value.hp.max * 0.25)
    adjustHp(healAmount)
    character.value.effects = character.value.effects.filter(e => e.type !== 'shortRest')
    saveCharacterData()
    makeSystemRoll('Короткий Отдых', `Отдых завершен. Восстановлено ${healAmount} ХП.`)
  } else {
    makeSystemRoll('Предупреждение', 'Нет доступных коротких отдыхов!')
  }
}

const triggerLongRest = () => {
  if (!character.value) return
  character.value.hp.current = character.value.hp.max
  character.value.spellSlots.forEach(s => s.used = 0)
  character.value.rests.shortRemaining = character.value.rests.shortMax
  character.value.effects = character.value.effects.filter(e => e.type !== 'longRest' && e.type !== 'shortRest')
  saveCharacterData()
  makeSystemRoll('Длинный Отдых', 'Завершен длинный отдых. Временные эффекты сняты.')
}

// --- СИСТЕМА БРОСКОВ ---
const activeRoll = ref(null)
const makeRoll = (name, bonus) => {
  const d20 = Math.floor(Math.random() * 20) + 1
  const total = d20 + bonus
  let type = 'normal'
  if (d20 === 20) type = 'crit'
  if (d20 === 1) type = 'fumble'
  activeRoll.value = { name, d20, bonus, total, type }
}

const makeSystemRoll = (name, text) => {
  activeRoll.value = { name, d20: null, bonus: null, total: text, type: 'system' }
}
</script>

<template>
  <div class="bg-zinc-900/50 border border-zinc-800 rounded-sm overflow-hidden flex flex-col relative group/dnd min-h-[680px]">
    
    <DndSettingsModal 
      :is-open="isSettingsOpen" 
      :character-data="character" 
      @close="isSettingsOpen = false" 
      @save="handleConfigSave" 
    />

    <!-- Header -->
    <div class="flex items-center justify-between px-3 py-1.5 bg-zinc-950 border-b border-zinc-900">
      <span class="text-[10px] text-emerald-500 font-mono font-bold tracking-widest uppercase flex items-center gap-1.5">
        <span class="w-1.5 h-1.5 bg-emerald-500 rounded-full animate-pulse"></span>
        DND_CORE: {{ character ? character.name : 'ИНИЦИАЛИЗАЦИЯ...' }}
      </span>
      <button @click="isSettingsOpen = true" class="text-zinc-600 hover:text-emerald-400 opacity-0 group-hover/dnd:opacity-100 transition outline-none" title="Настроить Персонажа">⚙</button>
    </div>

    <!-- Заглушка Загрузки -->
    <div v-if="loading" class="flex-1 flex flex-col items-center justify-center p-6 text-emerald-500 font-mono text-xs animate-pulse min-h-[400px]">
      СИНХРОНИЗАЦИЯ С СЕРВЕРОМ...
    </div>

    <!-- Заглушка отсутствия данных -->
    <div v-else-if="!character" class="flex-1 flex flex-col items-center justify-center p-6 text-zinc-500 text-xs font-mono text-center gap-4 min-h-[400px]">
      <span class="text-4xl">🐉</span>
      <span class="uppercase tracking-widest text-[10px] font-bold text-zinc-400">Лист персонажа пуст</span>
      <p class="text-[9px] text-zinc-600 max-w-[200px] leading-normal uppercase">Данные на сервере отсутствуют. Нажмите кнопку ниже для создания вашей карточки героя.</p>
      <button @click="isSettingsOpen = true" class="px-4 py-2 bg-emerald-950/40 border border-emerald-800 text-emerald-400 text-[10px] font-bold hover:bg-emerald-500 hover:text-black transition duration-300 rounded-sm">
        ИНИЦИАЛИЗИРОВАТЬ ГЕРОЯ
      </button>
    </div>

    <!-- Основной рабочий блок (template v-else) -->
    <template v-else>
      <!-- Вкладки -->
      <div class="flex border-b border-zinc-900 text-[9px] font-mono bg-zinc-950/40 select-none border-b-zinc-800">
        <button @click="activeTab = 'core'" :class="activeTab === 'core' ? 'text-emerald-400 bg-zinc-900/50' : 'text-zinc-500 hover:text-zinc-300'" class="flex-1 py-1.5 text-center transition outline-none">ГЛАВНОЕ</button>
        <button @click="activeTab = 'spells'" :class="activeTab === 'spells' ? 'text-emerald-400 bg-zinc-900/50' : 'text-zinc-500 hover:text-zinc-300'" class="flex-1 py-1.5 text-center transition outline-none">МАГИЯ</button>
        <button @click="activeTab = 'bag'" :class="activeTab === 'bag' ? 'text-emerald-400 bg-zinc-900/50' : 'text-zinc-500 hover:text-zinc-300'" class="flex-1 py-1.5 text-center transition outline-none">ИНВЕНТАРЬ</button>
        <button @click="activeTab = 'details'" :class="activeTab === 'details' ? 'text-emerald-400 bg-zinc-900/50' : 'text-zinc-500 hover:text-zinc-300'" class="flex-1 py-1.5 text-center transition outline-none">ДЕТАЛИ</button>
      </div>

      <!-- Бросок кубиков -->
      <div v-if="activeRoll" class="absolute inset-x-0 top-14 mx-3 z-30 bg-zinc-950 border border-emerald-500/50 p-2 rounded shadow-2xl font-mono text-xs flex justify-between items-center animate-in fade-in zoom-in-95 duration-200">
        <div>
          <span class="text-zinc-600 uppercase text-[8px] block leading-none mb-1">{{ activeRoll.name }}</span>
          <span v-if="activeRoll.type !== 'system'" class="text-zinc-300">
            РЕЗУЛЬТАТ: <span class="font-bold text-emerald-400">{{ activeRoll.total }}</span> 
            <span class="text-zinc-500 text-[10px]"> (d20: {{ activeRoll.d20 }} + {{ activeRoll.bonus }})</span>
            <span v-if="activeRoll.type === 'crit'" class="text-amber-400 font-bold ml-1 text-[10px]">[КРИТ]</span>
            <span v-if="activeRoll.type === 'fumble'" class="text-red-500 font-bold ml-1 text-[10px]">[ПРОВАЛ]</span>
          </span>
          <span v-else class="text-zinc-300 text-[10px]">{{ activeRoll.total }}</span>
        </div>
        <button @click="activeRoll = null" class="text-zinc-500 hover:text-red-400 font-bold text-sm ml-2 outline-none">×</button>
      </div>

      <!-- ТЕЛО ВИДЖЕТА -->
      <div class="p-3 text-sm font-mono flex-1 flex flex-col min-h-0">
        
        <!-- ВКЛАДКА 1: ГЛАВНОЕ -->
        <div v-if="activeTab === 'core'" class="space-y-3">
          
          <!-- Красная Полоса Хитов (HP Bar) -->
          <div class="space-y-1">
            <div class="flex justify-between items-center text-[9px] text-zinc-500 uppercase font-bold leading-none">
              <span>Жизненная энергия (HP)</span>
              
              <!-- Индикатор Героического Вдохновения (Тоггл-точка) -->
              <div class="flex items-center gap-1.5 select-none">
                <span class="text-[7.5px] text-zinc-600 uppercase tracking-wider font-bold">Вдохновение:</span>
                <button @click="toggleInspiration"
                        class="w-2.5 h-2.5 rounded-full border transition-all duration-300 outline-none"
                        :class="character.inspiration ? 'bg-amber-500 border-amber-500 shadow-[0_0_8px_rgba(245,158,11,0.6)] animate-pulse' : 'bg-transparent border-zinc-700'"
                        title="Героическое вдохновение">
                </button>
              </div>

              <span class="text-zinc-300">{{ character.hp.current }} / {{ character.hp.max }}</span>
            </div>
            <div class="w-full bg-zinc-950 border border-zinc-900 h-2.5 rounded-full overflow-hidden relative">
              <div class="h-full bg-red-600 transition-all duration-300" :style="{ width: hpPercent + '%' }"></div>
            </div>
            <!-- Регулятор ХП -->
            <div class="flex items-center gap-1 bg-zinc-950/40 p-1 border border-zinc-900 rounded-sm">
              <span class="text-[8px] text-zinc-600 mr-auto uppercase">Регулятор:</span>
              <input v-model.number="hpModifier" type="number" class="w-8 bg-zinc-900 border border-zinc-800 text-[10px] text-center text-emerald-400 focus:outline-none focus:border-emerald-500/50 h-4 [appearance:textfield] [&::-webkit-outer-spin-button]:appearance-none [&::-webkit-inner-spin-button]:appearance-none">
              <button @click="adjustHp(-hpModifier)" class="w-4 h-4 flex items-center justify-center bg-red-950/50 border border-zinc-800 text-red-400 text-[10px] rounded-sm hover:bg-red-900/80 outline-none" title="Урон">-</button>
              <button @click="adjustHp(hpModifier)" class="w-4 h-4 flex items-center justify-center bg-emerald-950/50 border border-zinc-800 text-emerald-400 text-[10px] rounded-sm hover:bg-emerald-900/80 outline-none" title="Лечение">+</button>
            </div>
          </div>

          <!-- Компактное отображение ячеек -->
          <div class="bg-zinc-950/50 border border-zinc-900 p-2 rounded-sm space-y-1">
            <div class="text-[8px] text-zinc-500 uppercase font-bold leading-none">Ячейки заклинаний</div>
            <div class="flex flex-wrap gap-x-2 gap-y-1 text-[10px]">
              <div v-for="slot in activeSpellSlots" :key="slot.level" class="flex items-center gap-0.5 bg-zinc-900/60 px-1.5 py-0.5 rounded border border-zinc-800">
                <span class="text-zinc-500 font-bold text-[8px]">Кр.{{ slot.level }}:</span>
                <div class="flex gap-0.5">
                  <span v-for="n in slot.max" :key="n"
                        @click="toggleSpellSlot(character.spellSlots.indexOf(slot), n)"
                        class="w-1.5 h-1.5 rounded-full cursor-pointer transition-all"
                        :class="n <= (slot.max - slot.used) ? 'bg-emerald-500' : 'bg-zinc-800 border border-zinc-700'">
                  </span>
                </div>
              </div>
            </div>
          </div>

          <!-- КД + Остальные Параметры -->
          <div class="grid grid-cols-4 gap-1.5 text-center text-[9px] bg-zinc-950/30 p-1 border border-zinc-900 rounded-sm">
            <div class="border-r border-zinc-900 last:border-r-0">
              <div class="text-zinc-600 text-[7px] uppercase leading-none mb-0.5">КД (AC)</div>
              <span class="font-bold text-zinc-200 text-xs">{{ character.ac }}</span>
            </div>
            <div class="border-r border-zinc-900 last:border-r-0 cursor-pointer hover:bg-zinc-900/50" @click="makeRoll('Инициатива', character.initiative)">
              <div class="text-zinc-600 text-[7px] uppercase leading-none mb-0.5">Иниц.</div>
              <span class="font-bold text-emerald-400 text-xs hover:underline">{{ character.initiative >= 0 ? '+' : '' }}{{ character.initiative }}</span>
            </div>
            <div class="border-r border-zinc-900 last:border-r-0">
              <div class="text-zinc-600 text-[7px] uppercase leading-none mb-0.5">Скорость</div>
              <span class="font-bold text-zinc-300 text-xs">{{ character.speed }} фт</span>
            </div>
            <div>
              <div class="text-zinc-600 text-[7px] uppercase leading-none mb-0.5">Воспр.</div>
              <span class="font-bold text-zinc-300 text-xs">{{ character.passivePerception }}</span>
            </div>
          </div>

          <!-- Плитки Магической Силы и Магической Защиты (Для Кастеров) -->
          <div class="grid grid-cols-2 gap-1.5 text-center text-[9px] bg-zinc-950/30 p-1 border border-zinc-900 rounded-sm">
            <div class="border-r border-zinc-900">
              <div class="text-zinc-600 text-[7px] uppercase leading-none mb-0.5">Магическая Сила (Атака)</div>
              <span class="font-bold text-emerald-400 text-xs">{{ character.spellAttackBonus >= 0 ? '+' : '' }}{{ character.spellAttackBonus }}</span>
            </div>
            <div class="cursor-pointer hover:bg-zinc-900/50" @click="makeRoll('Сложность спасброска', character.spellSaveDc)">
              <div class="text-zinc-600 text-[7px] uppercase leading-none mb-0.5">Магическая Защита (Спас)</div>
              <span class="font-bold text-emerald-400 text-xs hover:underline">{{ character.spellSaveDc }}</span>
            </div>
          </div>

          <!-- Уменьшенные атрибуты -->
          <div class="grid grid-cols-6 gap-1 text-center">
            <div v-for="stat in character.stats" :key="stat.name" 
                 @click="makeRoll(stat.name, stat.mod)"
                 class="bg-zinc-950/50 border border-zinc-800 py-1 rounded-sm cursor-pointer hover:border-emerald-500/20 transition group">
              <span class="text-zinc-500 block font-bold text-[7px] leading-none">{{ stat.name }}</span>
              <span class="text-zinc-200 font-bold leading-none text-[10px] group-hover:text-emerald-400">{{ stat.value }}</span>
              <span class="text-emerald-500 block text-[7px] leading-none">({{ stat.mod >= 0 ? '+' : '' }}{{ stat.mod }})</span>
            </div>
          </div>

          <!-- Проверки навыков -->
          <div class="space-y-1 border-t border-zinc-900 pt-2">
            <div class="text-[8px] text-zinc-600 uppercase font-bold tracking-wider leading-none">Проверки Навыков</div>
            <div class="grid grid-cols-2 gap-1 max-h-[260px] overflow-y-auto custom-scrollbar pr-1">
              <div v-for="s in character.skills" :key="s.name" 
                   @click="makeRoll(`Навык: ${s.name}`, s.bonus)"
                   class="flex justify-between items-center p-1 bg-zinc-950/20 border border-zinc-800 hover:border-emerald-500/20 cursor-pointer rounded-sm transition text-[9px]">
                <span :class="s.prof ? 'text-emerald-400 font-bold' : 'text-zinc-500'" class="truncate">
                  {{ s.prof ? '● ' : '○ ' }}{{ s.name }}
                </span>
                <span class="text-zinc-400 font-bold font-mono">{{ s.bonus >= 0 ? '+' : '' }}{{ s.bonus }}</span>
              </div>
            </div>
          </div>
        </div>

        <!-- ВКЛАДКА 2: SPELLS -->
        <div v-if="activeTab === 'spells'" class="space-y-3">
          <!-- Ячейки по кругам -->
          <div class="space-y-1">
            <div class="text-[9px] text-zinc-500 uppercase tracking-widest border-b border-zinc-900 pb-0.5">Ячейки по кругам</div>
            <div class="space-y-1 max-h-[200px] overflow-y-auto custom-scrollbar pr-1">
              <div v-for="(slot, sIdx) in activeSpellSlots" :key="slot.level" class="flex justify-between items-center bg-zinc-950/40 p-1 border border-zinc-800 rounded-sm text-[10px]">
                <span class="text-zinc-400">Круг {{ slot.level }}</span>
                <div class="flex gap-1">
                  <span v-for="n in slot.max" :key="n"
                        @click="toggleSpellSlot(character.spellSlots.indexOf(slot), n)"
                        class="w-3 h-3 rounded-full border border-emerald-500/50 cursor-pointer flex items-center justify-center transition-all"
                        :class="n <= (slot.max - slot.used) ? 'bg-emerald-500/40' : 'bg-transparent border-dashed border-zinc-800'">
                  </span>
                </div>
              </div>
            </div>
          </div>

          <!-- Список известных заклинаний -->
          <div class="space-y-1">
            <div class="text-[9px] text-zinc-500 uppercase tracking-widest border-b border-zinc-900 pb-0.5">Известные заклинания</div>
            <div class="space-y-1 max-h-[360px] overflow-y-auto custom-scrollbar pr-1">
              <div v-for="spell in character.spells" :key="spell.name" 
                   class="p-1.5 bg-zinc-950/30 border border-zinc-800 rounded-sm flex items-center justify-between text-[11px]">
                <div class="flex items-center gap-1.5 min-w-0">
                  <button @click="togglePrep(spell)" 
                          class="text-[7px] font-bold px-1 py-0.5 rounded-sm transition-colors outline-none border"
                          :class="spell.isPrepared ? 'bg-emerald-950 text-emerald-400 border-emerald-800' : 'bg-zinc-900 text-zinc-600 border-zinc-800'">
                    ПОДГ
                  </button>
                  <a :href="spell.url" target="_blank" class="truncate font-bold text-zinc-200 hover:text-emerald-400 hover:underline" title="Справка в Wiki">
                    {{ spell.name }} <span class="text-zinc-600 font-normal text-[8px]">({{ spell.level }} кр.)</span>
                  </a>
                </div>
                <div class="flex items-center gap-1 flex-shrink-0">
                  <span v-if="spell.isRitual" class="text-[7px] font-bold text-blue-400 bg-blue-900/20 border border-blue-500/20 px-0.5 rounded">РИТ</span>
                  <button @click="makeRoll(`Заклинание: ${spell.name}`, character.stats.find(s => s.name === 'ИНТ')?.mod || 0)" class="text-[9px] hover:text-emerald-400 p-0.5 outline-none">🎲</button>
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- ВКЛАДКА 3: BAG -->
        <div v-if="activeTab === 'bag'" class="space-y-3 flex flex-col flex-1 min-h-[500px]">
          <!-- Компактный кошелек -->
          <div class="space-y-1 bg-zinc-950/40 p-1.5 border border-zinc-900 rounded-sm">
            <div class="flex justify-between items-center text-[9px] font-bold">
              <span class="text-zinc-500 uppercase">Баланс монет</span>
              <div class="flex items-center gap-2">
                <span class="text-amber-500">{{ totalGoldValue.toFixed(1) }} gp</span>
                <button @click="showCoinAdjust = !showCoinAdjust" class="text-zinc-500 hover:text-zinc-300 text-[8px] outline-none">
                  {{ showCoinAdjust ? '[Скрыть]' : '[+/-]' }}
                </button>
              </div>
            </div>

            <!-- Строка кошелька -->
            <div class="text-[10px] text-zinc-300 flex justify-between px-1 items-center">
              <span>{{ character.coins.pp }}<span class="text-zinc-500 text-[8px] ml-0.5">pp</span></span>
              <span>{{ character.coins.gp }}<span class="text-amber-500 text-[8px] ml-0.5">gp</span></span>
              <span>{{ character.coins.ep }}<span class="text-cyan-600 text-[8px] ml-0.5">ep</span></span>
              <span>{{ character.coins.sp }}<span class="text-zinc-400 text-[8px] ml-0.5">sp</span></span>
              <span>{{ character.coins.cp }}<span class="text-amber-700 text-[8px] ml-0.5">cp</span></span>
              <button @click="consolidateCoins" class="text-[8px] text-amber-500 hover:underline outline-none">Разменять</button>
            </div>

            <!-- Панель настройки -->
            <div v-if="showCoinAdjust" class="grid grid-cols-5 gap-1 pt-1.5 text-center text-[9px] border-t border-zinc-900/60 animate-in slide-in-from-top-1 duration-150">
              <div v-for="(val, coin) in character.coins" :key="coin" class="bg-zinc-900/60 p-0.5 rounded-sm border border-zinc-800">
                <span class="text-[7px] text-zinc-500 uppercase block leading-none font-bold">{{ coin }}</span>
                <div class="flex justify-center gap-1.5 mt-0.5">
                  <button @click="modifyCoin(coin, -1)" class="text-[8px] hover:text-red-500 outline-none">-</button>
                  <button @click="modifyCoin(coin, 1)" class="text-[8px] hover:text-emerald-500 outline-none">+</button>
                </div>
              </div>
            </div>
          </div>

          <!-- 1. РАЗДЕЛ: ЭКИПИРОВАНО -->
          <div class="space-y-1.5 flex flex-col max-h-[170px]">
            <div class="flex justify-between items-center text-[9px] text-zinc-500 uppercase font-bold leading-none border-b border-zinc-900 pb-1">
              <span class="flex items-center gap-1">🛡️ Экипировано (Оружие/Броня)</span>
              <span class="text-emerald-500/80 font-mono text-[8px]">{{ filteredEquipped.length }} шт.</span>
            </div>
            
            <div class="relative flex items-center bg-zinc-950 border border-zinc-900 rounded-sm px-2 focus-within:border-emerald-500/50 transition">
              <span class="text-zinc-600 text-[9px] mr-1.5">🔍</span>
              <input v-model="equippedSearch" placeholder="ПОИСК ПО ЭКИПИРОВКЕ..." class="w-full bg-transparent text-[9px] py-1 focus:outline-none placeholder:text-zinc-800 font-mono">
            </div>

            <div class="overflow-y-auto custom-scrollbar space-y-1 pr-1 flex-1">
              <div v-for="item in filteredEquipped" :key="item.name" class="p-1.5 bg-emerald-950/10 text-[10px] text-zinc-300 border border-emerald-900/20 rounded-sm flex flex-col gap-0.5">
                <div class="flex justify-between items-start">
                  <div class="truncate flex items-center gap-1.5 min-w-0">
                    <a v-if="item.url" :href="item.url" target="_blank" class="font-bold text-emerald-400 hover:underline truncate" title="Открыть Wiki">
                      {{ item.name }}
                    </a>
                    <span v-else class="font-bold text-emerald-400 truncate">{{ item.name }}</span>
                    
                    <!-- Интерактивный дайс атаки 🎲 для экипированного оружия -->
                    <button v-if="item.tags && (item.tags.includes('оружие') || item.tags.includes('атака'))"
                            @click="makeRoll(`Атака: ${item.name}`, getAttackBonus(item))"
                            class="text-[9px] text-zinc-500 hover:text-emerald-400 outline-none transition"
                            title="Бросить кубик атаки">
                      🎲
                    </button>
                  </div>
                  
                  <div class="flex items-center gap-1.5 flex-shrink-0">
                    <button @click="toggleEquipState(item)" class="text-[8px] text-zinc-500 hover:text-red-400 outline-none font-bold">
                      [СНЯТЬ]
                    </button>
                    <a v-if="item.url" :href="item.url" target="_blank" class="text-[9px] text-zinc-600 hover:text-emerald-400 px-1 font-bold">↗</a>
                  </div>
                </div>
                <p v-if="item.desc" class="text-[8px] text-zinc-500 leading-tight italic">{{ item.desc }}</p>
                <div v-if="item.tags && item.tags.length" class="flex flex-wrap gap-1 mt-0.5">
                    <span v-for="tag in item.tags" :key="tag" class="text-[7px] text-emerald-500/80 bg-emerald-950/10 border border-emerald-900/10 px-1 rounded-sm">
                        #{{ tag }}
                    </span>
                </div>
              </div>
              <div v-if="filteredEquipped.length === 0" class="text-center py-2 text-zinc-700 text-[8px] italic border border-dashed border-zinc-900 rounded-sm">
                Ничего не надето
              </div>
            </div>
          </div>

          <!-- 2. РАЗДЕЛ: РЮКЗАК -->
          <div class="space-y-1.5 flex-1 flex flex-col min-h-0 border-t border-zinc-900 pt-2">
            <div class="flex justify-between items-center text-[9px] text-zinc-500 uppercase font-bold leading-none pb-1">
              <span>🎒 Рюкзак</span>
              <span class="text-zinc-500 font-mono text-[8px]">{{ filteredBackpack.length }} шт.</span>
            </div>

            <div class="relative flex items-center bg-zinc-950 border border-zinc-900 rounded-sm px-2 focus-within:border-emerald-500/50 transition">
              <span class="text-zinc-600 text-[9px] mr-1.5">🔍</span>
              <input v-model="backpackSearch" placeholder="ПОИСК В РЮКЗАКЕ..." class="w-full bg-transparent text-[9px] py-1 focus:outline-none placeholder:text-zinc-800 font-mono">
            </div>

            <div class="overflow-y-auto custom-scrollbar space-y-1 pr-1 flex-1">
              <div v-for="item in filteredBackpack" :key="item.name" class="p-1.5 bg-zinc-950/20 text-[10px] text-zinc-300 border border-zinc-800 rounded-sm flex flex-col gap-0.5">
                <div class="flex justify-between items-start">
                  <div class="truncate">
                    <a v-if="item.url" :href="item.url" target="_blank" class="font-bold text-zinc-200 hover:text-emerald-400 hover:underline mr-1" title="Открыть Wiki">
                      {{ item.name }}
                    </a>
                    <span v-else class="font-bold text-zinc-200">{{ item.name }}</span>
                    <span class="text-zinc-500 text-[8px]">×{{ item.qty || 1 }}</span>
                  </div>
                  <button @click="toggleEquipState(item)" class="text-[8px] text-zinc-500 hover:text-emerald-400 ml-2 outline-none font-bold">
                    [НАДЕТЬ]
                  </button>
                </div>
                <p v-if="item.desc" class="text-[8px] text-zinc-500 leading-tight">{{ item.desc }}</p>
                <div v-if="item.tags && item.tags.length" class="flex flex-wrap gap-1 mt-0.5">
                  <span v-for="tag in item.tags" :key="tag" class="text-[7px] text-emerald-500/80 bg-emerald-950/10 border border-emerald-900/10 px-1 rounded-sm">
                    #{{ tag }}
                  </span>
                </div>
              </div>
              <div v-if="filteredBackpack.length === 0" class="text-center py-4 text-zinc-600 text-[9px] italic border border-dashed border-zinc-900 rounded-sm">
                Рюкзак пуст
              </div>
            </div>
          </div>
        </div>

        <!-- ВКЛАДКА 4: DETAILS -->
        <div v-if="activeTab === 'details'" class="space-y-3 animate-in fade-in duration-150 flex flex-col min-h-[500px]">
          <!-- Детали -->
          <div class="bg-zinc-950/50 p-2 border border-zinc-900 rounded-sm text-[10px] space-y-1 leading-normal text-zinc-400">
            <div><span class="text-zinc-500">РАСА:</span> <span class="text-zinc-200 font-bold">{{ character.race }}</span></div>
            <div><span class="text-zinc-500">ПОЛ:</span> <span class="text-zinc-200 font-bold">{{ character.gender }}</span></div>
            <div><span class="text-zinc-500">КЛАСС:</span> <span class="text-zinc-200">{{ character.class }} (ур. {{ character.level }})</span></div>
            <div><span class="text-zinc-500">МИРОВОЗЗРЕНИЕ:</span> <span class="text-amber-500 font-bold">{{ character.alignment }}</span></div>
            <div class="flex flex-wrap gap-1 pt-1 items-center">
              <span class="text-zinc-500">ЯЗЫКИ:</span>
              <span v-for="lang in languagesList" :key="lang" class="bg-zinc-900 border border-zinc-800 text-[8px] text-zinc-400 px-1.5 rounded">{{ lang }}</span>
            </div>
          </div>

          <!-- Отдых -->
          <div class="bg-zinc-950/50 p-2 border border-zinc-900 rounded-sm space-y-2">
            <div class="flex justify-between text-[9px] text-zinc-500 uppercase font-bold leading-none border-b border-zinc-900 pb-1">
              <span>Доступные отдыха</span>
              <span>КОР.: {{ character.rests.shortRemaining }}/{{ character.rests.shortMax }}</span>
            </div>
            <div class="flex gap-2">
              <button @click="triggerShortRest" class="flex-1 py-1.5 text-[9px] bg-zinc-900 border border-zinc-800 text-zinc-300 hover:text-emerald-400 hover:border-emerald-500/50 transition outline-none rounded-sm">
                КОРОТКИЙ ОТДЫХ
              </button>
              <button @click="triggerLongRest" class="flex-1 py-1.5 text-[9px] bg-amber-950/30 border border-amber-900/40 text-amber-500 hover:bg-amber-500 hover:text-black transition outline-none rounded-sm">
                ДЛИННЫЙ ОТДЫХ
              </button>
            </div>
          </div>

          <!-- Список Эффектов -->
          <div class="space-y-1 flex-1 flex flex-col min-h-0">
            <div class="flex justify-between items-center border-b border-zinc-900 pb-1">
              <span class="text-[9px] text-zinc-500 uppercase tracking-widest">Активные эффекты</span>
              <button @click="nextTurn" 
                      :disabled="!hasTurnEffects"
                      :class="hasTurnEffects ? 'text-amber-500 hover:text-amber-400 cursor-pointer' : 'text-zinc-700 cursor-not-allowed'"
                      class="text-[9px] font-bold outline-none transition">
                [СДЕЛАТЬ ХОД >>]
              </button>
            </div>
            <div class="flex gap-1 pt-1.5 items-center">
              <input v-model="newEffectName" placeholder="Эффект..." class="flex-1 bg-zinc-950 border border-zinc-900 text-[9px] p-1.5 text-zinc-300 focus:outline-none font-mono">
              <select v-model="newEffectType" class="bg-zinc-950 border border-zinc-900 text-[9px] text-zinc-400 p-1.5 focus:outline-none cursor-pointer">
                <option value="turns">Ходы</option>
                <option value="shortRest">До кор.отд.</option>
                <option value="longRest">До дл.отд.</option>
                <option value="permanent">Постоянно</option>
              </select>
              <input v-if="newEffectType === 'turns'" v-model.number="newEffectValue" type="number" class="w-10 bg-zinc-950 border border-zinc-900 text-[9px] p-1.5 text-zinc-300 focus:outline-none h-7 [appearance:textfield] [&::-webkit-outer-spin-button]:appearance-none [&::-webkit-inner-spin-button]:appearance-none">
              <button @click="addEffect" class="bg-emerald-950 border border-emerald-800 text-emerald-400 text-[9px] p-1.5 hover:bg-emerald-500 hover:text-black font-bold outline-none rounded-sm">+</button>
            </div>
            <div class="overflow-y-auto custom-scrollbar pr-1 flex-1 space-y-1">
              <div v-for="(effect, eIdx) in character.effects" :key="effect.name" class="flex justify-between items-center text-[10px] bg-zinc-950/60 p-1 border border-zinc-800 rounded-sm">
                <span class="text-zinc-200 font-bold truncate">{{ effect.name }}</span>
                <div class="flex items-center gap-2">
                  <span class="text-emerald-400 text-[8px] font-bold bg-emerald-950/20 px-1 rounded-sm border border-emerald-900/20">
                    {{ getEffectLabel(effect) }}
                  </span>
                  <button @click="removeEffect(eIdx)" class="text-zinc-600 hover:text-red-500 font-bold text-xs outline-none">×</button>
                </div>
              </div>
            </div>
            <!-- Добавить эффект -->

          </div>
        </div>

      </div>
    </template>

  </div>
</template>

<style scoped>
.custom-scrollbar::-webkit-scrollbar { width: 3px; }
.custom-scrollbar::-webkit-scrollbar-thumb { background: #10b981; }
</style>