FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/CouponManager.Api/CouponManager.Api.csproj -c Release -o /app --no-self-contained

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /app/data/uploads \
    && chown -R app:app /app/data
COPY --from=build /app .
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "CouponManager.Api.dll"]
