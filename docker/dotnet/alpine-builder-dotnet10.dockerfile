FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine

ENV DOTNET_NOLOGO=1
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1

# Native AOT compilation requires these packages
RUN apk add --no-cache \
    clang \
    build-base \
    zlib-dev \
    libstdc++-dev
