FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY BunnyChat/BunnyChat.csproj BunnyChat/
RUN dotnet restore BunnyChat/BunnyChat.csproj

COPY BunnyChat/ BunnyChat/
RUN dotnet publish BunnyChat/BunnyChat.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "BunnyChat.dll"]
