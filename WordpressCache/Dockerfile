FROM mcr.microsoft.com/dotnet/aspnet:latest AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:latest AS build
WORKDIR /src
COPY ["WordpressCache/WordpressCache.csproj", "WordpressCache/"]
RUN dotnet restore "WordpressCache/WordpressCache.csproj"
COPY . .
WORKDIR "/src/WordpressCache"
RUN dotnet build "WordpressCache.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "WordpressCache.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "WordpressCache.dll"]
