# Stage 1: Build .NET app
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /source

# Copy solution and project files
COPY Balenthiran.Snipit.slnx .
COPY Balenthiran.Snipit.WebApi/*.csproj ./Balenthiran.Snipit.WebApi/
COPY Balenthiran.Snipit.Abstractions/*.csproj ./Balenthiran.Snipit.Abstractions/
COPY Balenthiran.Snipit.DataModels/*.csproj ./Balenthiran.Snipit.DataModels/
COPY Balenthiran.Snipit.DomainModels/*.csproj ./Balenthiran.Snipit.DomainModels/
COPY Balenthiran.Snipit.Services/*.csproj ./Balenthiran.Snipit.Services/
COPY Balenthiran.Snipit.EntityModels/*.csproj ./Balenthiran.Snipit.EntityModels/ 
COPY Balenthiran.Snipit.Database/*.csproj ./Balenthiran.Snipit.Database/
# Restore dependencies
RUN dotnet restore

# Copy all source files
COPY . .

# Build and publish
RUN dotnet publish Balenthiran.Snipit.WebApi/Balenthiran.Snipit.WebApi.csproj -c Release -o /app/publish

# Stage 2: Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine
WORKDIR /app
COPY --from=build /app/publish .

# Expose port (default ASP.NET Core port is 8080 in .NET 8+)
EXPOSE 8080

ENTRYPOINT ["dotnet", "Balenthiran.Snipit.WebApi.dll"]
