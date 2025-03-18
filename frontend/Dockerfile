# Use an official Node.js runtime as a parent image
FROM node:22.12.0-alpine
 
# Set the working directory
WORKDIR /app
 
# Copy package.json and install dependencies
COPY package.json /app/package.json
RUN npm install && npm install -g @angular/cli@19.0.6
 
# Copy the rest of the application code
COPY . /app

# Copy the startup script
COPY entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh
 
# Expose port 80
EXPOSE 80

# Start the Angular app on port 80
#CMD ["ng", "serve", "--host", "0.0.0.0", "--port", "80", "--disable-host-check"]

# Run the startup script
CMD ["/entrypoint.sh"]