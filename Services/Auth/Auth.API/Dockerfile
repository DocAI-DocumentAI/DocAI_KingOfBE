FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
# Nếu không khai báo APP_UID từ ngoài, dòng này có thể gây lỗi:
# USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# ✅ Sửa dòng COPY này lại cho đúng với context hiện tại
COPY ["Auth.API.csproj", "."]
RUN dotnet restore "Auth.API.csproj"

# Copy toàn bộ source
COPY . .

# Build
RUN dotnet build "Auth.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Auth.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Auth.API.dll"]
