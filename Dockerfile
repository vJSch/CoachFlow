FROM node:22-alpine AS web
WORKDIR /repo/src/coachflow-web
COPY src/coachflow-web/package.json src/coachflow-web/package-lock.json src/coachflow-web/.npmrc ./
RUN npm ci
COPY src/coachflow-web/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /repo
COPY global.json ./
COPY src/CoachFlow.Api/ src/CoachFlow.Api/
COPY --from=web /repo/src/CoachFlow.Api/wwwroot/ src/CoachFlow.Api/wwwroot/
RUN dotnet publish src/CoachFlow.Api/CoachFlow.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=api /app ./
# Die SQLite-Datei muss auf einem dauerhaften Volume liegen, sonst gehen die Daten beim Neustart verloren.
RUN mkdir /data && chown app /data
VOLUME /data
ENV ConnectionStrings__Default="Data Source=/data/coachflow.db"
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "CoachFlow.Api.dll"]
