# AquaMap — Arquitetura Técnica

Este documento descreve **como o sistema é construído hoje**, com base em leitura direta do código (não é um plano ou aspiração — para isso veja `ROADMAP_PRODUCAO.md`; para uma avaliação crítica do estado atual, com bugs e dívidas técnicas, veja `HANDOFF.md`).

## 1. Visão Geral dos Componentes

```
┌─────────────────────┐        ┌──────────────────────┐
│   AquaMap (técnico)  │        │ AquaMap.Public (cidadão)│
│  MAUI · offline-first │        │  MAUI · sempre online  │
└──────────┬───────────┘        └───────────┬───────────┘
           │                                 │
           └────────────┬────────────────────┘
                         │  HttpClient
             ┌───────────▼────────────┐
             │  AquaMap.Client.Shared │  ApiService + ApiClientFactory
             └───────────┬────────────┘
                         │ HTTPS (JWT Bearer nas rotas protegidas)
             ┌───────────▼────────────┐
             │      AquaMap.Api       │  Minimal APIs (.NET 10)
             └───────────┬────────────┘
                         │ EF Core (Npgsql)
             ┌───────────▼────────────┐
             │  AquaMap.Infrastructure │  AppDbContext + Migrations
             └───────────┬────────────┘
                         │
             ┌───────────▼────────────┐
             │       PostgreSQL        │  (Render.com em produção; Docker localmente)
             └─────────────────────────┘

  AquaMap.Domain          → entidades e regras de negócio puras, sem dependência de UI/infra
  AquaMap.Application     → camada reservada para casos de uso; hoje é um projeto vazio (stub)
```

Deploy atual: API em Render.com (`https://aquamap-g0at.onrender.com`), banco PostgreSQL gerenciado. Localmente, `docker-compose.yml` sobe Postgres + API a partir de variáveis em `.env` (veja `.env.example`).

## 2. Domínio (`AquaMap.Domain`)

### Entidades

- **`User`** — `Id (Guid)`, `FullName`, `TaxId` (CPF, único), `BirthDate`, `Address`, `PhoneNumber`, `Email`, `PasswordHash` (BCrypt), `Role (UserType)`. Todos os setters são privados; só é possível construir via construtor.
  - `enum UserType { Citizen = 0, Administrator = 1 }` — **só existem esses dois valores** (não há `Technician`; veja a nota em HANDOFF.md sobre a confusão de nomenclatura que isso causa).
- **`Reservoir`** — `Id`, `Name`, `Latitude`, `Longitude`, coleção de `Neighborhood` (1:N) e `WaterAnalysis` (1:N). `StatusColor` é calculado: `"Gray"` sem análises, `"Green"`/`"Red"` conforme `IsPotable` da análise mais recente.
- **`Neighborhood`** — bairro atendido por um reservatório (`ReservoirId` FK). Relação 1:N simples.
- **`WaterAnalysis`** — uma coleta/análise: `AnalysisDate`, `ResidualChlorine`, `Ph`, `Turbidity`, `EColiAbsent`, `Iron`, `CollectionLatitude`/`CollectionLongitude` (nullable), `IsPendingSync`, `ReservoirId` FK.

### Regras de potabilidade (Portaria 888/2021), em `WaterAnalysis`

| Parâmetro | Regra válida |
|---|---|
| Cloro residual | `0.2 ≤ x ≤ 5.0` mg/L |
| pH | `6.0 ≤ x ≤ 9.5` |
| Turbidez | `x ≤ 5.0` NTU |
| Ferro | `x ≤ 0.3` mg/L |
| E. coli | ausente |

`IsPotable` exige todos os parâmetros válidos **e** ausência de E. coli. Essa é a regra canônica — a API e o domínio a seguem; **o formulário de coleta no app técnico usa uma faixa "ideal" de cloro diferente (0.2–2.0) e isso causa um bug real**, detalhado em HANDOFF.md.

## 3. Infraestrutura e Dados (`AquaMap.Infrastructure`)

- `AppDbContext` mapeia as 4 entidades para PostgreSQL via Npgsql. Índice único em `User.TaxId`. FKs com cascade delete entre `Reservoir` → `Neighborhood`/`WaterAnalysis`.
- 3 migrations aplicadas: `InitialCreate`, `AddCoordinates` (lat/lng do reservatório), `AddIronAndCollectionGps` (ferro + GPS da coleta + flag de sync pendente). Migrations rodam automaticamente no startup da API (`db.Database.Migrate()`), não há passo manual.
- `SeedDataService`/`ISeedDataService` existem no código mas **não fazem nada e não são chamados** (stub morto — o único seed real é o usuário admin, feito inline em `AquaMap.Api/Program.cs` no startup).
- O pacote `Microsoft.EntityFrameworkCore.Sqlite` está referenciado no `.csproj` da Infrastructure mas não é usado em lugar nenhum — o provider ativo é sempre Npgsql/PostgreSQL, tanto local (Docker) quanto em produção.

## 4. API (`AquaMap.Api`)

Minimal APIs, tudo definido em `Program.cs` (sem controllers). Autenticação: JWT Bearer, chave/issuer/audience vindos de configuração (nunca hardcoded — `Jwt:Key` vazio no `appsettings.json` versionado, populado via `dotnet user-secrets` em dev ou variáveis de ambiente `Jwt__Key` em produção/Docker).

| Rota | Método | Autenticação | Descrição |
|---|---|---|---|
| `/login` | POST | — | Verifica `TaxId` + senha (BCrypt), retorna JWT |
| `/reservoirs` | GET | — | Lista reservatórios com bairros e análises |
| `/reservoirs/{id}` | GET | — | Detalhe de um reservatório |
| `/reservoirs` | POST | JWT | Cria reservatório |
| `/reservoirs/{id}` | PUT | JWT | Atualiza reservatório e reconcilia bairros |
| `/reservoirs/{id}` | DELETE | JWT | Remove reservatório |
| `/water-analysis` | POST | JWT | Registra uma coleta/análise |
| `/water-analysis/{reservoirId}` | GET | — | Histórico de análises de um reservatório |
| `/water-analysis/collection-points` | GET | — | Pontos georreferenciados com coleta (não consumido por nenhum client hoje) |
| `/users` | POST | JWT | Cria usuário |
| `/users` | GET | JWT | Lista usuários |
| `/users/{id}` | DELETE | JWT | Remove usuário |
| `/metrics` | GET | JWT | Métricas agregadas (não consumido por nenhum client hoje — os apps calculam as métricas localmente) |

**Importante:** `RequireAuthorization()` exige apenas um JWT válido — não há checagem de `Role`/policy em nenhuma rota. Qualquer usuário autenticado (mesmo `Citizen`) pode chamar rotas administrativas. Detalhado como risco de segurança em HANDOFF.md.

Um usuário administrador padrão (`000.000.000-00` / `admin123`) é criado automaticamente se a tabela `Users` estiver vazia no startup.

## 5. Client compartilhado (`AquaMap.Client.Shared`)

- **`ApiService`** — um método por endpoint (exceto `/metrics` e `/water-analysis/collection-points`, que não têm método correspondente ainda). Todas as chamadas fazem `try/catch` internamente e retornam `null`/`false`/lista vazia em erro — nenhuma exceção sobe para quem chama.
- **`ApiClientFactory`** — resolve a `BaseUrl` a partir de `appsettings.json` embutido como recurso no assembly de cada app (`appsettings.Development.json` também, só em builds Debug, com override por plataforma via `BaseUrlAndroid`/`BaseUrlWindows`). **Hoje, tanto em Release quanto em Debug, os dois apps apontam para a API de produção no Render** — não há um ambiente local configurado nos arquivos versionados.
- O token JWT **não é anexado automaticamente**: cada ViewModel lê o token do `SecureStorage` (chave `"jwt_token"`) e passa como parâmetro para o método do `ApiService`, que monta o header `Authorization: Bearer` manualmente a cada chamada. Não existe um `DelegatingHandler` central.

## 6. App Técnico (`AquaMap`) — offline-first

- **Navegação**: `AppShell` com 3 abas (Reservatórios, Mapa, Técnico/Login) + rotas empurradas (`CollectionFormPage`, `ReservoirFormPage`, `ReservoirDetailPage`, `UserListPage`, `UserFormPage`).
- **`LocalDatabaseService`**: wrapper sobre SQLite local (`aquamap.db3`), com tabelas espelho `LocalReservoir`, `LocalWaterAnalysis`, `LocalUser`. Leituras (reservatórios, histórico de análise, usuários) sempre passam pelo banco local primeiro; dados vindos da API são gravados de volta no SQLite (padrão stale-while-revalidate).
- **`SyncService`**: monitora `Connectivity.ConnectivityChanged`; quando a rede volta, varre `LocalWaterAnalysis` com `IsPendingSync = true` e envia uma a uma para `POST /water-analysis`, marcando como sincronizada em caso de sucesso. É *push* unidirecional, sem resolução de conflito (last-write-wins implícito).
- **Escopo do offline-first é limitado a `WaterAnalysis`**: criar/editar/excluir reservatório e criar/excluir usuário chamam a API diretamente e **falham se estiver offline** — não entram na fila de sincronização.
- **Login/token**: `LoginViewModel` valida CPF localmente (checksum) antes de chamar a API; token JWT recebido é salvo em `SecureStorage` (`"jwt_token"`) e cada tela que precisa dele o lê individualmente (ver §5).
- **Seletor de localização (GPS)**: três caminhos — captura por GPS do dispositivo (`Geolocation`, funciona offline), toque num mapa Leaflet embutido em `WebView` (exige internet, carrega tiles/JS de CDN), ou digitação manual de lat/lng como fallback.
- **Mapa da aba "Mapa"**: `WebView` + Leaflet/OpenStreetMap em todas as plataformas, exceto Android, que usa `Microsoft.Maui.Controls.Maps` nativo (`#if ANDROID`).
- **Exportação de PDF**: `PdfExportService` (QuestPDF) gera um boletim por reservatório a partir do histórico de análises e aciona o `Share` nativo do MAUI.

## 7. App Cidadão (`AquaMap.Public`) — somente leitura

- Sem login, sem SQLite, sem `SyncService` — cada tela chama `ApiService` diretamente e mantém os dados só em memória (`ObservableCollection`).
- Mapa da tela principal: inteiramente `WebView` + HTML/Leaflet montado em runtime (marcadores serializados como JSON no HTML), com um esquema de URL customizado (`aquamap://details/{id}`) para navegar ao detalhe do reservatório ao clicar no popup do marcador.
- Cor do marcador/legenda é recalculada no client a partir de `WaterAnalysis.IsPotable` da análise mais recente (a lógica de potabilidade duplicada aqui já foi corrigida para não reimplementar as regras do domínio — ver histórico do git).

## 8. Convenções e padrões

- MVVM nos dois apps; ViewModels não conhecem `Page`, navegação é feita via `Shell.Current.GoToAsync`.
- Domínio (`AquaMap.Domain`) não depende de UI nem de infraestrutura — só é referenciado por elas.
- `AquaMap.Application` existe na solution como camada reservada para casos de uso mais complexos, mas hoje é um projeto vazio (um único arquivo de template) — nenhuma lógica passa por ali ainda.
