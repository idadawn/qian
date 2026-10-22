FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:747cbade5bfd918a175ac32f8f1d931edec2c7d0d5397a3de72eaef1ac1f66d2
RUN apt-get update && apt-get install --no-install-recommends -y clang zlib1g-dev && rm -rf /var/lib/apt/lists/*
WORKDIR /src
COPY . .
RUN dotnet publish tools/Qian.Gateway.AotProbe -c Release -r linux-arm64 -p:IlcTreatWarningsAsErrors=true -p:TrimmerSingleWarn=false -o /probe
