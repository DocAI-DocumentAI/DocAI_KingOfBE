FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Services/AI/AI.API/AI.API.csproj", "Services/AI/AI.API/"]
COPY ["Services/AI/AI.Infrastructure/AI.Infrastructure.csproj", "Services/AI/AI.Infrastructure/"]
COPY ["Services/AI/AI.Domain/AI.Domain.csproj", "Services/AI/AI.Domain/"]
RUN dotnet restore "Services/AI/AI.API/AI.API.csproj"
COPY . .
WORKDIR "/src/Services/AI/AI.API"
RUN dotnet build "./AI.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./AI.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AI.API.dll"]
