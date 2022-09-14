#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["Feedster.Web/Feedster.Web.csproj", "Feedster.Web/"]
COPY ["Feedster.DAL/Feedster.DAL.csproj", "Feedster.DAL/"]
RUN dotnet restore "Feedster.Web/Feedster.Web.csproj"
COPY . .
WORKDIR "/src/Feedster.Web"

# Install NPM
RUN apt-get install -y curl \
 && curl -sL https://deb.nodesource.com/setup_9.x | bash - \
 && apt-get install -y nodejs \
 && curl -L https://www.npmjs.com/install.sh | sh

RUN dotnet build "Feedster.Web.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Feedster.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Feedster.Web.dll"]