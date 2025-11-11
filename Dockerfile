# --- Build stage ---
    FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
    WORKDIR /src
    
    # Copy csproj and restore dependencies
    COPY *.csproj ./
    RUN dotnet restore
    
    # Copy the rest of the source code and build it
    COPY . .
    RUN dotnet publish -c Release -o /app/out
    
    # --- Runtime stage ---
    FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
    WORKDIR /app
    
    # Copy build output
    COPY --from=build /app/out .
    
    # Configure environment
    ENV ASPNETCORE_URLS=http://+:8080
    ENV ASPNETCORE_ENVIRONMENT=Development
    
    # Expose the app port
    EXPOSE 8080
    
    # Start your app
    ENTRYPOINT ["dotnet", "backend.dll"]
    