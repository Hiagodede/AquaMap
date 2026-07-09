# Usa a imagem oficial do .NET SDK 10.0 (ou 9.0 dependendo da versão usada localmente)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copia todos os arquivos de projeto primeiro para otimizar o cache do Docker (restauração de pacotes)
COPY ["AquaMap.Api/AquaMap.Api.csproj", "AquaMap.Api/"]
COPY ["AquaMap.Application/AquaMap.Application.csproj", "AquaMap.Application/"]
COPY ["AquaMap.Domain/AquaMap.Domain.csproj", "AquaMap.Domain/"]
COPY ["AquaMap.Infrastructure/AquaMap.Infrastructure.csproj", "AquaMap.Infrastructure/"]

# Restaura as dependências
RUN dotnet restore "AquaMap.Api/AquaMap.Api.csproj"

# Copia o restante do código-fonte
COPY . .

# Compila o projeto em modo Release
WORKDIR "/src/AquaMap.Api"
RUN dotnet build "AquaMap.Api.csproj" -c Release -o /app/build

# Publica a aplicação
FROM build AS publish
RUN dotnet publish "AquaMap.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Gera a imagem final de runtime (menor e otimizada)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 80
EXPOSE 443

# Copia os arquivos publicados para o container final
COPY --from=publish /app/publish .

# Define o ponto de entrada da API
ENTRYPOINT ["dotnet", "AquaMap.Api.dll"]
