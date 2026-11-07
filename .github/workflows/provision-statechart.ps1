$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

# Shared by ci.yml and release.yml so both test against the same pinned, hash-checked TLC, JRE and
# Pele. The calling step's id receives the tla2tools, java and pele outputs.
$lock = Get-Content Resources/Statechart/dependency-lock.json -Raw | ConvertFrom-Json
$jre = ($lock.platforms | Where-Object rid -eq 'win-x64').java
if (-not $IsWindows) { $jre = ($lock.platforms | Where-Object rid -eq 'linux-x64').java }
$root = New-Item -ItemType Directory -Path (Join-Path $env:RUNNER_TEMP 'dydo-statechart')
function Get-Pinned($url, $name) {
  $file = Join-Path $root $name
  Invoke-WebRequest -Uri $url -OutFile $file
  $file
}
$jar = Get-Pinned $lock.tlc.url 'tla2tools.jar'
if ((Get-FileHash $jar -Algorithm SHA256).Hash -ne $lock.tlc.sha256) { throw 'TLC does not match its pinned SHA-256.' }
$archive = Get-Pinned $jre.url $jre.name
if ((Get-FileHash $archive -Algorithm SHA256).Hash -ne $jre.sha256) { throw 'The JRE does not match its pinned SHA-256.' }
$javaRoot = New-Item -ItemType Directory -Path (Join-Path $root 'java')
if ($IsWindows) {
  Expand-Archive $archive -DestinationPath $javaRoot
  $javaName = 'java.exe'
} else {
  & tar -xzf $archive -C $javaRoot
  if ($LASTEXITCODE -ne 0) { throw 'The JRE did not unpack.' }
  $javaName = 'java'
}
$java = @(Get-ChildItem $javaRoot -Recurse -Filter $javaName | Where-Object { $_.Directory.Name -eq 'bin' })
if ($java.Count -ne 1) { throw 'The JRE archive must hold exactly one bin/java executable.' }
$tarball = Get-Pinned $lock.pele.url 'pele.tgz'
$integrity = 'sha512-' + [Convert]::ToBase64String([Security.Cryptography.SHA512]::HashData([IO.File]::ReadAllBytes($tarball)))
if ($integrity -cne $lock.pele.integrity) { throw 'Pele does not match its pinned integrity.' }
$pele = New-Item -ItemType Directory -Path (Join-Path $root 'pele')
& tar -xzf $tarball -C $pele
if ($LASTEXITCODE -ne 0) { throw 'Pele did not unpack.' }
$entry = [IO.Path]::GetFullPath((Join-Path $pele "package/$($lock.pele.entry)"))
if (-not (Test-Path $entry -PathType Leaf)) { throw 'Pele has no pinned entry module.' }
"tla2tools=$jar" >> $env:GITHUB_OUTPUT
"java=$($java[0].FullName)" >> $env:GITHUB_OUTPUT
"pele=$entry" >> $env:GITHUB_OUTPUT
