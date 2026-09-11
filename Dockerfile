FROM node:22-bookworm-slim AS web
WORKDIR /web
COPY dashboard/src/web/package*.json ./
RUN npm ci
COPY dashboard/src/web ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY dashboard/src/Api/Api.csproj dashboard/src/Api/
RUN dotnet restore dashboard/src/Api/Api.csproj
COPY dashboard/src/Api dashboard/src/Api
RUN dotnet publish dashboard/src/Api/Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app ./
COPY --from=web /web/dist ./wwwroot
EXPOSE 10000
CMD ASPNETCORE_URLS=http://0.0.0.0:$PORT dotnet Api.dll
