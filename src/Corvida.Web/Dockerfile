# Build the WebAssembly bundle, then serve it as a static site.
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
WORKDIR /src

# The emscripten toolchain that compiles the WebAssembly runtime shells out to python.
RUN apt-get update \
    && apt-get install -y --no-install-recommends python3 \
    && ln -sf /usr/bin/python3 /usr/bin/python \
    && rm -rf /var/lib/apt/lists/*

# Restore first so a source-only change does not re-download the emscripten workload.
COPY Corvida.Core/Corvida.Core.csproj Corvida.Core/
COPY Corvida.Shared/Corvida.Shared.csproj Corvida.Shared/
COPY Corvida.Web/Corvida.Web.csproj Corvida.Web/
RUN dotnet workload restore Corvida.Web/Corvida.Web.csproj || dotnet workload install wasm-tools
RUN dotnet restore Corvida.Web/Corvida.Web.csproj

COPY Corvida.Core/ Corvida.Core/
COPY Corvida.Shared/ Corvida.Shared/
COPY Corvida.Web/ Corvida.Web/
RUN dotnet publish Corvida.Web/Corvida.Web.csproj -c Release -o /app

FROM nginx:1.27-alpine AS final

# openssl for the self-signed certificate generated on first start.
RUN apk add --no-cache openssl

COPY --from=build /app/wwwroot /usr/share/nginx/html

# Empty apiBaseUrl: the browser calls this same origin and nginx proxies through to the API,
# which is what keeps HTTPS free of mixed-content errors and needs no CORS configuration.
RUN printf '{ "apiBaseUrl": "" }\n' > /usr/share/nginx/html/config.json

COPY Corvida.Web/nginx.conf /etc/nginx/conf.d/default.conf
COPY Corvida.Web/docker-entrypoint.d/10-corvida-cert.sh /docker-entrypoint.d/10-corvida-cert.sh
RUN chmod +x /docker-entrypoint.d/10-corvida-cert.sh

EXPOSE 80 443
