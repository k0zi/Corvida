FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
WORKDIR /src
COPY Corvida.Core/Corvida.Core.csproj Corvida.Core/
COPY Corvida.ServiceDefaults/Corvida.ServiceDefaults.csproj Corvida.ServiceDefaults/
COPY Corvida.Api/Corvida.Api.csproj Corvida.Api/
RUN dotnet restore Corvida.Api/Corvida.Api.csproj
COPY Corvida.Core/ Corvida.Core/
COPY Corvida.ServiceDefaults/ Corvida.ServiceDefaults/
COPY Corvida.Api/ Corvida.Api/
RUN dotnet publish Corvida.Api/Corvida.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Corvida.Api.dll"]
