# 使用 .NET 8.0 SDK 作為建置環境
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-backend

# 安裝 Node.js (需要用於建置 Angular 應用程式)
RUN curl -fsSL https://deb.nodesource.com/setup_18.x | bash - \
    && apt-get install -y nodejs

# 設定工作目錄
WORKDIR /src

# 複製專案檔案並還原相依性
COPY GcpFileMove.csproj .
COPY GcpFileMove.sln .
RUN dotnet restore

# 複製所有源碼
COPY . .

# 建置 Angular 應用程式
WORKDIR /src/ClientApp
RUN npm install
RUN npm run build -- --configuration production

# 建置 .NET 應用程式
WORKDIR /src
RUN dotnet publish -c Release -o /app/publish --no-restore

# 使用 .NET 8.0 runtime 作為最終映像
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final

# 安裝必要的系統相依性
RUN apt-get update && apt-get install -y \
    curl \
    && rm -rf /var/lib/apt/lists/*

# 建立非 root 使用者
RUN adduser --disabled-password --gecos '' appuser

# 設定工作目錄
WORKDIR /app

# 從建置階段複製應用程式
COPY --from=build-backend /app/publish .

# 設定檔案權限
RUN chown -R appuser:appuser /app
USER appuser

# 暴露埠號
EXPOSE 8080

# 設定環境變數
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080

# 建立 GCP 服務帳戶金鑰的目錄
RUN mkdir -p /app/gcp

# 設定啟動命令
ENTRYPOINT ["dotnet", "GcpFileMove.dll"]
