# Use the official image as a parent image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base

# Set the working directory
WORKDIR /app

# Expose ports for the application
EXPOSE 8080
EXPOSE 8081

# Use the SDK image to build the project
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

# Set the build configuration
ARG BUILD_CONFIGURATION=Release

# Copy csproj and restore dependencies
COPY ["FileRenamer.Api.csproj", "./"]
RUN dotnet restore "FileRenamer.Api.csproj"

# Copy the project files
COPY . .

# Build the application
RUN dotnet build "FileRenamer.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish the application
FROM build AS publish
RUN dotnet publish "FileRenamer.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Generate the runtime image
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Set the entry point for the container
ENTRYPOINT ["dotnet", "FileRenamer.Api.dll"]
