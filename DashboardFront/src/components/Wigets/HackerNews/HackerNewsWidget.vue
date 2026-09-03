<script setup>
import { ref, onMounted } from 'vue'

const stories = ref([])
const loading = ref(true)
const error = ref(false)

const fetchStories = async () => {
  try {
    // 1. Получаем ID лучших историй
    const idsRes = await fetch('https://hacker-news.firebaseio.com/v0/topstories.json')
    const ids = await idsRes.json()
    
    // Берем топ 5
    const top5 = ids.slice(0, 5)

    // 2. Параллельно грузим детали
    const promises = top5.map(id => 
        fetch(`https://hacker-news.firebaseio.com/v0/item/${id}.json`).then(r => r.json())
    )
    
    stories.value = await Promise.all(promises)
  } catch (e) {
    error.value = true
  } finally {
    loading.value = false
  }
}

onMounted(() => {
    fetchStories()
    // Обновление раз в 10 минут
    setInterval(fetchStories, 600000)
})
</script>

<template>
  <div class="space-y-2">
    <!-- Header -->
    <div class="flex items-center justify-between text-[10px] font-mono font-bold text-orange-500 uppercase tracking-widest border-b border-orange-500/20 pb-1">
        <span class="flex items-center gap-2">
            <span class="bg-orange-500 text-black px-1 font-bold">Y</span> NET_NEWS
        </span>
        <span v-if="error" class="text-red-500">ERR</span>
    </div>

    <!-- Loading -->
    <div v-if="loading" class="text-zinc-600 text-[10px] font-mono italic animate-pulse">
        SYNCING_FEED...
    </div>

    <!-- List -->
    <div v-else class="flex flex-col gap-2">
        <a 
            v-for="(story, idx) in stories" 
            :key="story.id"
            :href="story.url || `https://news.ycombinator.com/item?id=${story.id}`"
            target="_blank"
            class="group flex gap-3 items-start p-2 rounded hover:bg-zinc-900/50 border border-transparent hover:border-zinc-800 transition"
        >
            <span class="text-zinc-600 font-mono text-[10px] pt-0.5">{{ idx + 1 }}</span>
            <div class="flex flex-col">
                <span class="text-zinc-300 text-xs font-mono leading-tight group-hover:text-orange-400 transition">
                    {{ story.title }}
                </span>
                <span class="text-[9px] text-zinc-600 mt-1">
                    {{ story.score }} pts by {{ story.by }}
                </span>
            </div>
        </a>
    </div>
  </div>
</template>