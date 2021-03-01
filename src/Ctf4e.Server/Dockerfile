# Build ASP.NET Core application
FROM mcr.microsoft.com/dotnet/sdk:5.0-alpine AS build
ARG BUILD_VERSION=unknown
WORKDIR /src
COPY . .
WORKDIR "/src/src/Ctf4e.Server"
RUN dotnet publish "Ctf4e.Server.csproj" -c Release -o /app/publish -p:BuildVersion=$BUILD_VERSION

# Setup static files
FROM node:latest AS build-static
COPY . /src
WORKDIR "/src/src/Ctf4e.Server"
RUN npm install
RUN npx grunt

# Produce final image
FROM mcr.microsoft.com/dotnet/aspnet:5.0-buster-slim
WORKDIR /app
COPY --from=build /app/publish .
COPY --from=build-static /src/src/Ctf4e.Server/wwwroot/lib ./wwwroot/lib
COPY --from=build-static /src/src/Ctf4e.Server/wwwroot/css ./wwwroot/css
ENTRYPOINT ["dotnet", "Ctf4e.Server.dll"]
