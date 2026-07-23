FROM mcr.microsoft.com/dotnet/sdk:10.0 AS base

# git-lfs is needed both at test time (LFS-related fixtures shell out to git) and
# at runtime (the cli tool retrieves LFS-tracked files). Install it once here so
# both the build/test stage and the final image inherit it.
ARG GIT_LFS_VERSION=3.7.1
ARG GIT_LFS_SHA256=1c0b6ee5200ca708c5cebebb18fdeb0e1c98f1af5c1a9cba205a4c0ab5a5ec08
RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates curl \
    && rm -rf /var/lib/apt/lists/* \
    && curl -fsSL "https://github.com/git-lfs/git-lfs/releases/download/v${GIT_LFS_VERSION}/git-lfs-linux-amd64-v${GIT_LFS_VERSION}.tar.gz" -o /tmp/git-lfs.tar.gz \
    && echo "${GIT_LFS_SHA256}  /tmp/git-lfs.tar.gz" | sha256sum -c - \
    && tar -xzf /tmp/git-lfs.tar.gz -C /tmp \
    && /tmp/git-lfs-${GIT_LFS_VERSION}/install.sh \
    && rm -rf /tmp/git-lfs.tar.gz /tmp/git-lfs-${GIT_LFS_VERSION} \
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
