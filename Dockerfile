FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
RUN apt-get update \
    && apt-get install -y python3 python-is-python3 \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
RUN apt-get update \
    && apt-get install -y python3 python-is-python3 \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /src
COPY HelloWorldWeb.csproj ./
RUN dotnet restore "HelloWorldWeb.csproj"
COPY . .
RUN dotnet publish "HelloWorldWeb.csproj" -c Release -o /app/out /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/out ./out
COPY render_monitor.py .
WORKDIR /app/out
ENTRYPOINT ["sh", "-c", "dotnet NoodlesSimulator.dll --urls http://0.0.0.0:${PORT:-8080}"]
