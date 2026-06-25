$ErrorActionPreference = "Stop"
$Version = "0.1.0-dev"
Write-Host "[ForgeStudio Circuit] Build $Version" -ForegroundColor Green

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "dotnet SDK was not found. Install .NET 8 SDK first."
}

dotnet restore .\ForgeStudioCircuit.sln
dotnet build .\ForgeStudioCircuit.sln -c Release --no-restore

dotnet publish .\src\ForgeStudio.Circuit.App\ForgeStudio.Circuit.App.csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:PublishTrimmed=false `
    -p:AssemblyVersion=0.1.0.0 `
    -p:FileVersion=0.1.0.0 `
    -p:InformationalVersion=$Version `
    -o .rtifacts\publish\win-x64

Write-Host "[ForgeStudio Circuit] Published to artifacts/publish/win-x64" -ForegroundColor Green
