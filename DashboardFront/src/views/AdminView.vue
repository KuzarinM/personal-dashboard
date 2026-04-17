<script setup>
import { ref, onMounted, computed, shallowRef } from 'vue'
import { request } from '@/api'

// --- CODEMIRROR IMPORTS ---
import { Codemirror } from 'vue-codemirror'
import { sql, SQLite } from '@codemirror/lang-sql'
import { oneDark } from '@codemirror/theme-one-dark'

const activeTab = ref('users') 
const users = ref([])
const isLoading = ref(false)

// SQL State
const sqlQuery = ref('')
const sqlResult = ref(null)
const sqlError = ref('')
const dbSchema = ref({}) 

// Настройка расширений редактора
const editorExtensions = computed(() => {
    return [
        oneDark,
        sql({
            schema: dbSchema.value, // Передаем схему в плагин
            tables: Object.keys(dbSchema.value), // Список таблиц для подсказок
            dialect: SQLite,
            upperCaseKeywords: true
        })
    ]
})

// --- USERS TAB ---
const fetchUsers = async () => {
    isLoading.value = true
    try { users.value = await request('/admin/users') } 
    catch(e) { alert(e.message) }
    finally { isLoading.value = false }
}

const resetPassword = async (user) => {
    if(!confirm(`Reset password for ${user.username}?`)) return
    try {
        const res = await request(`/admin/users/${user.id}/reset-password`, { method: 'POST' })
        prompt(`Success! Copy new password for ${user.username}:`, res.newPassword)
    } catch(e) { alert(e.message) }
}

const deleteUser = async (user) => {
    if(!confirm(`DELETE USER ${user.username}?`)) return
    try {
        await request(`/admin/users/${user.id}`, { method: 'DELETE' })
        fetchUsers()
    } catch(e) { alert(e.message) }
}

// --- SQL TAB ---
const fetchSchema = async () => {
    try {
        dbSchema.value = await request('/admin/schema')
    } catch(e) { console.error("Schema fetch failed", e) }
}

const runSql = async () => {
    if(!sqlQuery.value) return
    isLoading.value = true
    sqlError.value = ''
    sqlResult.value = null
    
    try {
        const res = await request('/admin/sql', { 
            method: 'POST', 
            body: JSON.stringify({ query: sqlQuery.value }) 
        })
        sqlResult.value = res
    } catch(e) {
        sqlError.value = e.message
    } finally {
        isLoading.value = false
    }
}

// Init
onMounted(() => {
    fetchUsers()
    fetchSchema() // Грузим схему сразу
})
</script>

<template>
  <div class="min-h-screen bg-zinc-950 text-zinc-300 p-4 md:p-8 font-sans">
      
      <div class="max-w-6xl mx-auto">
          <!-- Header -->
          <div class="flex items-center justify-between mb-6 border-b border-zinc-800 pb-4">
              <h1 class="text-2xl font-mono font-bold text-red-500 tracking-widest flex items-center gap-2">
                  <span class="w-3 h-3 bg-red-500 rounded-full animate-pulse"></span>
                  ADMIN_PANEL
              </h1>
              <router-link to="/" class="text-zinc-500 hover:text-white font-mono text-xs">[EXIT TO SYSTEM]</router-link>
          </div>

          <!-- Tabs -->
          <div class="flex gap-4 mb-6">
              <button @click="activeTab='users'" class="px-4 py-2 border font-mono text-xs transition" :class="activeTab==='users' ? 'bg-red-900/20 border-red-500 text-red-400' : 'border-zinc-800 hover:border-zinc-600'">USER_DB</button>
              <button @click="activeTab='sql'" class="px-4 py-2 border font-mono text-xs transition" :class="activeTab==='sql' ? 'bg-red-900/20 border-red-500 text-red-400' : 'border-zinc-800 hover:border-zinc-600'">SQL_CONSOLE</button>
          </div>

          <!-- TAB: USERS -->
          <div v-if="activeTab === 'users'">
              <div class="bg-zinc-900 border border-zinc-800 rounded overflow-hidden">
                  <table class="w-full text-left text-xs font-mono">
                      <thead class="bg-zinc-950 text-zinc-500 border-b border-zinc-800">
                          <tr>
                              <th class="p-3">ID</th>
                              <th class="p-3">USERNAME</th>
                              <th class="p-3">ROLE</th>
                              <th class="p-3">STATUS</th>
                              <th class="p-3">DASHBOARDS</th>
                              <th class="p-3 text-right">ACTIONS</th>
                          </tr>
                      </thead>
                      <tbody>
                          <tr v-for="u in users" :key="u.id" class="border-b border-zinc-800/50 hover:bg-zinc-800/30">
                              <td class="p-3 text-zinc-600">{{ u.id }}</td>
                              <td class="p-3 font-bold text-zinc-300">{{ u.username }}</td>
                              <td class="p-3" :class="u.isAdmin ? 'text-red-400' : 'text-zinc-500'">{{ u.isAdmin ? 'ADMIN' : 'USER' }}</td>
                              <td class="p-3 text-zinc-400">{{ u.statusText || '-' }}</td>
                              <td class="p-3 text-zinc-400">{{ u.dashboardsCount }}</td>
                              <td class="p-3 text-right flex justify-end gap-2">
                                  <button @click="resetPassword(u)" class="text-amber-500 hover:text-amber-300 border border-amber-500/30 px-2 py-1 rounded hover:bg-amber-900/20">RESET PWD</button>
                                  <button @click="deleteUser(u)" class="text-red-600 hover:text-red-400 border border-red-600/30 px-2 py-1 rounded hover:bg-red-900/20">DELETE</button>
                              </td>
                          </tr>
                      </tbody>
                  </table>
              </div>
          </div>

          <!-- TAB: SQL (UPDATED) -->
          <div v-if="activeTab === 'sql'" class="flex flex-col gap-4 h-[calc(100vh-200px)]">
              
              <!-- Editor Area -->
              <div class="flex flex-col border border-zinc-800 rounded overflow-hidden bg-[#282c34] shadow-lg">
                  <!-- Toolbar -->
                  <div class="bg-zinc-900 p-2 flex justify-between items-center border-b border-zinc-800">
                      <div class="text-[10px] text-zinc-500 font-mono flex gap-2">
                          <span>SQLite Mode</span>
                          <span v-if="Object.keys(dbSchema).length > 0" class="text-emerald-500">Schema Loaded ({{ Object.keys(dbSchema).length }} tables)</span>
                      </div>
                      <button @click="runSql" :disabled="isLoading" class="px-4 py-1 bg-red-900/20 border border-red-500/50 text-red-400 font-bold font-mono text-xs hover:bg-red-500 hover:text-black transition flex items-center gap-2">
                          <span v-if="isLoading" class="animate-spin">/</span> EXECUTE (Ctrl+Enter)
                      </button>
                  </div>
                  
                  <!-- CodeMirror Component -->
                  <Codemirror
                    v-model="sqlQuery"
                    :extensions="editorExtensions"
                    :style="{ height: '150px', fontSize: '13px' }"
                    :autofocus="true"
                    :indent-with-tab="true"
                    :tab-size="2"
                    placeholder="SELECT * FROM Users..."
                    @keydown.ctrl.enter="runSql"
                  />
              </div>

              <!-- Results Area -->
              <div class="flex-1 bg-zinc-900 border border-zinc-800 rounded overflow-auto custom-scrollbar relative">
                  <div v-if="sqlError" class="p-4 text-red-500 font-mono text-xs whitespace-pre-wrap">{{ sqlError }}</div>
                  
                  <div v-else-if="sqlResult" class="min-w-full">
                      <!-- Table -->
                      <table v-if="Array.isArray(sqlResult)" class="w-full text-left text-xs font-mono border-collapse">
                          <thead class="bg-zinc-950 text-zinc-500 sticky top-0 z-10 shadow-sm">
                              <tr>
                                  <th v-for="(val, key) in sqlResult[0]" :key="key" class="p-2 border-b border-zinc-700 border-r border-zinc-800 last:border-r-0 whitespace-nowrap bg-zinc-950">{{ key }}</th>
                              </tr>
                          </thead>
                          <tbody>
                              <tr v-for="(row, idx) in sqlResult" :key="idx" class="hover:bg-zinc-800/50 transition">
                                  <td v-for="(val, key) in row" :key="key" class="p-2 border-b border-zinc-800/50 border-r border-zinc-800/30 last:border-r-0 whitespace-nowrap text-zinc-300">
                                      {{ val }}
                                  </td>
                              </tr>
                          </tbody>
                      </table>
                      <!-- Message -->
                      <div v-else class="p-4 text-emerald-400 font-mono">{{ sqlResult.message }}</div>
                  </div>
                  
                  <div v-else class="flex items-center justify-center h-full text-zinc-700 font-mono text-xs flex-col gap-2 opacity-50">
                      <span class="text-4xl">⌨️</span>
                      <span>READY TO QUERY</span>
                  </div>
              </div>
          </div>

      </div>
  </div>
</template>

<style scoped>
.custom-scrollbar::-webkit-scrollbar { width: 8px; height: 8px; }
.custom-scrollbar::-webkit-scrollbar-track { background: #18181b; }
.custom-scrollbar::-webkit-scrollbar-thumb { background: #3f3f46; border-radius: 0; }
.custom-scrollbar::-webkit-scrollbar-thumb:hover { background: #ef4444; }
.custom-scrollbar::-webkit-scrollbar-corner { background: #18181b; }
</style>