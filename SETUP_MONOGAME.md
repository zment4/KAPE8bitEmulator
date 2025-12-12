# MonoGame Setup Instructions

To build the KAPE8bitEmulator project, you need to install MonoGame SDK:

## Installation Steps

1. Download MonoGame SDK 3.8.1 from:
   https://github.com/MonoGame/MonoGame/releases/tag/v3.8.1
   
   Download: `MonoGame.framework.3.8.1.303.exe` (for Windows)

2. Run the installer which will install:
   - MonoGame project templates
   - MonoGame Content Builder
   - Required MSBuild targets to `$(MSBuildExtensionsPath)\MonoGame\v3.0\`

3. After installation, you should be able to build with:
   ```bash
   dotnet build KAPE8bitEmulator.csproj
   ```

## Alternative: Use Visual Studio

If you have Visual Studio 2022, you can:
1. Open `KAPE8bitEmulator.sln` in Visual Studio
2. Visual Studio should prompt you to install MonoGame extension
3. Build from Visual Studio IDE

## Verify Installation

After installation, verify the MonoGame targets exist:
```
ls "C:/Program Files/dotnet/sdk/10.0.101/MonoGame/v3.0/"
```

Or on older installations:
```
ls "C:/Program Files (x86)/MSBuild/MonoGame/v3.0/"
```
