# AquaMap

Sistema cliente-servidor para monitoramento da qualidade da água do SAAE Alegre, com base na **Portaria GM/MS Nº 888/2021**. Duas aplicações MAUI (técnico e cidadão) consomem uma API .NET compartilhada, com PostgreSQL como banco de dados.

> Documentação completa: este README cobre visão geral e como rodar o projeto.
> Para arquitetura técnica detalhada (fluxo de dados, autenticação, sincronização offline), veja **[ARCHITECTURE.md](ARCHITECTURE.md)**.
> Para o estado atual real do projeto — o que funciona, bugs conhecidos, dívidas técnicas e prioridades para quem assumir — veja **[HANDOFF.md](HANDOFF.md)**. Leia esse documento antes de começar a mexer no código.
> Para testar os apps no Visual Studio (Windows, emulador Android ou celular físico, com geração de .apk) — veja **[MANUAL_TECNICO_TI_CINETICA.md](MANUAL_TECNICO_TI_CINETICA.md)**.

## 1. Visão Geral

O AquaMap é composto por duas aplicações distintas que compartilham a mesma API e as mesmas regras de negócio:

- **AquaMap** (`AquaMap/`) — app do **técnico do SAAE**. Requer login (JWT), permite cadastrar/editar reservatórios, registrar coletas de análise de água (cloro, pH, turbidez, ferro, E. coli), gerenciar usuários e exportar boletins em PDF. Funciona **offline-first**: lê dados do SQLite local e sincroniza análises pendentes quando a conexão volta.
- **AquaMap.Public** (`AquaMap.Public/`) — app do **cidadão**, somente leitura, sem login. Mostra no mapa os reservatórios e o boletim de qualidade da água de cada um. Sempre online, sem cache local.

Ambos consomem a mesma **AquaMap.Api**, hospedada atualmente em produção em `https://aquamap-g0at.onrender.com` (Render.com) com PostgreSQL.

## 2. Estrutura da Solução

```
📂 AquaMap.sln
 ├── 📂 AquaMap                 # App Técnico (MAUI) — login, CRUD, coleta, PDF, sync offline
 ├── 📂 AquaMap.Public          # App Cidadão (MAUI) — leitura pública, sem login, sempre online
 ├── 📂 AquaMap.Client.Shared   # HTTP client (ApiService) compartilhado pelos dois apps acima
 ├── 📂 AquaMap.Domain          # Entidades e regras de negócio puras (User, Reservoir, Neighborhood, WaterAnalysis)
 ├── 📂 AquaMap.Application     # Camada de casos de uso — projeto reservado, ainda vazio (ver HANDOFF.md)
 ├── 📂 AquaMap.Infrastructure  # EF Core + AppDbContext + Migrations (PostgreSQL)
 └── 📂 AquaMap.Api             # Backend .NET (Minimal APIs) — autenticação JWT, endpoints REST
```

Não existe projeto de testes automatizados na solution — veja **HANDOFF.md** para esse e outros gaps.

## 3. Tecnologias Principais

- **.NET 9** (apps MAUI) / **.NET 10** (API)
- **PostgreSQL** + **Entity Framework Core** (Code-First, migrations em `AquaMap.Infrastructure/Migrations`)
- **JWT Bearer** para autenticação do app técnico
- **SQLite** (`sqlite-net-pcl`) local no app técnico, para cache offline-first
- **QuestPDF** para geração de boletins em PDF
- **Leaflet.js + OpenStreetMap** (via `WebView`) para os mapas e o seletor de localização por toque
- **MVVM** como padrão de UI nos dois apps

## 4. Como Rodar o Projeto

### 4.1 Apps MAUI (usando a API de produção)

Mais rápido para testar a UI — os dois apps já vêm configurados (inclusive em Debug) para apontar para a API em produção (`https://aquamap-g0at.onrender.com`).

1. Abra `AquaMap.sln` no Visual Studio 2022 (17.12+), com a workload **".NET Multi-platform App UI development"** instalada.
2. Defina `AquaMap` (técnico) ou `AquaMap.Public` (cidadão) como projeto de inicialização.
3. Escolha o destino **Windows Machine** (mais rápido) ou um emulador Android.
4. F5.

Login de técnico padrão (seed automático, ver §4.2): CPF `000.000.000-00`, senha `admin123`.

> ⚠️ Como o app aponta para produção mesmo em Debug, qualquer cadastro/coleta feito localmente durante o desenvolvimento vai parar no banco real. Veja o alerta correspondente em **HANDOFF.md**.

### 4.2 API + banco local (para desenvolver o backend)

1. Copie `.env.example` para `.env` e ajuste os valores (usuário/senha do Postgres e uma `JWT_KEY` de pelo menos 32 caracteres).
2. Suba o banco (e opcionalmente a API) via Docker: `docker compose up -d`.
3. Para rodar a API localmente pelo Visual Studio/`dotnet run` em vez do container, configure os segredos de desenvolvimento com `dotnet user-secrets` no projeto `AquaMap.Api` (`Jwt:Key`, `ConnectionStrings:DefaultConnection`) — os arquivos `appsettings*.json` versionados não têm segredos reais (são preenchidos por env vars/user-secrets, nunca hardcoded).
4. As migrations do EF Core rodam automaticamente no startup da API (`db.Database.Migrate()`); não é necessário aplicar nada manualmente.
5. No primeiro start, se a tabela `Users` estiver vazia, a API cria um usuário administrador padrão (CPF `000.000.000-00` / senha `admin123`) — troque essa senha em qualquer ambiente que não seja local.

## 5. Convenções de Código

- Idioma do código (classes, variáveis, comentários): **inglês**. Documentação de projeto: português.
- `async`/`await` para toda operação de I/O.
- UI: usar `Border` em vez de `Frame` (padrão .NET 9 MAUI).
- Fluxo de dados no app técnico: SQLite local → `LocalDatabaseService` → ViewModel → View (binding). Escrita nova sempre grava local primeiro e marca `IsPendingSync`; `SyncService` empurra para a API quando há conexão.
