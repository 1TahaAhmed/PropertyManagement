FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["src/PropertyManagement.Api/PropertyManagement.Api.csproj", "src/PropertyManagement.Api/"]
COPY ["src/PropertyManagement.Application/PropertyManagement.Application.csproj", "src/PropertyManagement.Application/"]
COPY ["src/PropertyManagement.Domain/PropertyManagement.Domain.csproj", "src/PropertyManagement.Domain/"]
COPY ["src/PropertyManagement.Infrastructure/PropertyManagement.Infrastructure.csproj", "src/PropertyManagement.Infrastructure/"]

RUN dotnet restore "src/PropertyManagement.Api/PropertyManagement.Api.csproj"

COPY src/ src/
WORKDIR "/src/src/PropertyManagement.Api"
RUN dotnet build "PropertyManagement.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build --no-restore

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "PropertyManagement.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false --no-restore

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "PropertyManagement.Api.dll"]
