<script setup>
import { ref, onMounted } from 'vue'

const stories = ref([])
const loading = ref(true)

const fetchStories = async () => {
  try {
    // 1. Берем ID топ-500 историй
    const resIds = await fetch('https://hacker-news.firebaseio.com/v0/topstories.json')
    const ids = await resIds.json()
    
    // Берем первые 5
    const top5Ids = ids.slice(0, 5)

    // 2. Параллельно грузим детали каждой истории
    const storyPromises = top5Ids.map(id => 
      fetch(`https://hacker-news.firebaseio.com/v0/item/${id}.json`).then(r => r.json())
    )
    
    const results = await Promise.all(storyPromises)
    stories.value = results
    loading.value = false
  } catch (e) {
    console.error(e)
    loading.value = false
  }
}

onMounted(() => {
  fetchStories()
  // Обновляем раз в 10 минут
  setInterval(fetchStories, 600000)
})
</script>

<template>
  <div class="bg-zinc-900/50 rounded-sm border border-zinc-800 p-4 font-mono text-xs space-y-3 relative overflow-hidden">
     <!-- Заголовок -->
     <div class="flex items-center justify-between text-zinc-500 uppercase tracking-widest border-b border-zinc-800 pb-2 mb-2">
        <span class="flex items-center gap-2">
            <span class="text-orange-500 font-bold">Y</span> HACKER_NEWS
        </span>
        <span class="text-emerald-500/30 text-[10px]">TOP_5</span>
     </div>

     <div v-if="loading" class="text-center py-4 text-zinc-600 animate-pulse">
        FETCHING_DATA...
     </div>

     <div v-else class="flex flex-col gap-3">
       <a 
         v-for="(story, index) in stories" 
         :key="story.id"
         :href="story.url || `https://news.ycombinator.com/item?id=${story.id}`"
         target="_blank"
         class="group flex gap-3 items-start hover:bg-zinc-800/50 p-1 -mx-1 rounded transition"
       >
         <span class="text-zinc-600 font-bold min-w-[12px]">{{ index + 1 }}.</span>
         <div class="flex flex-col">
            <span class="text-zinc-300 group-hover:text-emerald-400 transition leading-tight mb-1">
                {{ story.title }}
            </span>
            <div class="text-[10px] text-zinc-600 flex gap-2">
                <span>{{ story.score }} pts</span>
                <span>by {{ story.by }}</span>
            </div>
         </div>
       </a>
     </div>
  </div>
</template>