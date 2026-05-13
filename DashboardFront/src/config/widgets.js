import WeatherWidget from '@/components/WeatherWidget.vue'
import TelegramWidget from '@/components/TelegramWidget.vue'
import EventsWidget from '@/components/EventsWidget.vue'
import EmailWidget from '@/components/EmailWidget.vue'
import CryptoWidget from '@/components/CryptoWidget.vue'
import HackerNewsWidget from '@/components/HackerNewsWidget.vue'
import CalculatorWidget from '@/components/CalculatorWidget.vue'
import FiatWidget from '@/components/FiatWidget.vue'
import RemindersWidget from '@/components/RemindersWidget.vue'
import MonitoringWidget from '@/components/MonitoringWidget.vue'
import UserStatusWidget from '@/components/UserStatusWidget.vue'
import TimeTrackingWidget from '@/components/TimeTrackingWidget.vue'

export const widgetRegistry = {
    // Alt + W
    weather: { comp: WeatherWidget, name: 'WEATHER', icon: '🌤', shortcut: 'KeyW', keyChar: 'W' },
    
    // Alt + T
    telegram: { comp: TelegramWidget, name: 'TELEGRAM', icon: '✈️', shortcut: 'KeyT', keyChar: 'T' },
    
    // Alt + E
    events: { comp: EventsWidget, name: 'EVENTS', icon: '📅', shortcut: 'KeyE', keyChar: 'E' },
    
    // Alt + M (Mail)
    email: { comp: EmailWidget, name: 'EMAIL', icon: '✉️', shortcut: 'KeyM', keyChar: 'M' },
    
    // Alt + B (Blockchain/Bitcoin) - C занято калькулятором
    crypto: { comp: CryptoWidget, name: 'CRYPTO', icon: '💰', shortcut: 'KeyB', keyChar: 'B' },
    
    // Alt + N (News)
    hackernews: { comp: HackerNewsWidget, name: 'NEWS', icon: 'Y', shortcut: 'KeyN', keyChar: 'N' },
    
    // Alt + C
    calculator: { comp: CalculatorWidget, name: 'CALC', icon: '🧮', shortcut: 'KeyC', keyChar: 'C' },
    
    // Alt + F
    fiat: { comp: FiatWidget, name: 'FOREX', icon: '💱', shortcut: 'KeyF', keyChar: 'F' },

    // Alt + R
    reminders: { comp: RemindersWidget, name: 'REMINDERS', icon: '🔔', shortcut: 'KeyR', keyChar: 'R'},

    // Alt + M 
    monitoring: { comp: MonitoringWidget, name: 'MONITORING', icon: '📡', shortcut: 'KeyU', keyChar: 'U' },

    // Alt + P
    userstatus: { comp: UserStatusWidget, name: 'TEAM_STATUS', icon: '👥', shortcut: 'KeyP', keyChar: 'P' },

    // Alt + K (Time Tracker)
    timetracking: { comp: TimeTrackingWidget, name: 'TIME_TRACKER', icon: '⏱️', shortcut: 'KeyK', keyChar: 'K' }
}