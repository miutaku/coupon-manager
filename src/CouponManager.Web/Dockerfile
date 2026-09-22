FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/CouponManager.Web/CouponManager.Web.csproj -c Release -o /app --no-self-contained

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
RUN mkdir -p /app/data-protection-keys && chown -R app:app /app/data-protection-keys
COPY --from=build /app .
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "CouponManager.Web.dll"]
