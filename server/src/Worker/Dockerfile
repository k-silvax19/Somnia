FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/Dominio/Somnia.Dominio.csproj src/Dominio/
COPY src/Aplicacao/Somnia.Aplicacao.csproj src/Aplicacao/
COPY src/Infraestrutura/Somnia.Infraestrutura.csproj src/Infraestrutura/
COPY src/Worker/Somnia.Worker.csproj src/Worker/
RUN dotnet restore src/Worker/Somnia.Worker.csproj
COPY src/ src/
RUN dotnet publish src/Worker/Somnia.Worker.csproj -c Release --no-restore -o /out /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /out .
USER app
ENTRYPOINT ["dotnet", "Somnia.Worker.dll"]
