# syntax=docker/dockerfile:1
# Build browser assets without installing Node or compilers into the .NET stage.
FROM --platform=$BUILDPLATFORM node:26-bookworm-slim AS assets
WORKDIR /src/Feedster.Web
COPY Feedster.Web/package*.json ./
RUN --mount=type=cache,target=/root/.npm npm ci --ignore-scripts
COPY Feedster.Web/Styles ./Styles
COPY Feedster.Web/Pages ./Pages
COPY Feedster.Web/Shared ./Shared
COPY Feedster.Web/scripts ./scripts
RUN npm run buildcss:release

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0.401-noble AS build
ARG TARGETARCH
WORKDIR /src
COPY global.json ./
COPY Feedster.DAL/Feedster.DAL.csproj Feedster.DAL/
COPY Feedster.Web/Feedster.Web.csproj Feedster.Web/
RUN --mount=type=cache,target=/root/.nuget/packages \
    case "$TARGETARCH" in amd64) rid=linux-x64 ;; arm64) rid=linux-arm64 ;; *) exit 1 ;; esac \
    && dotnet restore Feedster.Web/Feedster.Web.csproj --runtime "$rid"
COPY Feedster.DAL/ Feedster.DAL/
COPY Feedster.Web/ Feedster.Web/
COPY --from=assets /src/Feedster.Web/wwwroot/css/app.css Feedster.Web/wwwroot/css/app.css
COPY --from=assets /src/Feedster.Web/wwwroot/js/alpine.min.js Feedster.Web/wwwroot/js/alpine.min.js
RUN --mount=type=cache,target=/root/.nuget/packages \
    case "$TARGETARCH" in amd64) rid=linux-x64 ;; arm64) rid=linux-arm64 ;; *) exit 1 ;; esac \
    && dotnet publish Feedster.Web/Feedster.Web.csproj --configuration Release \
       --runtime "$rid" --self-contained false --no-restore --output /app/publish \
       -p:SkipCssBuild=true -p:UseAppHost=false -p:DebugType=None -p:DebugSymbols=false \
    && mkdir -p /app/publish/data /app/publish/images

# Retain ICU, certificates, and time zones for feeds, SQLite, and image processing.
FROM mcr.microsoft.com/dotnet/aspnet:10.0.12-noble-chiseled-extra AS final
WORKDIR /app
# Preserve compatibility with existing root-owned data/image bind mounts.
USER 0
EXPOSE 8080
COPY --from=build /app/publish ./
ENTRYPOINT ["dotnet", "Feedster.Web.dll"]