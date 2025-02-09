# Windows Process Monitor

实时监控Windows进程变化的监控程序，支持Windows 7及以上系统

## 功能特性
- 每秒记录进程状态
- 生成CSV日志文件
- 支持32位/64位系统
- 单文件独立部署

## 构建要求
- .NET 6 SDK
- Windows 10/11 开发环境

## 构建方法
```bash
# 通过PowerShell
./build.ps1

# 或手动构建
dotnet publish -c Release -r win7-x86 --self-contained true
dotnet publish -c Release -r win7-x64 --self-contained true
```

## 兼容性要求
- Windows 7 SP1 或更高版本
- 需要安装 [.NET 6 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/6.0) （如果未使用自包含构建）

## 输出文件
构建完成后在 artifacts 目录下：
- x86/ProcessMonitor.exe （32位版本）
- x64/ProcessMonitor.exe （64位版本）
