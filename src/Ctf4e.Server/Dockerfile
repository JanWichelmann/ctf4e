FROM mcr.microsoft.com/dotnet/core/aspnet:3.1-buster-slim AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/core/sdk:3.1-buster AS build
WORKDIR /src
COPY ["ctf4e/ctf4e.csproj", "ctf4e/"]
RUN dotnet restore "ctf4e/ctf4e.csproj"
COPY . .
WORKDIR "/src/ctf4e"
RUN dotnet build "ctf4e.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ctf4e.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ctf4e.dll"]