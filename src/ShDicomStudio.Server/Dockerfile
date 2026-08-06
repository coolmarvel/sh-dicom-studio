# sh DICOM Studio 서버 — 멀티스테이지 빌드 (SDK 로 빌드 → 런타임만 담아 슬림하게)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/ShDicomStudio.Server/ShDicomStudio.Server.csproj ShDicomStudio.Server/
RUN dotnet restore ShDicomStudio.Server/ShDicomStudio.Server.csproj
COPY src/ShDicomStudio.Server/ ShDicomStudio.Server/
RUN dotnet publish ShDicomStudio.Server/ShDicomStudio.Server.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "ShDicomStudio.Server.dll"]
