# Cấu hình gửi email qua Gmail cho môi trường LOCAL (User Secrets, không ghi vào git).
# Cách dùng (PowerShell, tại thư mục gốc dự án):
#   powershell -ExecutionPolicy Bypass -File .\scripts\set-gmail-user-secrets.ps1
#
# Yêu cầu: tài khoản Gmail đã bật xác minh 2 bước và đã tạo App Password tại
#   https://myaccount.google.com/apppasswords  (16 ký tự, có thể dán cả dấu cách)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot '..\DemoMVC.csproj'

$gmail = Read-Host 'Địa chỉ Gmail dùng để gửi (vd: ten.ban@gmail.com)'
if ($gmail -notmatch '^[^@\s]+@[^@\s]+\.[^@\s]+$') {
    throw 'Địa chỉ Gmail không hợp lệ.'
}

$securePassword = Read-Host 'App Password 16 ký tự (không phải mật khẩu Gmail)' -AsSecureString
$bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePassword)
try {
    $appPassword = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr) -replace '\s', ''
}
finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
}
if ($appPassword.Length -ne 16) {
    throw 'App Password của Google phải gồm 16 ký tự.'
}

$publicBaseUrl = Read-Host 'URL công khai cho liên kết trong email (Enter để tự lấy theo trình duyệt, vd: https://localhost:7044)'

dotnet user-secrets set 'Email:Host' 'smtp.gmail.com' --project $project | Out-Null
dotnet user-secrets set 'Email:Port' '587' --project $project | Out-Null
dotnet user-secrets set 'Email:UseStartTls' 'true' --project $project | Out-Null
dotnet user-secrets set 'Email:UserName' $gmail --project $project | Out-Null
dotnet user-secrets set 'Email:Password' $appPassword --project $project | Out-Null
dotnet user-secrets set 'Email:SenderEmail' $gmail --project $project | Out-Null
if (-not [string]::IsNullOrWhiteSpace($publicBaseUrl)) {
    dotnet user-secrets set 'Email:PublicBaseUrl' $publicBaseUrl.Trim() --project $project | Out-Null
}

$appPassword = $null
Write-Host 'Đã lưu cấu hình Gmail vào User Secrets. Chạy lại ứng dụng (dotnet run) để áp dụng.' -ForegroundColor Green
