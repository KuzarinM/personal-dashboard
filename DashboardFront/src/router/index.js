import { createRouter, createWebHistory } from 'vue-router'
import DashboardView from '../views/DashboardView.vue'
import LoginView from '../views/LoginView.vue'
import RegisterView from '../views/RegisterView.vue'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: '/login',
      name: 'login',
      component: LoginView
    },
    {
      path: '/register',
      name: 'register',
      component: RegisterView
    },
    {
      path: '/',
      name: 'home',
      // Вместо компонента — функция редиректа
      beforeEnter: async (to, from, next) => {
        const token = localStorage.getItem('jwt_token')
        try {
          // Делаем запрос к уже существующему списку
          // Используем fetch напрямую, чтобы не срабатывал редирект из api.js раньше времени
          const res = await fetch('/api/dashboards/list', {
            headers: token ? { 'Authorization': `Bearer ${token}` } : {}
          })
          
          if (res.ok) {
            const list = await res.json()
            if (list.length > 0) {
              return next(`/${list[0].id}`) // Прыгаем на первый доступный
            }
          }
        } catch (e) {
          console.error("Home redirect failed", e)
        }
        
        // Если дашбордов нет или ошибка — на логин
        next('/login')
      }
    },
    {
      path: '/:dashboardId(\\d+)',
      name: 'dashboard',
      component: DashboardView,
      props: route => ({ dashboardId: Number(route.params.dashboardId) })
    },
    {
      path: '/note/:guid',
      name: 'public-note',
      component: () => import('../views/PublicNoteView.vue'),
      props: true
    },
    {
      path: '/admin',
      name: 'admin',
      component: () => import('../views/AdminView.vue'),
      beforeEnter: (to, from, next) => {
          if (localStorage.getItem('is_admin') === 'true') next()
          else next('/')
      }
    },
  ]
})

export default router