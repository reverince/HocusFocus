# Shipping 빌드 스크립트
# 프로덕션 배포용 최적화 빌드

Write-Host "Building Shipping Release..." -ForegroundColor Green

# 기존 빌드 출력물 정리
if (Test-Path "./publish") {
    Remove-Item -Recurse -Force "./publish"
}

# Shipping 빌드 실행
dotnet publish `
    -c Release `
    -r win-x64 `
    --self-contained false `
    -p:PublishSingleFile=false `
    -p:DebugType=none `
    -p:DebugSymbols=false `
    -p:Optimize=true `
    -p:IncludeNativeLibrariesForSelfExtract=false `
    -o ./publish

if ($LASTEXITCODE -eq 0) {
    Write-Host "`nShipping build completed successfully!" -ForegroundColor Green
    Write-Host "Output directory: ./publish" -ForegroundColor Cyan
    
    # 파일 크기 확인
    $exePath = "./publish/HocusFocus.exe"
    if (Test-Path $exePath) {
        $fileSize = (Get-Item $exePath).Length / 1MB
        Write-Host "Executable size: $([math]::Round($fileSize, 2)) MB" -ForegroundColor Cyan
    }
} else {
    Write-Host "`nBuild failed!" -ForegroundColor Red
    exit 1
}

