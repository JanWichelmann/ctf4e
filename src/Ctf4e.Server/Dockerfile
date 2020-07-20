ARG BUILD_VERSION=unknown

FROM mcr.microsoft.com/dotnet/core/aspnet:3.1-buster-slim AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/core/sdk:3.1-buster AS build
WORKDIR /src
COPY ["src/Ctf4e.Server/Ctf4e.Server.csproj", "Ctf4e.Server/"]
RUN dotnet restore "Ctf4e.Server/Ctf4e.Server.csproj"
COPY . .
WORKDIR "/src/Ctf4e.Server"
RUN dotnet build "Ctf4e.Server.csproj" -c Release -o /app/build -p:BuildVersion=$BUILD_VERSION

FROM build AS publish
RUN dotnet publish "Ctf4e.Server.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Ctf4e.Server.dll"]