import { ref } from 'vue'
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr'

// Singleton состояние
const connection = ref(null)
const isConnected = ref(false)
const listeners = new Map() // Map<string, Set<Function>>

export function useSignalR() {
    
    const start = async (dashboardId) => {
        if (connection.value && connection.value.state === "Connected") {
            await stop() 
        }

        connection.value = new HubConnectionBuilder()
            .withUrl("/api/hub/notifications", {
                accessTokenFactory: () => localStorage.getItem('jwt_token') || ''
            })
            .withAutomaticReconnect([0, 2000, 10000, 30000]) // Попытки: 0с, 2с, 10с, 30с
            .configureLogging(LogLevel.Warning)
            .build()

        // --- HANDLERS ---

        connection.value.on("InvalidateData", (target) => {
            console.log(`[SignalR] Update Signal: ${target}`)
            triggerListeners(target)
        })

        connection.value.on("ReceiveReminder", (data) => {
            triggerListeners('alarm', data)
        })

        // --- RECONNECTION LOGIC ---

        // 1. Соединение восстановилось
        connection.value.onreconnected(async (connectionId) => {
            console.log(`[SignalR] Reconnected! ID: ${connectionId}`)
            isConnected.value = true
            
            // А. Переподписываемся на группу дашборда (сервер мог перезагрузиться и забыть нас)
            try { 
                await connection.value.invoke("JoinDashboard", dashboardId.toString()) 
            } catch (e) {
                console.warn("[SignalR] Failed to re-join group:", e)
            }

            // Б. "Mass Refresh": Дергаем ВСЕХ слушателей
            // Это заставит каждый активный виджет (Telegram, Monitoring, Notes)
            // выполнить свой fetch-запрос, чтобы получить актуальные данные.
            console.log("[SignalR] Triggering global refresh...")
            listeners.forEach((callbacks) => {
                callbacks.forEach(cb => cb())
            })
        })

        // 2. В процессе переподключения (мигает желтым)
        connection.value.onreconnecting(() => {
            console.log("[SignalR] Connection lost. Reconnecting...")
            isConnected.value = false
        })

        // 3. Сдался (красный статус)
        connection.value.onclose(() => {
            console.log("[SignalR] Connection permanently closed.")
            isConnected.value = false
        })

        // --- START ---
        try {
            await connection.value.start()
            await connection.value.invoke("JoinDashboard", dashboardId.toString())
            isConnected.value = true
            console.log(`[SignalR] Connected to Dash #${dashboardId}`)
        } catch (e) {
            console.error("[SignalR] Connection Failed:", e)
            // Простейший ручной ретрай, если старт не удался сразу
            setTimeout(() => start(dashboardId), 5000) 
        }
    }

    const stop = async () => {
        if (connection.value) {
            try { await connection.value.stop() } catch {}
            connection.value = null
            isConnected.value = false
        }
    }

    // Вспомогательная функция вызова подписчиков
    const triggerListeners = (event, data = null) => {
        // Конкретные подписчики (например "telegram")
        if (listeners.has(event)) {
            listeners.get(event).forEach(cb => cb(data))
        }
        // Глобальные подписчики ("*")
        if (listeners.has('*')) {
            listeners.get('*').forEach(cb => cb(event, data))
        }
    }

    const on = (event, callback) => {
        if (!listeners.has(event)) listeners.set(event, new Set())
        listeners.get(event).add(callback)
    }

    const off = (event, callback) => {
        if (listeners.has(event)) {
            listeners.get(event).delete(callback)
        }
    }

    return { start, stop, on, off, isConnected }
}