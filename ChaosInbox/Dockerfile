FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["ChaosInbox.csproj", "./"]
RUN dotnet restore "ChaosInbox.csproj"
COPY . .
RUN dotnet publish "ChaosInbox.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_RUNNING_IN_CONTAINER=true
EXPOSE 8080
COPY --from=build /app/publish .
RUN mkdir -p /app/data
ENTRYPOINT ["dotnet", "ChaosInbox.dll"]
