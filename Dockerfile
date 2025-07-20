FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore "./HelloWorldWeb.csproj"
RUN dotnet publish "./HelloWorldWeb.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
# Install pip and requests
RUN apt-get update && apt-get install -y python3-pip
COPY render_monitor.py .
RUN pip3 install requests
# Run both the .NET app and the Python monitor script in parallel
ENTRYPOINT ["bash", "-c", "dotnet NoodlesSimulator.dll & python3 render_monitor.py"]
