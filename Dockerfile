# The contract gives the client, the client goes into the frontend build.
FROM node:26-slim AS frontend
WORKDIR /src
COPY contract/package.json contract/package-lock.json contract/
COPY frontend/package.json frontend/package-lock.json frontend/
RUN npm ci --prefix contract && npm ci --prefix frontend
COPY contract contract
COPY frontend frontend
RUN npm run --prefix contract build \
    && npm run --prefix frontend openapi-ts \
    && npm exec --prefix frontend -- vite build frontend

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend
WORKDIR /src
COPY backend/Directory.Build.props backend/
COPY backend/CalendarApi/CalendarApi.csproj backend/CalendarApi/
RUN dotnet restore backend/CalendarApi
COPY backend/CalendarApi backend/CalendarApi
RUN dotnet publish backend/CalendarApi --no-restore -c Release -o /publish -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=backend /publish .
COPY --from=frontend /src/frontend/dist wwwroot
# The SQLite file lives in the container, so it is gone with it: see Деплой in README.md.
RUN mkdir /data && chown "$APP_UID" /data
ENV ConnectionStrings__Calendar="Data Source=/data/calendar.db"
USER $APP_UID
EXPOSE 8080
# exec makes dotnet PID 1, so it receives the stop signal of the platform.
CMD ["sh", "-c", "ASPNETCORE_HTTP_PORTS=\"${PORT:-8080}\" exec dotnet CalendarApi.dll"]
