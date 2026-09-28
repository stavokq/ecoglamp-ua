FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["src/WebLab/WebLab.csproj", "src/WebLab/"]
RUN dotnet restore "src/WebLab/WebLab.csproj"

COPY . .
WORKDIR "/src/src/WebLab"
RUN dotnet build "WebLab.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "WebLab.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "WebLab.dll"]
