FROM mcr.microsoft.com/dotnet/sdk:8.0

# Set working directory inside the container
WORKDIR /workspace

# Copy the project files into the container
COPY . .

# Restore dependencies
RUN dotnet restore src/ChatAPI.csproj

# Build the project
RUN dotnet build src/ChatAPI.csproj --configuration Release --no-restore

# Run the application
CMD ["dotnet", "run", "--project", "src/ChatAPI.csproj"]
