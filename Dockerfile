# docker build -t image-generator .
# docker run -p 7120:80 image-generator

# apt-get update && apt-get install -y libfontconfig1

FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 7120

ENV ASPNETCORE_URLS=http://+:7120

# Creates a non-root user with an explicit UID and adds permission to access the /app folder
# For more info, please refer to https://aka.ms/vscode-docker-dotnet-configure-containers
RUN adduser -u 5678 --disabled-password --gecos "" appuser && mkdir /app/App_Data && chown -R appuser /app
USER appuser

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["AppImageGenerator.csproj", "./"]
RUN dotnet restore "AppImageGenerator.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "AppImageGenerator.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "AppImageGenerator.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AppImageGenerator.dll"]

USER root
RUN chown -R appuser /app/App_Data
USER appuser
