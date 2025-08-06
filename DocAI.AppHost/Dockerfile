FROM mcr.microsoft.com/dotnet/runtime:9.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["DocAI.AppHost/DocAI.AppHost.csproj", "DocAI.AppHost/"]
COPY ["Services/Notification/Notification.API/Notification.API.csproj", "Services/Notification/Notification.API/"]
COPY ["Services/Notification/Notification.Infrastructure/Notification.Infrastructure.csproj", "Services/Notification/Notification.Infrastructure/"]
COPY ["Services/Notification/Notification.Domain/Notification.Domain.csproj", "Services/Notification/Notification.Domain/"]
COPY ["Services/Document/Document.API/Document.API.csproj", "Services/Document/Document.API/"]
COPY ["Services/Document/Document.Infrastructure/Document.Infrastructure.csproj", "Services/Document/Document.Infrastructure/"]
COPY ["Services/Document/Document.Domain/Document.Domain.csproj", "Services/Document/Document.Domain/"]
COPY ["Services/Auth/Auth.API/Auth.API.csproj", "Services/Auth/Auth.API/"]
COPY ["Services/Auth/Auth.Infrastructure/Auth.Infrastructure.csproj", "Services/Auth/Auth.Infrastructure/"]
COPY ["Services/Auth/Auth.Domain/Auth.Domain.csproj", "Services/Auth/Auth.Domain/"]
COPY ["Services/ChatBox/ChatBox.API/ChatBox.API.csproj", "Services/ChatBox/ChatBox.API/"]
COPY ["Services/ChatBox/ChatBox.Infrastructure/ChatBox.Infrastructure.csproj", "Services/ChatBox/ChatBox.Infrastructure/"]
COPY ["Services/ChatBox/ChatBox.Domain/ChatBox.Domain.csproj", "Services/ChatBox/ChatBox.Domain/"]
COPY ["ApiGateway/ApiGateway.csproj", "ApiGateway/"]

RUN dotnet restore "DocAI.AppHost/DocAI.AppHost.csproj"

COPY . .

WORKDIR "/src/DocAI.AppHost"
RUN dotnet build "./DocAI.AppHost.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
WORKDIR /src/DocAI.AppHost
RUN dotnet publish "./DocAI.AppHost.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DocAI.AppHost.dll"]
