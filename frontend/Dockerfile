# Use an official Node.js image as base
FROM node:18-alpine

# Set the working directory inside the container
WORKDIR /app

# Copy package.json and package-lock.json first to leverage Docker caching
COPY package*.json ./

# Install dependencies
RUN npm install --production

# Copy the rest of the application files
COPY . .

# Expose port (optional, if the app runs on a specific port)
EXPOSE 3000

# Set the command to run the application
CMD ["npm", "start"]
