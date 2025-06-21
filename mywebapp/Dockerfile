# Development image using SDK (no multi-stage build for live reload)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS dev

# Install .NET 7.0 SDK
RUN dotnet --list-sdks && \
    wget https://packages.microsoft.com/config/debian/11/packages-microsoft-prod.deb -O packages-microsoft-prod.deb && \
    dpkg -i packages-microsoft-prod.deb && \
    apt-get update && \
    apt-get install -y dotnet-sdk-7.0

WORKDIR /app

# Copy everything
COPY . ./

# Restore and build
RUN dotnet restore
RUN dotnet build

# Expose port
EXPOSE 5001

# Set environment URL binding
ENV ASPNETCORE_URLS=http://+:5001

# Use dotnet watch for live reload
ENTRYPOINT ["dotnet", "watch", "--no-hot-reload", "run"]
