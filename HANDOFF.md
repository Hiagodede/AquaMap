# AquaMap — Handoff / Estado Atual do Projeto

Documento escrito na saída do desenvolvedor responsável, para que o time que assume o projeto tenha uma visão honesta do que funciona, o que está quebrado e o que falta. Datado em 2026-07-09. Não é um roadmap de aspirações (isso está em `ROADMAP_PRODUCAO.md`) nem uma explicação de arquitetura (isso está em `ARCHITECTURE.md`) — é um raio-x do estado real do código nesta data.

## 1. O que foi validado hoje (2026-07-09)

Antes da entrega, os três projetos executáveis foram recompilados do zero e testados:

- **`AquaMap` (técnico)**: compila sem erros/avisos. Foi executado como app standalone (Windows) e ficou responsivo, sem crash. Confirmado visualmente que o novo campo "Ferro — Fe (mg/L)" aparece corretamente no formulário de coleta.
- **`AquaMap.Public` (cidadão)**: compila sem erros/avisos. **Não foi possível validar visualmente** — o `.exe` gerado crasha ao abrir direto (`0xc000027b` em `Microsoft.UI.Xaml.dll`, confirmado pelo Log de Eventos do Windows). Esse crash já acontecia em execuções de ontem (08/07), antes de qualquer mudança de hoje, e as configurações de empacotamento Windows dos dois `.csproj` são idênticas — é um problema de deploy/identidade do Windows App SDK ao rodar o `.exe` bruto nesta máquina, não uma regressão de código. **Ao testar, rode via Visual Studio (F5/Deploy) e não pelo `.exe` da pasta `bin/`.**
- **`AquaMap.Api`**: compila sem erros/avisos.

## 2. Mudanças entregues neste commit

- Seletor de localização por toque no mapa (Leaflet/`WebView`) no formulário de coleta e no formulário de reservatório, complementando a captura por GPS já existente, com fallback de digitação manual de lat/lng.
- Exportação de boletim em PDF por reservatório (`PdfExportService`, QuestPDF), acionável na tela de detalhe do técnico.
- Parâmetro "Ferro" (Fe) exibido nas telas de detalhe dos dois apps (o dado já existia no domínio/banco desde a migration `AddIronAndCollectionGps`; só não estava na UI).
- Cards de métricas (total de reservatórios, total de coletas, reservatórios em alerta) na tela inicial do app técnico.
- Legenda no mapa do app cidadão.

## 3. Bugs conhecidos (por prioridade)

### 🔴 Alta — perda de dados na sincronização offline
`SyncService.SyncPendingAnalysisAsync` (`AquaMap/Services/SyncService.cs:73-81`), ao reenviar para a API uma análise que foi capturada offline, monta o objeto `WaterAnalysis` copiando só `ResidualChlorine`, `Ph`, `Turbidity`, `EColiAbsent`, `ReservoirId` e `AnalysisDate`. **`Iron` e as coordenadas GPS da coleta (`CollectionLatitude`/`CollectionLongitude`) ficam de fora** e chegam zeradas/nulas na API, mesmo tendo sido salvas corretamente no SQLite local. Ou seja: qualquer coleta feita sem internet perde o valor de ferro e a localização assim que sincroniza.

### 🔴 Alta — faixa de cloro "ideal" incoerente com a regra de potabilidade
No formulário de coleta, `CollectionFormViewModel.ValidateChlorineAsync` (`AquaMap/ViewModels/CollectionFormViewModel.cs:357-358`) só aceita cloro residual entre **0.2 e 2.0 mg/L** como válido e bloqueia o envio fora dessa faixa. Só que a regra real de potabilidade — usada pelo domínio (`WaterAnalysis.IsChlorineValid`, `AquaMap.Domain/Entities/WaterAnalysis.cs:28`) e pela própria API — aceita até **5.0 mg/L**. Resultado: um técnico com uma leitura de, por exemplo, 3.0 mg/L (potável pela regra oficial) é impedido de submeter a coleta pelo app. O mesmo descompasso existe duplicado em `LocalWaterAnalysis` (`AquaMap/Models/LocalEntities.cs:43` usa 0.2–2.0, linha 56 usa 0.2–5.0 na mesma classe).

### 🔴 Alta (segurança) — sem checagem de papel (role) na API
Todas as rotas protegidas usam só `.RequireAuthorization()` — exigem um JWT válido, mas não verificam `Role`. Não há `[Authorize(Roles=...)]` nem policy em lugar nenhum de `AquaMap.Api/Program.cs`. Na prática, qualquer usuário autenticado — inclusive um com `UserType.Citizen` — pode chamar rotas de criação/edição/exclusão de reservatórios e usuários, que deveriam ser exclusivas de administrador/técnico.

### 🟡 Média — nomenclatura de papel de usuário confusa
O enum de domínio só tem `UserType.Citizen` (0) e `UserType.Administrator` (1) — não existe um valor "Técnico". Só que `UserFormViewModel.SaveAsync` (`AquaMap/ViewModels/UserFormViewModel.cs:401`) grava `Role = 0` para todo usuário não-admin, e a UI (`UserDto.RoleLabel`, `LocalUser.RoleLabel`) rotula esse `0` como **"Técnico"**. Ou seja: todo técnico do SAAE cadastrado pelo app fica salvo no banco como `Citizen`. Funciona porque a API não diferencia papéis (ver item acima), mas é uma armadilha para quem for implementar controle de acesso por papel no futuro.

### 🟡 Média — apps em Debug apontam para a API de produção
`AquaMap/appsettings.Development.json` e `AquaMap.Public/appsettings.Development.json` têm `BaseUrlAndroid`/`BaseUrlWindows` configurados para `https://aquamap-g0at.onrender.com` — a mesma API real usada pelos usuários finais. Não existe um ambiente local/staging configurado nos arquivos versionados. Qualquer teste manual durante o desenvolvimento grava no banco de produção.

### 🟢 Baixa — crash do `AquaMap.Public.exe` ao rodar direto (ambiente local)
Ver item 1. Provavelmente ligado a identidade/empacotamento do Windows App SDK não registrada para deploy "unpackaged" desse projeto especificamente nesta máquina. Não bloqueia build nem funcionamento via Visual Studio.

### 🟢 Baixa — seletor manual de coordenadas não aceita números negativos com facilidade
Os campos de latitude/longitude manuais usam `Keyboard="Numeric"`, que normalmente não expõe uma tecla de "-" — e Alegre/ES tem coordenadas negativas. Caminho secundário (GPS e toque no mapa são os principais), mas vale trocar para um teclado que aceite sinal.

## 4. Dívida técnica / código morto

- **`AquaMap.Application`** é um projeto vazio na solution (só tem um `Class1.cs` de template) — reservado desde o início do projeto para "casos de uso futuros" e nunca usado. Decidir se mantém ou remove.
- **`SeedDataService`/`ISeedDataService`** existem, mas não fazem nada (`InitializeAsync` é um `Task.CompletedTask` vazio) e não estão registrados em nenhum DI nem chamados em lugar algum. O único seed real é o usuário admin padrão, inline em `AquaMap.Api/Program.cs`. O README antigo (já corrigido nesta entrega) afirmava que esse serviço recriava o banco com dados de teste — isso nunca foi verdade no código atual.
- Pacote `Microsoft.EntityFrameworkCore.Sqlite` referenciado em `AquaMap.Infrastructure.csproj` sem uso — o provider ativo é sempre Npgsql/PostgreSQL.
- Versões do pacote `Npgsql.EntityFrameworkCore.PostgreSQL` divergem entre projetos: `9.0.4` em `AquaMap.Infrastructure` (net9.0) vs `10.0.1` em `AquaMap.Api` (net10.0). Funciona hoje, mas vale alinhar numa próxima manutenção.
- Endpoints `GET /metrics` e `GET /water-analysis/collection-points` existem na API mas **nenhum client os consome** — os apps calculam métricas e pontos de coleta client-side a partir dos dados já carregados. Ou é trabalho inacabado, ou dá pra remover os endpoints.
- Marcadores `GAP 1` a `GAP 7` no código (busca por `GAP ` nos ViewModels/Program.cs) parecem ser numeração interna de um backlog anterior. Todos os números encontrados (1, 2, 3, 4, 5, 7) estão implementados; **não existe `GAP 6` em lugar nenhum** — vale perguntar ao time/cliente se foi descartado ou se ficou pra trás.
- Zero projetos de teste automatizado na solution. Todo o processo de validação até hoje foi manual (Thunder Client / execução manual dos apps).

## 5. Segurança e segredos

- Nenhum segredo real está no código versionado: `Jwt:Key` e a connection string de produção estão vazios nos `appsettings.Production.json`/`appsettings.json` da API e são injetados via variáveis de ambiente (Render.com em produção, `.env` + `docker-compose.yml` localmente, `dotnet user-secrets` em desenvolvimento). Isso já foi corrigido em commits anteriores (`fix(docker): remove hardcoded secrets`, `fix(api): remove hardcoded production secrets`).
- O usuário administrador padrão criado automaticamente (`000.000.000-00` / `admin123`) existe para todo banco que inicializa vazio — **confirme que a senha foi trocada no banco de produção real**, já que esse seed roda incondicionalmente sempre que `Users` está vazio.
- O token JWT expira em 8 horas e é armazenado em `SecureStorage` no dispositivo; não há refresh token — o usuário precisa logar de novo após expirar.

## 6. Como isso se encaixa no `ROADMAP_PRODUCAO.md`

- **Fase 1 (Higiene de configuração e segredos)**: concluída — segredos isolados, sem IPs fixos versionados (mas ver item 3 "apps em Debug apontam para produção", que é uma variação desse problema que sobrou).
- **Fase 2 (Login + formulário de coleta no MAUI)**: concluída.
- **Fase 3 (Mapa nativo em vez de WebView)**: parcial — só o Android do app técnico usa `Microsoft.Maui.Controls.Maps` nativo; Windows do app técnico e **o app cidadão inteiro** continuam em `WebView` + Leaflet/OSM.
- **Fase 4 (Deploy em nuvem)**: concluída — API rodando no Render.com com PostgreSQL gerenciado.

## 7. Prioridades sugeridas para o próximo time

1. Corrigir a perda de Ferro/GPS na sincronização offline (§3, item 1) — é perda de dado real em produção.
2. Alinhar a faixa de cloro do formulário com a regra oficial de potabilidade (§3, item 2) — está bloqueando envios válidos.
3. Decidir uma estratégia de autorização por papel na API antes de o app crescer (§3, item 3) — hoje é uma exposição de segurança real, mesmo que baixo risco enquanto o app tiver poucos usuários.
4. Separar um ambiente de desenvolvimento/staging da API para os apps em Debug não escreverem em produção (§3, item 5).
5. Resolver a confusão de nomenclatura de papel de usuário (§3, item 4) antes de implementar o item 3.
6. Avaliar se vale a pena manter `AquaMap.Application` e `SeedDataService` como estão, ou remover o código morto.
