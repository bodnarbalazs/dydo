FROM mcr.microsoft.com/dotnet/sdk:10.0.300
RUN apt-get update && apt-get install -y --no-install-recommends clang zlib1g-dev python3 procps && rm -rf /var/lib/apt/lists/*
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
