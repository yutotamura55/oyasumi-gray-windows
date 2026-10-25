# Version sets the version of the executable, for example 0.1.0 or 0.1.0-beta.1, without a leading v.
# Without it, the .NET SDK default (1.0.0) is used.
param([string] $Version)

$ErrorActionPreference = 'Stop'

if ($PSBoundParameters.ContainsKey('Version')) {
    $pattern = '^(0|[1-9][0-9]{0,4})\.(0|[1-9][0-9]{0,4})\.(0|[1-9][0-9]{0,4})(-[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*)?$'
    $isValid = $Version -match $pattern
    # An assembly version part must be 65534 or less.
    if ($isValid) { $isValid = -not (@($Matches[1], $Matches[2], $Matches[3]) | Where-Object { [int]$_ -gt 65534 }) }
    if (-not $isValid) { throw "Version '$Version' must look like 1.2.3 or 1.2.3-beta.1, with each number 65534 or less." }
}

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
