# Stage 1: Build the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /src

# Install Node.js, NPM and build tools in a single layer then cleanup
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl gnupg build-essential \
    && curl -fsSL https://deb.nodesource.com/setup_18.x | bash - \
    && apt-get install -y nodejs \
    && rm -rf /var/lib/apt/lists/*

# Verify Node.js and NPM installation
RUN node -v && npm -v

# Copy package files and install dependencies
COPY Feedster.Web/package*.json ./Feedster.Web/
WORKDIR /src/Feedster.Web

# Set NODE_ENV to development for npm install
ENV NODE_ENV=development

# Install NPM dependencies including devDependencies
RUN npm ci

# Reset NODE_ENV to production for the build
ENV NODE_ENV=production

# Copy the rest of your source code
COPY . /src

# Restore .NET dependencies
RUN dotnet restore "Feedster.Web.csproj"

# Publish the project (build + publish) without restoring again
RUN dotnet publish "Feedster.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false --no-restore

# Stage 2: Build the runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM base AS final
WORKDIR /app

# Copy the published app from the build stage
COPY --from=build-env /app/publish .

# Set the entry point for the container
ENTRYPOINT ["dotnet", "Feedster.Web.dll"]
