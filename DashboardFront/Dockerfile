# 1. Build
FROM node:20-alpine as build
WORKDIR /app
COPY package*.json ./
RUN npm install
COPY . .
RUN npm run build

# 2. Serve static
FROM nginx:alpine
# Копируем статику
COPY --from=build /app/dist /usr/share/nginx/html
# Копируем конфиг nginx (только для SPA роутинга, без proxy_pass!)
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]