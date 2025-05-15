FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
# Nếu $APP_UID không được truyền từ bên ngoài thì nên comment hoặc kiểm tra biến.
# USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# ✅ Vì bạn đang ở trong chính thư mục chứa file .csproj
COPY ["Notification.API.csproj", "."]
RUN dotnet restore "Notification.API.csproj"

COPY . .

RUN dotnet build "Notification.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Notification.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Notification.API.dll"]
