FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine
RUN apk add --no-cache tzdata
WORKDIR /app
COPY publish/ .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "YemekhaneApi.dll"]
