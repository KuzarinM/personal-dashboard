<script setup>
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { request } from '@/api'

const username = ref('')
const password = ref('')
const confirmPassword = ref('')
const error = ref('')
const isLoading = ref(false)
const router = useRouter()

const handleRegister = async () => {
  if (password.value !== confirmPassword.value) {
      error.value = 'Passwords do not match'
      return
  }
  
  isLoading.value = true
  error.value = ''
  
  try {
    await request('/auth/register', {
      method: 'POST',
      body: JSON.stringify({ username: username.value, password: password.value })
    })
    
    // После успеха сразу кидаем на логин
    alert('Identity created successfully. Initializing session...')
    router.push('/login')
  } catch (e) {
    error.value = e.message
  } finally {
    isLoading.value = false
  }
}
</script>

<template>
  <div class="min-h-screen flex items-center justify-center bg-zinc-950 text-zinc-300 font-mono relative overflow-hidden">
    <!-- Background Effect -->
    <div class="absolute inset-0 opacity-5 pointer-events-none" style="background-image: linear-gradient(0deg, transparent 24%, rgba(16, 185, 129, .3) 25%, rgba(16, 185, 129, .3) 26%, transparent 27%, transparent 74%, rgba(16, 185, 129, .3) 75%, rgba(16, 185, 129, .3) 76%, transparent 77%, transparent), linear-gradient(90deg, transparent 24%, rgba(255, 255, 255, .3) 25%, rgba(255, 255, 255, .3) 26%, transparent 27%, transparent 74%, rgba(255, 255, 255, .3) 75%, rgba(255, 255, 255, .3) 76%, transparent 77%, transparent); background-size: 50px 50px;"></div>
    
    <div class="w-full max-w-md p-8 border border-zinc-800 bg-zinc-900/50 backdrop-blur-sm rounded shadow-[0_0_50px_rgba(16,185,129,0.05)]">
      <h1 class="text-2xl font-bold text-emerald-500 mb-6 tracking-widest flex items-center gap-2">
        <span class="animate-pulse">+</span> NEW_IDENTITY
      </h1>
      
      <form @submit.prevent="handleRegister" class="space-y-4">
        <div>
          <label class="block text-xs text-zinc-500 mb-1 uppercase">Username</label>
          <input v-model="username" type="text" class="w-full bg-zinc-950 border border-zinc-800 p-3 text-emerald-100 focus:border-emerald-500/50 outline-none transition" autofocus placeholder="Create username">
        </div>
        
        <div>
          <label class="block text-xs text-zinc-500 mb-1 uppercase">Password</label>
          <input v-model="password" type="password" class="w-full bg-zinc-950 border border-zinc-800 p-3 text-emerald-100 focus:border-emerald-500/50 outline-none transition" placeholder="Create password">
        </div>

        <div>
          <label class="block text-xs text-zinc-500 mb-1 uppercase">Confirm Password</label>
          <input v-model="confirmPassword" type="password" class="w-full bg-zinc-950 border border-zinc-800 p-3 text-emerald-100 focus:border-emerald-500/50 outline-none transition" placeholder="Repeat password">
        </div>

        <div v-if="error" class="text-red-500 text-xs border border-red-900/30 bg-red-900/10 p-2 font-bold">
          ERROR: {{ error }}
        </div>

        <button 
           type="submit" 
           :disabled="isLoading"
          class="w-full bg-emerald-900/20 border border-emerald-500/30 text-emerald-400 py-3 hover:bg-emerald-500 hover:text-zinc-950 transition font-bold tracking-wider flex justify-center mt-2"
        >
          <span v-if="isLoading" class="animate-spin mr-2">/</span>
          {{ isLoading ? 'PROCESSING...' : 'INITIALIZE' }}
        </button>

        <div class="text-center pt-4 border-t border-zinc-800 mt-4">
            <router-link to="/login" class="text-xs text-zinc-500 hover:text-emerald-400 transition hover:underline underline-offset-4">
                << BACK TO LOGIN
            </router-link>
        </div>
      </form>
    </div>
  </div>
</template>