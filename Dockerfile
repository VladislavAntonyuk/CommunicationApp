FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /app

COPY . ./

RUN dotnet publish CommunicationApi/CommunicationApi.csproj -c Release

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/CommunicationApi/bin/Release/net10.0/publish/ ./
ENTRYPOINT ["dotnet", "CommunicationApi.dll"]