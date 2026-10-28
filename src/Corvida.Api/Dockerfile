FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
WORKDIR /src
COPY src/Corvida.Core/Corvida.Core.csproj Corvida.Core/
COPY src/Corvida.ServiceDefaults/Corvida.ServiceDefaults.csproj Corvida.ServiceDefaults/
COPY src/Corvida.Api/Corvida.Api.csproj Corvida.Api/
RUN dotnet restore Corvida.Api/Corvida.Api.csproj
COPY src/Corvida.Core/ Corvida.Core/
COPY src/Corvida.ServiceDefaults/ Corvida.ServiceDefaults/
COPY src/Corvida.Api/ Corvida.Api/
RUN dotnet publish Corvida.Api/Corvida.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS runtime
WORKDIR /app
COPY --from=build /app/publish .
# Own license plus the notices and license texts of the redistributed components.
COPY LICENSE THIRD_PARTY_NOTICES.md /usr/share/doc/corvida/
COPY licenses/ /usr/share/doc/corvida/licenses/
ENTRYPOINT ["dotnet", "Corvida.Api.dll"]
