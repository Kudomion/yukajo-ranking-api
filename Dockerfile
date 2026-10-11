# 1. Build and restore dependencies
FROM ://microsoft.com AS build
WORKDIR /src

# Copy solution and project files to restore dependencies
COPY YKJProAPI.sln ./
COPY YKJProAPI/YKJProAPI.csproj YKJProAPI/
RUN dotnet restore YKJProAPI/YKJProAPI.csproj

# Copy the rest of the source files and build
COPY . .
WORKDIR /src/YKJProAPI
RUN dotnet publish -c Release -o /app/out

# 2. Setup the final runtime container
FROM ://microsoft.com AS runtime
WORKDIR /app
COPY --from=build /app/out .

# Tell .NET to listen to the dynamic port Railway assigns
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "YKJProAPI.dll"]