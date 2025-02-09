$configurations = @(
    @{ Platform = "x86"; Runtime = "win7-x86" },
    @{ Platform = "x64"; Runtime = "win7-x64" }
)

foreach ($config in $configurations) {
    dotnet publish src/ProcessMonitor/ProcessMonitor.csproj `
        -c Release `
        -r $config.Runtime `
        --self-contained true `
        /p:PublishSingleFile=true `
        /p:IncludeNativeLibrariesForSelfExtract=true `
        /p:Platform=$config.Platform
}

if (Test-Path "artifacts") { Remove-Item -Recurse -Force artifacts }
New-Item -ItemType Directory -Path artifacts | Out-Null

Copy-Item "src/ProcessMonitor/bin/Release/net6.0-windows/win7-x86/publish/*" -Destination artifacts/x86 -Recurse
Copy-Item "src/ProcessMonitor/bin/Release/net6.0-windows/win7-x64/publish/*" -Destination artifacts/x64 -Recurse

Write-Host "Build artifacts are available in: $((Resolve-Path artifacts).Path)"
