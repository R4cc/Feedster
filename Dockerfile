# See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

# Stage 1: Build the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /src

# Install Node.js and NPM
RUN apt-get update && apt-get install -y curl gnupg
RUN curl -fsSL https://deb.nodesource.com/setup_18.x | bash -
RUN apt-get install -y nodejs

# Verify Node.js and NPM installation
RUN node -v
RUN npm -v

# Copy the entire source code
COPY . .

# Set the working directory to the web project
WORKDIR /src/Feedster.Web

# Restore dependencies
RUN dotnet restore "Feedster.Web.csproj"

# Build the project
RUN dotnet build "Feedster.Web.csproj" -c Release -o /app/build

# Publish the project
RUN dotnet publish "Feedster.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Build the runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM base AS final
WORKDIR /app

# Copy the published app from the build stage
COPY --from=build-env /app/publish .

# Set the entry point for the container
ENTRYPOINT ["dotnet", "Feedster.Web.dll"]
