FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/Revestik.Api/Revestik.Api.csproj \
    && dotnet publish src/Revestik.Api/Revestik.Api.csproj -c Release --no-restore -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
COPY --from=build /out .
USER $APP_UID
ENTRYPOINT ["dotnet", "Revestik.Api.dll"]
