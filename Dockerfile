FROM mcr.microsoft.com/dotnet/sdk:9.0 AS base

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY "src/arc-export" .
RUN dotnet restore "./arc-export.fsproj"
RUN dotnet build "./arc-export.fsproj" -c $BUILD_CONFIGURATION -o /build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./arc-export.fsproj" -c $BUILD_CONFIGURATION -o /publish

FROM base AS final

# This container needs git-lfs installed so the cli tool can access it to retrieve lfs tracked files.
RUN apt-get update \
    && apt-get install -y --no-install-recommends git-lfs \
    && rm -rf /var/lib/apt/lists/* \
    && git lfs install --system

COPY --from=publish /publish .

#FROM mcr.microsoft.com/dotnet/sdk:6.0
#
#
#COPY publish/linux-x64/arc-export .
#
##ENTRYPOINT ["/arc-export"]