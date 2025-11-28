# --- ЭТАП 1: Сборка Фронтенда ---
FROM node:20-alpine AS build-stage

WORKDIR /app

# Копируем файлы зависимостей
COPY package*.json ./

# Устанавливаем ВСЕ зависимости (включая devDependencies для Vite)
RUN npm install

# Копируем исходный код
COPY . .

# Собираем проект (результат будет в папке dist)
RUN npm run build

# --- ЭТАП 2: Запуск Сервера ---
FROM node:20-alpine AS production-stage

WORKDIR /app

# Копируем package.json, чтобы поставить только runtime-библиотеки
COPY package*.json ./

# Устанавливаем только то, что нужно для работы сервера (без Vite)
RUN npm install --omit=dev

# Копируем серверный скрипт и папку со скриптами (если есть)
COPY server.js .
COPY setup-tg.js . 

# Копируем собранный фронтенд из первого этапа
COPY --from=build-stage /app/dist ./dist

# Открываем порт
EXPOSE 3000

# Запускаем
CMD ["node", "server.js"]