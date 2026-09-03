<script setup>
import { ref, watch, computed } from 'vue'
import { request } from '@/api'

const props = defineProps({
  isOpen: Boolean,
  characterData: Object,
  dashboardId: Number
})

const emit = defineEmits(['close', 'save'])

const activeSettingsTab = ref('general')
const catalog = ref({ items: [], spells: [] })

// Состояние поискового интерфейса импорта
const isImportOpen = ref(false)
const importType = ref('items')
const importSearchQuery = ref('')

const loadCatalog = async () => {
  try {
    // ИСПРАВЛЕНО: запрашиваем каталог дашборда (учитывает публичный гостевой режим)
    catalog.value = await request(`/dnd/catalog?dashboardId=${props.dashboardId}`)
  } catch (e) {
    console.error('Failed reading dnd catalog from backend', e)
  }
}

// Конструктор чистого пустого шаблона листа персонажа
const createEmptyCharacter = () => ({
  name: 'Новый Персонаж',
  race: '',
  gender: '',
  class: '',
  level: 1,
  alignment: 'Нейтральный',
  languagesRaw: 'Общий',
  hp: { current: 10, max: 10 },
  ac: 10,
  initiative: 0,
  speed: 30,
  passivePerception: 10,
  spellAttackBonus: 7, 
  spellSaveDc: 15,     
  inspiration: false,  
  stats: [
    { name: 'СИЛ', value: 10, mod: 0 },
    { name: 'ЛОВ', value: 10, mod: 0 },
    { name: 'ТЕЛ', value: 10, mod: 0 },
    { name: 'ИНТ', value: 10, mod: 0 },
    { name: 'МУД', value: 10, mod: 0 },
    { name: 'ХАР', value: 10, mod: 0 }
  ],
  skills: [
    // Сила (СИЛ)
    { name: 'Атлетика', stat: 'СИЛ', bonus: 0, prof: false },
    // Ловкость (ЛОВ)
    { name: 'Акробатика', stat: 'ЛОВ', bonus: 0, prof: false },
    { name: 'Ловкость рук', stat: 'ЛОВ', bonus: 0, prof: false },
    { name: 'Скрытность', stat: 'ЛОВ', bonus: 0, prof: false },
    // Интеллект (ИНТ)
    { name: 'История', stat: 'ИНТ', bonus: 0, prof: false },
    { name: 'Магия', stat: 'ИНТ', bonus: 0, prof: false },
    { name: 'Природа', stat: 'ИНТ', bonus: 0, prof: false },
    { name: 'Расследование', stat: 'ИНТ', bonus: 0, prof: false },
    { name: 'Религия', stat: 'ИНТ', bonus: 0, prof: false },
    // Мудрость (МУД)
    { name: 'Внимательность (Восприятие)', stat: 'МУД', bonus: 0, prof: false },
    { name: 'Выживание', stat: 'МУД', bonus: 0, prof: false },
    { name: 'Медицина', stat: 'МУД', bonus: 0, prof: false },
    { name: 'Проницательность', stat: 'МУД', bonus: 0, prof: false },
    { name: 'Уход за животными', stat: 'МУД', bonus: 0, prof: false },
    // Харизма (ХАР)
    { name: 'Выступление', stat: 'ХАР', bonus: 0, prof: false },
    { name: 'Запугивание', stat: 'ХАР', bonus: 0, prof: false },
    { name: 'Обман', stat: 'ХАР', bonus: 0, prof: false },
    { name: 'Убеждение', stat: 'ХАР', bonus: 0, prof: false }
  ],
  spellSlots: Array.from({ length: 9 }, (_, i) => ({ level: i + 1, max: 0, used: 0 })),
  spells: [],
  coins: { cp: 0, sp: 0, ep: 0, gp: 0, pp: 0 },
  inventory: [],
  rests: { shortRemaining: 2, shortMax: 2, longRemaining: 1, longMax: 1 },
  effects: [],
  feats: []
})

// ИСПРАВЛЕНО: Форма инициализируется чистым шаблоном сразу, предотвращая undefined-ошибки во Vue шаблоне
const form = ref(createEmptyCharacter())

watch(() => props.isOpen, (val) => {
  if (val) {
    loadCatalog()
    isImportOpen.value = false
    importSearchQuery.value = ''
    
    let rawData = props.characterData ? JSON.parse(JSON.stringify(props.characterData)) : null
    if (!rawData || !rawData.name || !rawData.stats) {
      rawData = createEmptyCharacter()
    }

    // Восстанавливаем каноничный список из 18 навыков при необходимости
    const standardSkills = createEmptyCharacter().skills
    if (!rawData.skills || rawData.skills.length === 0) {
      rawData.skills = standardSkills
    } else {
      rawData.skills = standardSkills.map(defaultSkill => {
        const existing = rawData.skills.find(s => s.name.toLowerCase() === defaultSkill.name.toLowerCase())
        return existing ? existing : defaultSkill
      })
    }

    // Инициализация кастерских характеристик
    rawData.spellAttackBonus = rawData.spellAttackBonus !== undefined ? rawData.spellAttackBonus : 7
    rawData.spellSaveDc = rawData.spellSaveDc !== undefined ? rawData.spellSaveDc : 15
    rawData.inspiration = rawData.inspiration !== undefined ? rawData.inspiration : false

    // Инициализация Черт (Feats)
    rawData.feats = (rawData.feats || []).map(f => {
      return {
        name: f.name || '',
        desc: f.desc || ''
      }
    })

    // Формируем 9 уровней ячеек
    const currentSlots = rawData.spellSlots || []
    const fullSlots = []
    for (let i = 1; i <= 9; i++) {
      const existing = currentSlots.find(s => s.level === i)
      fullSlots.push(existing ? existing : { level: i, max: 0, used: 0 })
    }
    rawData.spellSlots = fullSlots

    // Преобразуем массив инвентаря
    rawData.inventory = (rawData.inventory || []).map(item => {
      return {
        name: item.name || '',
        qty: item.qty || item.quantity || 1,
        url: item.url || '',
        desc: item.desc || '',
        isEquipped: item.isEquipped !== undefined ? item.isEquipped : false,
        tagsRaw: item.tags ? item.tags.join(', ') : ''
      }
    })

    form.value = rawData
  }
})

// Настройка Инвентаря
const addInventoryItem = () => {
  form.value.inventory.push({ name: 'Новый предмет', qty: 1, url: '', desc: '', isEquipped: false, tagsRaw: 'снаряжение' })
}

const removeInventoryItem = (idx) => {
  form.value.inventory.splice(idx, 1)
}

// Настройка Заклинаний
const addSpell = () => {
  form.value.spells.push({ name: 'Новое заклинание', level: 1, isPrepared: false, isRitual: false, url: 'https://dnd5.club/spells' })
}

const removeSpell = (idx) => {
  form.value.spells.splice(idx, 1)
}

// Управление чертами и особенностями
const addFeat = () => {
  if (!form.value.feats) {
    form.value.feats = []
  }
  form.value.feats.push({ name: 'Новая черта', desc: '' })
}

const removeFeat = (idx) => {
  if (form.value.feats) {
    form.value.feats.splice(idx, 1)
  }
}

// --- УМНЫЙ ИМПОРТ ИЗ КАТАЛОГА С ПОИСКОМ ---
const openImportPanel = (type) => {
  importType.value = type
  importSearchQuery.value = ''
  isImportOpen.value = true
}

const filteredCatalogItems = computed(() => {
  const q = importSearchQuery.value.toLowerCase().trim()
  const items = catalog.value.items || []
  if (!q) return items
  return items.filter(item => {
    return item.name?.toLowerCase().includes(q) || 
           (item.desc && item.desc.toLowerCase().includes(q)) ||
           (item.tags && item.tags.some(t => t.toLowerCase().includes(q)))
  })
})

const filteredCatalogSpells = computed(() => {
  const q = importSearchQuery.value.toLowerCase().trim()
  const spells = catalog.value.spells || []
  if (!q) return spells
  return spells.filter(s => {
    return s.name?.toLowerCase().includes(q) || (s.desc && s.desc.toLowerCase().includes(q))
  })
})

const importItem = (template) => {
  form.value.inventory.push({
    name: template.name,
    qty: 1,
    url: template.url || '',
    desc: template.desc || '',
    isEquipped: false,
    tagsRaw: template.tags ? template.tags.join(', ') : 'снаряжение'
  })
  isImportOpen.value = false
}

const importSpell = (template) => {
  form.value.spells.push({
    name: template.name,
    level: template.level ?? 1,
    isPrepared: false,
    isRitual: !!template.isRitual,
    url: template.url || ''
  })
  isImportOpen.value = false
}

const syncWithGlobalCatalog = async (items, spells) => {
  try {
    // ИСПРАВЛЕНО: синхронизируем с каталогом этого дашборда напрямую
    const currentCat = await request(`/dnd/catalog?dashboardId=${props.dashboardId}`)
    let updated = false

    items.forEach(item => {
      const exists = currentCat.items.some(i => i.name.toLowerCase() === item.name.toLowerCase())
      if (!exists && item.name.trim() !== 'Новый предмет') {
        currentCat.items.push({
          name: item.name,
          desc: item.desc || '',
          url: item.url || '',
          tags: item.tags || []
        })
        updated = true
      }
    })

    spells.forEach(spell => {
      const exists = currentCat.spells.some(s => s.name.toLowerCase() === spell.name.toLowerCase())
      if (!exists && spell.name.trim() !== 'Новое заклинание') {
        currentCat.spells.push({
          name: spell.name,
          level: spell.level || 1,
          isRitual: !!spell.isRitual,
          url: spell.url || ''
        })
        updated = true
      }
    })

    if (updated) {
      await request(`/dnd/catalog?dashboardId=${props.dashboardId}`, {
        method: 'PUT',
        body: JSON.stringify(currentCat)
      })
    }
  } catch (e) {
    console.error('Failed syncing with global catalog', e)
  }
}

const handleSave = () => {
  const payload = JSON.parse(JSON.stringify(form.value))
  
  payload.inventory = payload.inventory.map(item => ({
    name: item.name,
    qty: parseInt(item.qty) || 1,
    url: item.url,
    desc: item.desc,
    isEquipped: !!item.isEquipped,
    tags: item.tagsRaw ? item.tagsRaw.split(',').map(t => t.trim().toLowerCase()).filter(Boolean) : []
  }))

  payload.feats = payload.feats.map(f => ({
    name: f.name,
    desc: f.desc
  }))

  syncWithGlobalCatalog(payload.inventory, payload.spells)

  emit('save', payload)
  emit('close')
}
</script>

<template>
  <div v-if="isOpen" class="fixed inset-0 z-[100] flex items-center justify-center p-4">
    <div class="absolute inset-0 bg-black/90 backdrop-blur-sm" @click="$emit('close')"></div>
    <div class="relative bg-zinc-950 border border-emerald-500/30 w-full max-w-3xl flex flex-col rounded shadow-[0_0_50px_rgba(16,185,129,0.15)] overflow-hidden max-h-[90vh]">
      
      <!-- Header -->
      <div class="p-3 border-b border-zinc-800 bg-zinc-900/50 flex justify-between items-center">
        <h2 class="text-emerald-500 font-mono font-bold tracking-widest text-sm uppercase">⚙️ CHARACTER_CONFIG_SYS</h2>
        <button @click="$emit('close')" class="text-zinc-500 hover:text-red-400 text-xs font-mono outline-none">[ESC]</button>
      </div>

      <!-- Внутренние вкладки настроек -->
      <div class="flex border-b border-zinc-800 text-[10px] font-mono bg-zinc-950/40 select-none">
        <button @click="activeSettingsTab = 'general'" :class="activeSettingsTab === 'general' ? 'text-emerald-400 bg-zinc-900/40' : 'text-zinc-500 hover:text-zinc-300'" class="flex-1 py-2.5 text-center outline-none transition">1. ПЕРСОНАЖ</button>
        <button @click="activeSettingsTab = 'stats'" :class="activeSettingsTab === 'stats' ? 'text-emerald-400 bg-zinc-900/40' : 'text-zinc-500 hover:text-zinc-300'" class="flex-1 py-2.5 text-center outline-none transition">2. ХАР-КИ & НАВЫКИ</button>
        <button @click="activeSettingsTab = 'spells'" :class="activeSettingsTab === 'spells' ? 'text-emerald-400 bg-zinc-900/40' : 'text-zinc-500 hover:text-zinc-300'" class="flex-1 py-2.5 text-center outline-none transition">3. МАГИЯ & ЯЧЕЙКИ</button>
        <button @click="activeSettingsTab = 'bag'" :class="activeSettingsTab === 'bag' ? 'text-emerald-400 bg-zinc-900/40' : 'text-zinc-500 hover:text-zinc-300'" class="flex-1 py-2.5 text-center outline-none transition">4. ИНВЕНТАРЬ</button>
      </div>

      <!-- Scrollable Form Body -->
      <div class="p-6 space-y-6 overflow-y-auto custom-scrollbar flex-1 bg-zinc-900/10 font-mono text-xs relative">
        
        <!-- ОВЕРЛЕЙ ИМПОРТА -->
        <div v-if="isImportOpen" class="absolute inset-0 bg-zinc-950 z-50 p-6 flex flex-col min-h-0 animate-in fade-in zoom-in-95 duration-200">
          <div class="flex justify-between items-center border-b border-zinc-800 pb-2 mb-4">
            <h3 class="text-emerald-400 font-bold uppercase text-[10px] tracking-widest">
              ИМПОРТ ИЗ ГЛОБАЛЬНОГО КАТАЛОГА ({{ importType === 'items' ? 'ПРЕДМЕТЫ' : 'ЗАКЛИНАНИЯ' }})
            </h3>
            <button @click="isImportOpen = false" class="text-zinc-500 hover:text-red-400 text-[10px] outline-none">[НАЗАД]</button>
          </div>

          <!-- Строка поиска -->
          <div class="relative flex items-center bg-zinc-900 border border-zinc-800 rounded-sm px-2 focus-within:border-emerald-500/50 transition mb-3">
            <span class="text-zinc-600 text-[10px] mr-1.5">🔍</span>
            <input v-model="importSearchQuery" placeholder="НАЧНИТЕ ВВОД ДЛЯ ФИЛЬТРАЦИИ..." class="w-full bg-transparent text-[10px] py-2 focus:outline-none placeholder:text-zinc-700 font-mono">
          </div>

          <!-- Результаты поиска предметов -->
          <div v-if="importType === 'items'" class="flex-1 overflow-y-auto custom-scrollbar space-y-2 pr-1">
            <div v-for="item in filteredCatalogItems" :key="item.name" class="p-2.5 bg-zinc-900 border border-zinc-800 rounded flex justify-between items-center">
              <div>
                <span class="font-bold text-zinc-200 block">{{ item.name }}</span>
                <span class="text-[9px] text-zinc-500 leading-normal">{{ item.desc || 'Нет описания' }}</span>
              </div>
              <button @click="importItem(item)" class="text-[9px] bg-emerald-950 border border-emerald-800 text-emerald-400 px-3 py-1 hover:bg-emerald-500 hover:text-black font-bold rounded-sm outline-none">
                ИМПОРТ
              </button>
            </div>
            <div v-if="filteredCatalogItems.length === 0" class="text-center py-8 text-zinc-700 italic">База данных пуста или ничего не найдено</div>
          </div>

          <!-- Результаты поиска заклинаний -->
          <div v-if="importType === 'spells'" class="flex-1 overflow-y-auto custom-scrollbar space-y-2 pr-1">
            <div v-for="spell in filteredCatalogSpells" :key="spell.name" class="p-2.5 bg-zinc-900 border border-zinc-800 rounded flex justify-between items-center">
              <div>
                <span class="font-bold text-zinc-200 block">{{ spell.name }} <span class="text-zinc-500 font-normal text-[9px]">({{ spell.level }} круг)</span></span>
                <span class="text-[9px] text-zinc-500 leading-normal">{{ spell.desc || 'Нет описания' }}</span>
              </div>
              <button @click="importSpell(spell)" class="text-[9px] bg-emerald-950 border border-emerald-800 text-emerald-400 px-3 py-1 hover:bg-emerald-500 hover:text-black font-bold rounded-sm outline-none">
                ИМПОРТ
              </button>
            </div>
            <div v-if="filteredCatalogSpells.length === 0" class="text-center py-8 text-zinc-700 italic">База данных пуста или ничего не найдено</div>
          </div>
        </div>

        <!-- ВКЛАДКА 1: ПЕРСОНАЖ -->
        <div v-if="activeSettingsTab === 'general'" class="space-y-4 animate-in fade-in duration-150">
          <div class="text-[10px] text-emerald-500 border-b border-zinc-800 pb-1 uppercase font-bold">Личные данные и боевой профиль</div>
          <div class="grid grid-cols-1 md:grid-cols-4 gap-3">
            <div>
              <label class="label-cyber">Имя Персонажа</label>
              <input v-model="form.name" class="input-cyber">
            </div>
            <div>
              <label class="label-cyber">Раса</label>
              <input v-model="form.race" class="input-cyber">
            </div>
            <div>
              <label class="label-cyber">Пол</label>
              <input v-model="form.gender" class="input-cyber" placeholder="Мужской/Женский">
            </div>
            <div>
              <label class="label-cyber">Класс</label>
              <input v-model="form.class" class="input-cyber">
            </div>
          </div>
          <div class="grid grid-cols-1 md:grid-cols-3 gap-3">
            <div>
              <label class="label-cyber">Уровень</label>
              <input v-model.number="form.level" type="number" class="input-cyber">
            </div>
            <div>
              <label class="label-cyber">Мировоззрение</label>
              <input v-model="form.alignment" class="input-cyber" placeholder="Хаотично-добрый">
            </div>
            <div>
              <label class="label-cyber">Языки (через запятую)</label>
              <input v-model="form.languagesRaw" class="input-cyber" placeholder="Общий, Эльфийский">
            </div>
          </div>
          <div class="grid grid-cols-2 md:grid-cols-6 gap-3 pt-2">
            <div>
              <label class="label-cyber">Макс. ХП</label>
              <input v-model.number="form.hp.max" type="number" class="input-cyber">
            </div>
            <div>
              <label class="label-cyber">Текущие ХП</label>
              <input v-model.number="form.hp.current" type="number" class="input-cyber">
            </div>
            <div>
              <label class="label-cyber">AC</label>
              <input v-model.number="form.ac" type="number" class="input-cyber">
            </div>
            <div>
              <label class="label-cyber">Инициатива</label>
              <input v-model.number="form.initiative" type="number" class="input-cyber">
            </div>
            <div>
              <label class="label-cyber">Скорость (фт)</label>
              <input v-model.number="form.speed" type="number" class="input-cyber">
            </div>
            <div>
              <label class="label-cyber">Пассивное Воспр.</label>
              <input v-model.number="form.passivePerception" type="number" class="input-cyber">
            </div>
          </div>

          <!-- Настройка магических показателей кастера -->
          <div class="grid grid-cols-1 md:grid-cols-2 gap-3 pt-2 border-t border-zinc-900">
            <div>
              <label class="label-cyber">Магическая Сила (Бонус атаки)</label>
              <input v-model.number="form.spellAttackBonus" type="number" class="input-cyber text-xs">
            </div>
            <div>
              <label class="label-cyber">Магическая Защита (Сложность спасброска)</label>
              <input v-model.number="form.spellSaveDc" type="number" class="input-cyber text-xs">
            </div>
          </div>

          <!-- Редактор Черт и Особенностей -->
          <div class="space-y-3 pt-4 border-t border-zinc-900">
            <div class="flex justify-between items-center pb-1">
              <span class="text-[10px] text-emerald-500 uppercase font-bold">Черты и особенности</span>
              <button @click="addFeat" class="text-[10px] text-emerald-400 hover:underline outline-none">[+] ДОБАВИТЬ ЧЕРТУ</button>
            </div>
            <div class="space-y-2 max-h-48 overflow-y-auto custom-scrollbar pr-1">
              <div v-for="(feat, idx) in form.feats" :key="idx" class="flex flex-col gap-2 bg-zinc-950 p-2.5 border border-zinc-800 rounded relative">
                <button @click="removeFeat(idx)" class="absolute top-2 right-2 text-zinc-600 hover:text-red-500 font-bold outline-none">×</button>
                <div class="grid grid-cols-1 gap-2 pr-6">
                  <div>
                    <label class="label-cyber text-[8px]">Название черты</label>
                    <input v-model="feat.name" class="input-cyber text-xs">
                  </div>
                  <div>
                    <label class="label-cyber text-[8px]">Описание / Эффект</label>
                    <input v-model="feat.desc" class="input-cyber text-xs" placeholder="Свойства и бонусы черты...">
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- ВКЛАДКА 2: ХАРАКТЕРИСТИКИ & НАВЫКИ -->
        <div v-if="activeSettingsTab === 'stats'" class="space-y-4 animate-in fade-in duration-150">
          <div class="text-[10px] text-emerald-500 border-b border-zinc-800 pb-1 uppercase font-bold">Базовые параметры характеристик</div>
          <div class="grid grid-cols-3 md:grid-cols-6 gap-2">
            <div v-for="stat in form.stats" :key="stat.name" class="bg-zinc-950 p-2 border border-zinc-800 rounded-sm">
              <div class="text-center font-bold text-[10px] text-zinc-500">{{ stat.name }}</div>
              <input v-model.number="stat.value" type="number" class="w-full text-center bg-transparent border-b border-zinc-800 focus:border-emerald-500/50 outline-none text-sm font-bold text-emerald-400 mt-1">
              <input v-model.number="stat.mod" type="number" class="w-full text-center bg-transparent text-[10px] text-zinc-500 outline-none mt-1" placeholder="Мод">
            </div>
          </div>

          <!-- Редактор Навыков -->
          <div class="text-[10px] text-emerald-500 border-b border-zinc-800 pb-1 uppercase font-bold pt-4">Модификаторы навыков и владение</div>
          <div class="grid grid-cols-1 md:grid-cols-2 gap-2 max-h-64 overflow-y-auto custom-scrollbar pr-1">
            <div v-for="skill in form.skills" :key="skill.name" class="bg-zinc-950 p-2 border border-zinc-800 rounded-sm flex items-center justify-between">
              <div class="flex items-center gap-2">
                <input type="checkbox" v-model="skill.prof" class="accent-emerald-500 w-3.5 h-3.5 cursor-pointer">
                <span class="text-xs text-zinc-300 font-bold">{{ skill.name }}</span>
                <span class="text-[9px] text-zinc-500">({{ skill.stat }})</span>
              </div>
              <div class="flex items-center gap-1.5">
                <span class="text-[9px] text-zinc-500">Бонус:</span>
                <input v-model.number="skill.bonus" type="number" class="w-10 bg-zinc-900 border border-zinc-800 text-xs text-center text-emerald-400 focus:outline-none focus:border-emerald-500/50 rounded-sm h-6">
              </div>
            </div>
          </div>
        </div>

        <!-- ВКЛАДКА 3: МАГИЯ & ЯЧЕЙКИ -->
        <div v-if="activeSettingsTab === 'spells'" class="space-y-5 animate-in fade-in duration-150">
          <div class="space-y-2">
            <div class="text-[10px] text-emerald-500 border-b border-zinc-800 pb-1 uppercase font-bold">Общее количество ячеек (1-9 Круги)</div>
            <div class="grid grid-cols-3 md:grid-cols-9 gap-2">
              <div v-for="slot in form.spellSlots" :key="slot.level" class="bg-zinc-950 p-1.5 border border-zinc-800 rounded-sm text-center">
                <label class="text-[8px] text-zinc-500 block uppercase font-bold">Круг {{ slot.level }}</label>
                <input v-model.number="slot.max" type="number" min="0" max="10" class="w-full bg-transparent text-center border-b border-zinc-800 focus:border-emerald-500/50 outline-none text-xs font-bold text-emerald-400 mt-1">
              </div>
            </div>
          </div>

          <div class="space-y-3">
            <div class="flex justify-between items-center border-b border-zinc-800 pb-1">
              <span class="text-[10px] text-emerald-500 uppercase font-bold">Заклинания персонажа</span>
              <div class="flex gap-2">
                <button @click="addSpell" class="text-[10px] text-emerald-400 hover:underline outline-none">[+] НОВОЕ</button>
                <button @click="openImportPanel('spells')" class="text-[10px] text-blue-400 hover:underline outline-none">[🎒 ИМПОРТ ИЗ КАТАЛОГА]</button>
              </div>
            </div>

            <div class="space-y-3">
              <div v-for="(s, idx) in form.spells" :key="idx" class="bg-zinc-950 p-3 border border-zinc-800 rounded-sm relative">
                <button @click="removeSpell(idx)" class="absolute top-2 right-2 text-zinc-600 hover:text-red-500 font-bold outline-none">×</button>
                <div class="grid grid-cols-1 md:grid-cols-4 gap-3 items-center">
                  <div>
                    <label class="label-cyber text-[8px]">Название заклинания</label>
                    <input v-model="s.name" class="input-cyber text-xs">
                  </div>
                  <div>
                    <label class="label-cyber text-[8px]">Круг (уровень)</label>
                    <input v-model.number="s.level" type="number" min="0" max="9" class="input-cyber text-xs">
                  </div>
                  <div class="flex gap-4 col-span-2 pt-3">
                    <label class="flex items-center gap-2 cursor-pointer text-[10px]">
                      <input type="checkbox" v-model="s.isPrepared" class="accent-emerald-500">
                      <span class="text-zinc-400">ПОДГОТОВЛЕНО</span>
                    </label>
                    <label class="flex items-center gap-2 cursor-pointer text-[10px]">
                      <input type="checkbox" v-model="s.isRitual" class="accent-emerald-500">
                      <span class="text-zinc-400">РИТУАЛ</span>
                    </label>
                  </div>
                </div>
                <div class="mt-2">
                  <label class="label-cyber text-[8px]">Ссылка на Wiki</label>
                  <input v-model="s.url" class="input-cyber text-xs text-emerald-500/80">
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- ВКЛАДКА 4: ИНВЕНТАРЬ -->
        <div v-if="activeSettingsTab === 'bag'" class="space-y-5 animate-in fade-in duration-150">
          <div class="space-y-3">
            <div class="flex justify-between items-center border-b border-zinc-800 pb-1">
              <span class="text-[10px] text-emerald-500 uppercase font-bold">Содержимое рюкзака (Инвентарь)</span>
              <div class="flex gap-2">
                <button @click="addInventoryItem" class="text-[10px] text-emerald-400 hover:underline outline-none">[+] ДОБАВИТЬ НОВЫЙ</button>
                <button @click="openImportPanel('items')" class="text-[10px] text-blue-400 hover:underline outline-none">[🎒 ИМПОРТ ИЗ КАТАЛОГА]</button>
              </div>
            </div>
            
            <div class="space-y-3 max-h-80 overflow-y-auto custom-scrollbar pr-1">
              <div v-for="(item, idx) in form.inventory" :key="idx" class="bg-zinc-950 p-3 border border-zinc-800 rounded-sm relative space-y-2">
                <button @click="removeInventoryItem(idx)" class="absolute top-2 right-2 text-zinc-600 hover:text-red-500 font-bold outline-none">×</button>
                
                <div class="grid grid-cols-1 md:grid-cols-4 gap-2">
                  <div class="md:col-span-2">
                    <label class="label-cyber">Название предмета</label>
                    <input v-model="item.name" class="input-cyber text-xs">
                  </div>
                  <div>
                    <label class="label-cyber">Количество</label>
                    <input v-model.number="item.qty" type="number" min="1" class="input-cyber text-xs">
                  </div>
                  <div>
                    <label class="label-cyber">Теги (через запятую)</label>
                    <input v-model="item.tagsRaw" class="input-cyber text-xs" placeholder="магия, фокус">
                  </div>
                </div>

                <div class="grid grid-cols-1 md:grid-cols-2 gap-2">
                  <div>
                    <label class="label-cyber">Ссылка на Wiki</label>
                    <input v-model="item.url" class="input-cyber text-xs text-emerald-500/80" placeholder="https://...">
                  </div>
                  <div class="flex flex-col justify-between">
                    <div>
                      <label class="label-cyber">Описание / Заметка</label>
                      <input v-model="item.desc" class="input-cyber text-xs" placeholder="Описание свойств...">
                    </div>
                    <label class="flex items-center gap-2 cursor-pointer text-[10px] mt-2">
                      <input type="checkbox" v-model="item.isEquipped" class="accent-emerald-500">
                      <span class="text-zinc-400">ЭКИПИРОВАНО</span>
                    </label>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>

      </div>

      <!-- Footer Actions -->
      <div class="p-4 border-t border-zinc-800 bg-zinc-950 flex justify-end gap-3">
        <button @click="$emit('close')" class="px-4 py-2 text-zinc-500 hover:text-zinc-300 font-mono text-xs transition outline-none">CANCEL</button>
        <button @click="handleSave" class="px-6 py-2 bg-emerald-900/20 border border-emerald-500/50 text-emerald-400 font-mono text-xs hover:bg-emerald-500 hover:text-black transition flex items-center gap-2 outline-none">
          SAVE CONFIG
        </button>
      </div>

    </div>
  </div>
</template>

<style scoped>
.label-cyber { @apply block text-[9px] text-zinc-500 font-mono uppercase mb-1; }
.input-cyber { @apply w-full bg-zinc-900 border border-zinc-800 p-2 text-xs font-mono text-emerald-100 focus:border-emerald-500/50 outline-none transition; }
.custom-scrollbar::-webkit-scrollbar { width: 4px; }
.custom-scrollbar::-webkit-scrollbar-thumb { background: #10b981; }
</style>