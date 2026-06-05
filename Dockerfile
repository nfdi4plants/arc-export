FROM mcr.microsoft.com/dotnet/sdk:9.0 AS base

# git-lfs is needed both at test time (LFS-related fixtures shell out to git) and
# at runtime (the cli tool retrieves LFS-tracked files). Install it once here so
# both the build/test stage and the final image inherit it.
RUN apt-get update \
    && apt-get install -y --no-install-recommends git-lfs \
    && rm -rf /var/lib/apt/lists/* \
    && git lfs install --system

FROM base AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY . .
RUN dotnet restore "./arc-export.sln"
RUN dotnet test "./arc-export.sln" -c $BUILD_CONFIGURATION --no-restore

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./src/arc-export/arc-export.fsproj" -c $BUILD_CONFIGURATION -o /publish --no-restore

FROM base AS final
COPY --from=publish /publish .

#FROM mcr.microsoft.com/dotnet/sdk:6.0
#
#
#COPY publish/linux-x64/arc-export .
#
##ENTRYPOINT ["/arc-export"]