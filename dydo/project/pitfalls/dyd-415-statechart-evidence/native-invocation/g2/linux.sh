#!/bin/bash
set -euo pipefail
mkdir -p /work
cp -a /source /work/source
export DOTNET_CLI_HOME=/work/dotnet-home
export NUGET_PACKAGES=/work/nuget
export DOTNET_CLI_USE_MSBUILD_SERVER=0
export MSBUILDDISABLENODEREUSE=1
dotnet --info > /evidence/dotnet-info.stdout 2>&1
uname -a > /evidence/uname.stdout
dotnet publish /work/source/dydo/project/pitfalls/dyd-415-statechart-evidence/native-invocation/g1/fixture/Fixture.csproj \
  -c Release -r linux-x64 --self-contained -p:RepositoryRoot=/work/source \
  -o /work/publish --warnaserror > /evidence/aot-publish.stdout 2>&1
sha256sum /work/publish/Fixture > /evidence/aot-binary.sha256
python3 -B /input/g2_cases.py /work/publish/Fixture /work/cases /evidence/native-outcomes.json \
  > /evidence/native-cases.stdout 2> /evidence/native-cases.stderr
