FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

COPY ["eSale.Api/eSale.Api.csproj", "eSale.Api/"]
COPY ["eSale.Application/eSale.Application.csproj", "eSale.Application/"]
COPY ["eSale.Domain/eSale.Domain.csproj", "eSale.Domain/"]
COPY ["eSale.Infrastructure/eSale.Infrastructure.csproj", "eSale.Infrastructure/"]
RUN dotnet restore "eSale.Api/eSale.Api.csproj"

COPY . .
WORKDIR /src/eSale.Api
RUN dotnet publish "eSale.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app

RUN addgroup -S esale && adduser -S esale -G esale

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .

RUN chown -R esale:esale /app
USER esale

EXPOSE 8080

ENTRYPOINT ["dotnet", "eSale.Api.dll"]
