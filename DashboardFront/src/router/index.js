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
      beforeEnter: async (to, from, next) => {
        const token = localStorage.getItem('jwt_token')
        if (!token) {
          return next('/login')
        }
        
        try {
          // Запрашиваем список дашбордов
          const res = await fetch('/api/dashboards/list', {
            headers: { 'Authorization': `Bearer ${token}` }
          })
          
          if (res.ok) {
            const list = await res.json()
            if (list.length > 0) {
              return next(`/${list[0].id}`) // Прыгаем на первый доступный
            } else {
              // ИСПРАВЛЕНО: Если дашбордов нет вообще, но токен активен — 
              // автоматически создаем дефолтный дашборд "на лету" через API
              const createRes = await fetch('/api/dashboards', {
                method: 'POST',
                headers: {
                  'Authorization': `Bearer ${token}`,
                  'Content-Type': 'application/json'
                },
                body: JSON.stringify({ title: 'MAIN_DASHBOARD', isPublic: false })
              })
              
              if (createRes.ok) {
                const newDash = await createRes.ok ? await createRes.json() : null
                if (newDash && newDash.id) {
                  return next(`/${newDash.id}`) // Мгновенно переходим на созданный дашборд
                }
              }
            }
          }
        } catch (e) {
          console.error("Home redirect failed", e)
        }
        
        // Если произошла непредвиденная ошибка — отправляем на логин
        next('/login')
      }
    },,
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