// src/api.js
import router from './router'

const BASE_URL = '/api'

export const request = async (endpoint, options = {}) => {
  const token = localStorage.getItem('jwt_token')
  
  const headers = {
    'Content-Type': 'application/json',
    ...options.headers
  }

  if (token) {
    headers['Authorization'] = `Bearer ${token}`
  }

  const config = {
    ...options,
    headers
  }

  try {
    const response = await fetch(`${BASE_URL}${endpoint}`, config)

    if (response.status === 401) {
      // Если токен протух или его нет - чистим и редиректим, 
      // ТОЛЬКО если это не публичный дашборд (это проверит компонент)
      // Но для простоты: если API явно сказало 401, значит доступ запрещен.
      localStorage.removeItem('jwt_token')
      if (router.currentRoute.value.name !== 'login') {
        router.push({ name: 'login', query: { redirect: router.currentRoute.value.fullPath } })
      }
      throw new Error('Unauthorized')
    }

    if (!response.ok) {
      const errorData = await response.json().catch(() => ({}))
      throw new Error(errorData.message || errorData.error || `Error ${response.status}`)
    }

    // Если ответ пустой (204 No Content), не парсим JSON
    if (response.status === 204) return true

    return await response.json()
  } catch (e) {
    throw e
  }
}