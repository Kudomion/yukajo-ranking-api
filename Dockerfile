
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files
COPY YKJProAPI.sln ./
COPY YKJProAPI/YKJProAPI.csproj YKJProAPI/

# Restore dependencies
RUN dotnet restore YKJProAPI/YKJProAPI.csproj

# Copy application source
COPY . .

# Publish application
WORKDIR /src/YKJProAPI
RUN dotnet publish YKJProAPI.csproj -c Release -o /app/out --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/out .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "YKJProAPI.dll"]
