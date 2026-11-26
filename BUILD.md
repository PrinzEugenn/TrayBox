# 构建 · Build

Windows 11 x64 · [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)

```powershell
dotnet publish .\src\TrayBox\TrayBoxFluent.csproj -c Release -r win-x64 --self-contained true -p:Platform=x64 -p:WindowsAppSDKSelfContained=true -o .\artifacts\portable
```

将 `--self-contained true` 改为 `--self-contained false` 可构建使用共享运行库的精简版。

Use `--self-contained false` to build the compact edition with the shared desktop runtime.
