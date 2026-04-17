<script setup>
import { ref, watch } from 'vue'
import { request } from '@/api'

const props = defineProps({ isOpen: Boolean, dashboardId: Number })
const emit = defineEmits(['close', 'refresh'])

const step = ref('phone')
const phone = ref('')
const code = ref('')
const password = ref('')
const error = ref('')
const isSaving = ref(false)

watch(() => props.isOpen, (val) => {
  if (val) { step.value = 'phone'; error.value = ''; phone.value = ''; code.value = '' }
})

const sendPhone = async () => {
  isSaving.value = true; error.value = ''
  try {
    const res = await request(`/integrations/${props.dashboardId}/telegram/login-start`, { method: 'POST', body: JSON.stringify({ phone: phone.value }) })
    if (res.status === 'CODE_SENT') step.value = 'code'
    else error.value = res.status
  } catch (e) { error.value = e.message } finally { isSaving.value = false }
}

const sendCode = async () => {
  isSaving.value = true; error.value = ''
  try {
    const res = await request(`/integrations/${props.dashboardId}/telegram/login-complete`, { 
        method: 'POST', body: JSON.stringify({ phone: phone.value, code: code.value, password: password.value }) 
    })
    if (res.status === 'SUCCESS') { step.value = 'success'; emit('refresh') }
    else if (res.status === 'PASSWORD_NEEDED') step.value = 'password'
    else error.value = res.status
  } catch (e) { error.value = e.message } finally { isSaving.value = false }
}
</script>

<template>
  <div v-if="isOpen" class="fixed inset-0 z-50 flex items-center justify-center p-4">
    <div class="absolute inset-0 bg-black/90 backdrop-blur-sm" @click="$emit('close')"></div>
    <div class="relative bg-zinc-950 border border-sky-500/30 w-full max-w-sm flex flex-col rounded shadow font-sans overflow-hidden">
      
      <div class="p-3 border-b border-zinc-800 bg-zinc-900/50 flex justify-between items-center">
        <h2 class="text-sky-500 font-mono font-bold tracking-widest text-sm">SECURE_UPLINK</h2>
        <button @click="$emit('close')" class="text-zinc-500 hover:text-red-400 text-xs font-mono">[ESC]</button>
      </div>

      <div class="p-6 space-y-4">
        <div v-if="step === 'phone'">
            <label class="text-[9px] text-zinc-500 font-mono uppercase">Phone Number</label>
            <input v-model="phone" class="input-cyber text-sky-100 text-center" placeholder="+1234567890">
            <button @click="sendPhone" :disabled="isSaving" class="btn-cyber mt-4">{{ isSaving ? '...' : 'SEND CODE' }}</button>
        </div>

        <div v-else-if="step === 'code'">
            <label class="text-[9px] text-zinc-500 font-mono uppercase">Code</label>
            <input v-model="code" class="input-cyber text-sky-100 text-center tracking-[0.5em]" placeholder="12345">
            <button @click="sendCode" :disabled="isSaving" class="btn-cyber mt-4">{{ isSaving ? '...' : 'LOGIN' }}</button>
        </div>

        <div v-else-if="step === 'password'">
            <label class="text-[9px] text-amber-500 font-mono uppercase">2FA Password</label>
            <input v-model="password" type="password" class="input-cyber text-sky-100 text-center" placeholder="Password">
            <button @click="sendCode" :disabled="isSaving" class="btn-cyber mt-4 bg-amber-900/20 text-amber-400 border-amber-500/50">{{ isSaving ? '...' : 'UNLOCK' }}</button>
        </div>

        <div v-else-if="step === 'success'" class="text-center py-4">
            <div class="text-3xl mb-2">✅</div>
            <div class="text-emerald-500 font-mono text-xs">CONNECTION ESTABLISHED</div>
        </div>

        <div v-if="error" class="text-[9px] text-red-400 bg-red-900/20 p-2 text-center border border-red-500/20">{{ error }}</div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.input-cyber { @apply w-full bg-zinc-950 border border-zinc-800 p-2 text-xs font-mono outline-none focus:border-sky-500 transition; }
.btn-cyber { @apply w-full py-2 bg-sky-900/20 border border-sky-500/50 text-sky-400 hover:bg-sky-500 hover:text-black transition font-mono text-xs; }
</style>