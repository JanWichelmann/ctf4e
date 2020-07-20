FROM mcr.microsoft.com/dotnet/core/sdk:3.1-buster AS build
ARG BUILD_VERSION=unknown
WORKDIR /src
COPY . .
WORKDIR "/src/src/Ctf4e.Server"
RUN dotnet publish "Ctf4e.Server.csproj" -c Release -o /app/publish -p:BuildVersion=$BUILD_VERSION

FROM mcr.microsoft.com/dotnet/core/aspnet:3.1-buster-slim
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Ctf4e.Server.dll"]