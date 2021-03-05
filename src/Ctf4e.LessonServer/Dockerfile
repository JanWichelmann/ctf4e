# Build ASP.NET Core application
FROM mcr.microsoft.com/dotnet/sdk:5.0-alpine AS build
WORKDIR /src
COPY . .
WORKDIR "/src/src/Ctf4e.LabServer"
RUN dotnet publish "Ctf4e.LabServer.csproj" -c Release -o /app/publish

# Setup static files
FROM node:latest AS build-static
COPY . /src
WORKDIR "/src/src/Ctf4e.LabServer"
RUN npm install
RUN npx grunt

# Produce final image
FROM mcr.microsoft.com/dotnet/aspnet:5.0-buster-slim
WORKDIR /app
COPY --from=build /app/publish .
COPY --from=build-static /src/src/Ctf4e.LabServer/wwwroot/lib ./wwwroot/lib
COPY --from=build-static /src/src/Ctf4e.LabServer/wwwroot/css ./wwwroot/css
ENTRYPOINT ["dotnet", "Ctf4e.LabServer.dll"]
