FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER app
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["SRN.API/SRN.API.csproj", "SRN.API/"]
COPY ["SRN.Application/SRN.Application.csproj", "SRN.Application/"]
COPY ["SRN.Domain/SRN.Domain.csproj", "SRN.Domain/"]
COPY ["SRN.Infrastructure/SRN.Infrastructure.csproj", "SRN.Infrastructure/"]

RUN dotnet restore "./SRN.API/SRN.API.csproj"
COPY . .
WORKDIR "/src/SRN.API"
RUN dotnet build "./SRN.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./SRN.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SRN.API.dll"]