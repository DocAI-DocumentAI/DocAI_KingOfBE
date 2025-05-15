FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
# Nếu biến $APP_UID không được set từ ngoài, dòng này sẽ gây lỗi. Bạn có thể bỏ dòng này nếu không cần:
# USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# ✅ Sửa đường dẫn này cho đúng context hiện tại
COPY ["Document.API.csproj", "."]
RUN dotnet restore "Document.API.csproj"

# Copy toàn bộ source
COPY . .

RUN dotnet build "Document.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Document.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Document.API.dll"]
