FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://0.0.0.0:8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["GuideMarket.Api/GuideMarket.Api.csproj", "GuideMarket.Api/"]
RUN dotnet restore "GuideMarket.Api/GuideMarket.Api.csproj"
COPY . .
WORKDIR "/src/GuideMarket.Api"
RUN dotnet build "GuideMarket.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "GuideMarket.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "GuideMarket.Api.dll"]