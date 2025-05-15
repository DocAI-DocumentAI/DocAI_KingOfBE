FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Services/Notification/Notification.API/Notification.API.csproj", "Services/Notification/Notification.API/"]
COPY ["Services/Notification/Notification.Infrastructure/Notification.Infrastructure.csproj", "Services/Notification/Notification.Infrastructure/"]
COPY ["Services/Notification/Notification.Domain/Notification.Domain.csproj", "Services/Notification/Notification.Domain/"]
RUN dotnet restore "Services/Notification/Notification.API/Notification.API.csproj"
COPY . .
WORKDIR "/src/Services/Notification/Notification.API"
RUN dotnet build "./Notification.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Notification.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Notification.API.dll"]
