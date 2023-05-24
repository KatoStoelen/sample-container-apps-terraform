FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS runtime
WORKDIR /app

COPY . ./

ENV DOTNET_EnableDiagnostics=0

ENTRYPOINT ["dotnet", "Api.dll"]