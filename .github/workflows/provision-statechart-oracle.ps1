$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

# CI-only compiler: the shipped runtime and the other statechart jobs keep their pinned JRE.
$lock = Get-Content Resources/Statechart/dependency-lock.json -Raw | ConvertFrom-Json
$jdk = $lock.configOracleJdk
$root = New-Item -ItemType Directory -Path (Join-Path $env:RUNNER_TEMP 'dydo-statechart-oracle')
$jar = Join-Path $root 'tla2tools.jar'
Invoke-WebRequest -Uri $lock.tlc.url -OutFile $jar
if ((Get-FileHash $jar -Algorithm SHA256).Hash -ne $lock.tlc.sha256) { throw 'TLC does not match its pinned SHA-256.' }
$archive = Join-Path $root $jdk.name
Invoke-WebRequest -Uri $jdk.url -OutFile $archive
if ((Get-FileHash $archive -Algorithm SHA256).Hash -ne $jdk.sha256) { throw 'The oracle JDK does not match its pinned SHA-256.' }
$javaRoot = New-Item -ItemType Directory -Path (Join-Path $root 'java')
& tar -xzf $archive -C $javaRoot
if ($LASTEXITCODE -ne 0) { throw 'The oracle JDK did not unpack.' }
$java = @(Get-ChildItem $javaRoot -Recurse -Filter java | Where-Object { $_.Directory.Name -eq 'bin' })
if ($java.Count -ne 1) { throw 'The oracle JDK archive must hold exactly one bin/java.' }
"tla2tools=$jar" >> $env:GITHUB_OUTPUT
"java=$($java[0].FullName)" >> $env:GITHUB_OUTPUT
