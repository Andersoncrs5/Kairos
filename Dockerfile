FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore separado para aproveitar cache de camadas
COPY src/Kairos.Api/Kairos.Api.csproj src/Kairos.Api/
RUN dotnet restore src/Kairos.Api/Kairos.Api.csproj

COPY src/Kairos.Api/ src/Kairos.Api/
RUN dotnet publish src/Kairos.Api/Kairos.Api.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# A imagem aspnet já escuta na 8080 e tem o usuário não-root "app"
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Kairos.Api.dll"]