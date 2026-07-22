#!/bin/bash

# Docker 執行腳本 - GCP File Move 應用程式

# 設定變數
IMAGE_NAME="gcpfilemove"
CONTAINER_NAME="gcpfilemove-app"
PORT="8080"

# GCP 服務帳戶金鑰路徑 (請修改為您的實際路徑)
SERVICE_ACCOUNT_KEY_PATH="/path/to/your-service-account-key.json"

# 檢查服務帳戶金鑰檔案是否存在
if [ ! -f "$SERVICE_ACCOUNT_KEY_PATH" ]; then
    echo "❌ 錯誤: 找不到 GCP 服務帳戶金鑰檔案: $SERVICE_ACCOUNT_KEY_PATH"
    echo "請確認檔案路徑是否正確，或修改腳本中的 SERVICE_ACCOUNT_KEY_PATH 變數"
    exit 1
fi

echo "🔨 建置 Docker 映像..."
docker build -t $IMAGE_NAME .

if [ $? -eq 0 ]; then
    echo "✅ Docker 映像建置成功"
    
    # 停止並移除現有容器 (如果存在)
    docker stop $CONTAINER_NAME 2>/dev/null
    docker rm $CONTAINER_NAME 2>/dev/null
    
    echo "🚀 啟動 Docker 容器..."
    docker run -d \
        --name $CONTAINER_NAME \
        -p $PORT:8080 \
        -v "$SERVICE_ACCOUNT_KEY_PATH:/app/gcp/service-account-key.json:ro" \
        -e ASPNETCORE_ENVIRONMENT=Production \
        -e ASPNETCORE_URLS=http://+:8080 \
        $IMAGE_NAME
    
    if [ $? -eq 0 ]; then
        echo "✅ 容器啟動成功!"
        echo "🌐 應用程式運行於: http://localhost:$PORT"
        echo "📋 容器名稱: $CONTAINER_NAME"
        echo ""
        echo "🔧 實用命令:"
        echo "  查看日誌: docker logs -f $CONTAINER_NAME"
        echo "  停止容器: docker stop $CONTAINER_NAME"
        echo "  移除容器: docker rm $CONTAINER_NAME"
    else
        echo "❌ 容器啟動失敗"
        exit 1
    fi
else
    echo "❌ Docker 映像建置失敗"
    exit 1
fi
