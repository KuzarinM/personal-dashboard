// src/config.js
export const categories = [
  {
    title: 'Media & Entertainment',
    items: [
      { 
        name: 'Jellyfin', 
        url: 'https://jellyfin.lan', 
        icon: 'https://cdn.jsdelivr.net/gh/walkxcode/dashboard-icons/png/jellyfin.png',
        desc: 'Кино и сериалы' 
      },
      { 
        name: 'Drop (Steam OSS)', 
        url: 'https://drop.lan', 
        icon: 'https://cdn.jsdelivr.net/gh/walkxcode/dashboard-icons/png/steam.png',
        desc: 'Игры' 
      },
    ]
  },
  {
    title: 'Infrastructure',
    items: [
      { 
        name: 'KubeSphere', 
        url: 'https://ks.lan', 
        icon: 'https://cdn.jsdelivr.net/gh/walkxcode/dashboard-icons/png/kubesphere.png',
        desc: 'K8s Cluster'
      },
      { 
        name: 'Proxmox', 
        url: 'https://192.168.1.10:8006', 
        icon: 'https://cdn.jsdelivr.net/gh/walkxcode/dashboard-icons/png/proxmox.png',
        desc: 'Virtualization (Local)'
      },
    ]
  },
  {
    title: 'Apps',
    items: [
      { 
        name: 'File Transfer', 
        url: 'https://files.lan', 
        icon: 'https://cdn.jsdelivr.net/gh/walkxcode/dashboard-icons/png/filebrowser.png', // Пример иконки
        desc: 'Перекинуть файлы'
      },
      { 
        name: 'My AppStore', 
        url: 'https://store.lan', 
        icon: 'https://cdn.jsdelivr.net/gh/walkxcode/dashboard-icons/png/appstore.png',
        desc: 'Софт'
      },
    ]
  },
  {
    title: 'Communication',
    items: [
      { 
        name: 'Element', 
        url: 'https://element.lan', 
        icon: 'https://cdn.jsdelivr.net/gh/walkxcode/dashboard-icons/png/element.png',
        desc: 'Matrix Chat'
      },
      { 
        name: 'Jitsi', 
        url: 'https://meet.lan', 
        icon: 'https://cdn.jsdelivr.net/gh/walkxcode/dashboard-icons/png/jitsi.png',
        desc: 'Видеозвонки'
      },
    ]
  }
]

export const events = [
  {
    name: 'Новый Год',
    date: '2026-01-01T00:00:00'
  },
  {
    name: 'Оплата Сервера',
    date: '2025-12-15T12:00:00', // Пример даты
    icon: '💸'
  },
  {
    name: 'Релиз Проекта',
    date: '2025-11-30T18:00:00',
    icon: '🚀'
  }
]