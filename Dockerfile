FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["SmartExpenseManager.csproj", "."]
RUN dotnet restore "SmartExpenseManager.csproj"

COPY . .
RUN dotnet publish "SmartExpenseManager.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "SmartExpenseManager.dll"]