# Build ASP.NET Core application
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
ARG BUILD_VERSION=unknown
COPY . /ctf4e
WORKDIR /ctf4e/src/Ctf4e.LabServer
RUN dotnet publish "Ctf4e.LabServer.csproj" -c Release -o /app/publish -p:BuildVersion=$BUILD_VERSION

# Setup static files
FROM node:latest AS build-static
COPY . /ctf4e
WORKDIR /ctf4e/src/Ctf4e.LabServer
RUN npm install
RUN npx grunt

# Produce final image
FROM mcr.microsoft.com/dotnet/aspnet:8.0-bookworm-slim
WORKDIR /app
COPY --from=build /app/publish .
COPY --from=build-static /ctf4e/src/Ctf4e.LabServer/wwwroot/lib ./wwwroot/lib
COPY --from=build-static /ctf4e/src/Ctf4e.LabServer/wwwroot/css ./wwwroot/css
ENTRYPOINT ["dotnet", "Ctf4e.LabServer.dll"]
