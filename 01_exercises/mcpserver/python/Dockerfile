# Use an official Python image
FROM python:3.12-slim

# Set the working directory inside the container
WORKDIR /app

# Copy requirements first for better caching
COPY requirements.txt .

# Install dependencies
ARG PIP_INDEX_URL
RUN pip install --upgrade --no-cache-dir -r requirements.txt

# Copy the source code
COPY src/ ./src/

# Expose the required port
EXPOSE 8080

# Set the PYTHONPATH to ensure `src` is found
ENV PYTHONPATH=/app

# Run the HTTP MCP server
CMD ["python", "src/mcp_http_server.py"]