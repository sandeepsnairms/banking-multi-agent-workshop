# Stage 1: Build the Angular App
FROM node:18 AS builder
WORKDIR /app
COPY package*.json ./
RUN npm install
COPY . .  # Ensure all files are copied
RUN npm run build --configuration=production

# Stage 2: Serve App with Nginx
FROM nginx:alpine
COPY --from=builder /app/dist/multi-agent-app /usr/share/nginx/html
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
