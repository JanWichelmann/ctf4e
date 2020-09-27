# Build ASP.NET Core application
FROM mcr.microsoft.com/dotnet/core/sdk:3.1-buster AS build
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
FROM mcr.microsoft.com/dotnet/core/aspnet:3.1-buster-slim
EXPOSE 5001
WORKDIR /app
COPY --from=build /app/publish .
COPY --from=build-static /src/src/Ctf4e.LabServer/wwwroot/lib ./wwwroot/lib
ENTRYPOINT ["dotnet", "Ctf4e.LabServer.dll"]
