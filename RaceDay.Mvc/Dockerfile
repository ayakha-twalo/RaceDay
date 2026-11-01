# Stage 1: Build the RaceDay MVC application.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

# Copy the project files.
COPY ["RaceDay.Mvc/RaceDay.Mvc.csproj", "RaceDay.Mvc/"]
COPY ["RaceDay.Contracts/RaceDay.Contracts.csproj", "RaceDay.Contracts/"]

# Restore the project dependencies.
RUN dotnet restore "RaceDay.Mvc/RaceDay.Mvc.csproj"

# Copy the remaining source files.
COPY . .

WORKDIR /src/RaceDay.Mvc

# Publish the MVC application.
RUN dotnet publish "RaceDay.Mvc.csproj" \
    --configuration Release \
    --output /app/publish \
    /p:UseAppHost=false


# Stage 2: Run the published application.
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final

WORKDIR /app

# Docker will expose the MVC application on port 8080.
EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080

# Copy the published application.
COPY --from=build /app/publish .

# Start RaceDay MVC.
ENTRYPOINT ["dotnet", "RaceDay.Mvc.dll"]