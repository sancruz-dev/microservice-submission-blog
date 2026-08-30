# Builds and runs only ContentSubmission.Api - test projects aren't part of
# the runtime image, only referenced source needed to restore/build it.
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY src/ContentSubmission.Domain/ContentSubmission.Domain.csproj src/ContentSubmission.Domain/
COPY src/ContentSubmission.Application/ContentSubmission.Application.csproj src/ContentSubmission.Application/
COPY src/ContentSubmission.Infrastructure/ContentSubmission.Infrastructure.csproj src/ContentSubmission.Infrastructure/
COPY src/ContentSubmission.Api/ContentSubmission.Api.csproj src/ContentSubmission.Api/
RUN dotnet restore src/ContentSubmission.Api/ContentSubmission.Api.csproj

COPY src/ src/
RUN dotnet publish src/ContentSubmission.Api/ContentSubmission.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "ContentSubmission.Api.dll"]
