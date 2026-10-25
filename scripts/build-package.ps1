# Version sets the file and product version of the executable, for example 0.1.0.
# Without it, the .NET SDK default (1.0.0) is used.
param([string] $Version)

$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$project = Join-Path $repositoryRoot 'src/OyasumiGray.Windows/OyasumiGray.Windows.csproj'
$artifacts = Join-Path $repositoryRoot 'artifacts'
$archive = Join-Path $artifacts 'OyasumiGray.Windows-win-x64.zip'
$temporaryArchive = Join-Path $artifacts ("package-" + [guid]::NewGuid().ToString('N') + '.zip')
$publishDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ("oyasumi-gray-" + [guid]::NewGuid().ToString('N'))

try {
    New-Item -ItemType Directory -Path $artifacts -Force | Out-Null
    $publishArguments = @($project, '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true', '-p:PublishSingleFile=true', '-o', $publishDirectory)
    if ($Version) { $publishArguments += "-p:Version=$Version" }
    dotnet publish @publishArguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }

    Copy-Item (Join-Path $repositoryRoot 'LICENSE') $publishDirectory
    Compress-Archive -Path (Join-Path $publishDirectory '*') -DestinationPath $temporaryArchive
    Move-Item -Path $temporaryArchive -Destination $archive -Force
    Write-Host "Created $archive"
}
finally {
    if (Test-Path $temporaryArchive) { Remove-Item $temporaryArchive -Force }
    if (Test-Path $publishDirectory) { Remove-Item $publishDirectory -Recurse -Force }
}
