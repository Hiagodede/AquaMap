# AquaMap v2 — Plano até o lançamento

> Fonte de verdade do trabalho na branch `v2`. Agentes: marquem `[x]` só depois de cumprir a Definição de Pronto do `CLAUDE.md` e anotem o PR ao lado do item.

## Estratégia de versões

- **`v1.0-estavel` (tag) / `master`**: a versão em uso. Não recebe trabalho da v2, só correções urgentes, se forem inevitáveis.
- **`v2`**: integração do desenvolvimento novo. Criada a partir de `origin/hiago` (força-tarefa de 01/10, PR #3) + merge de `fix/coleta-ferro-gps-cloro` (Ferro/GPS na sincronização e faixa de cloro).
- **Branches de trabalho**: `feat/...`, `fix/...`, `chore/...` a partir de `v2`, com PR de volta para `v2`. Agentes em paralelo usam **git worktrees** separados.
- **Promoção**: quando a v2 for validada em staging e com técnicos reais, `v2` entra na `master` e é criada a tag `v2.0.0`.

## Diagnóstico (2026-10-04)

O app funciona, mas não tem as bases de um produto:
- Nenhum teste automatizado. O único CI é o `build-apk.yml`, que só gera APK e não valida nada.
- Não existe staging: os apps em Debug apontam para a API de produção.
- Autorização por papel incompleta: `UserType` só tem `Citizen`/`Administrator`, e os técnicos são gravados como `Citizen`.
- `/login` sem rate limit (força bruta por CPF). O seed `admin123` roda sempre que `Users` está vazio.
- A API devolve entidades do EF (precisa de `IgnoreCycles`). `GET /reservoirs` traz todas as análises, sem paginação.
- `GET /users` expõe CPF, e-mail e telefone a qualquer usuário logado (LGPD).
- Apps MAUI em **.NET 9 (STS, suporte termina em novembro de 2026)**. A API já está em .NET 10.
- `ApplicationId` ainda é `com.companyname.*`, a identidade visual ainda é de template (`dotnet_bot.png`) e os APKs são assinados com chave de debug.
- Sem observabilidade: crashes e erros em produção são invisíveis.

---

## Fase 0 — Integração e deploy das correções pendentes

Ordem obrigatória (ver `RELATORIO_CORRECOES.md` §3) para não perder Ferro/GPS das coletas presas nos celulares:
- [x] Integrar `origin/hiago` + `fix/coleta-ferro-gps-cloro` na `v2`
- [x] Compilar a solution inteira na `v2` (PR `chore/build-android`: apps passam a ser só Android)
- [ ] Rodar os dois apps no emulador Android ou no celular (sem emulador instalado ainda; cuidado: Debug aponta para produção)
- [ ] Testar no celular: o app Cidadão abre (M-01) e uma coleta offline sincroniza com Ferro e GPS
- [ ] Decidir se essas correções também vão para a produção atual antes da v2 (hotfix). Se sim: APK novo do Técnico instalado **antes** do redeploy da API
- [ ] **Assinatura dos APKs (bloqueia instalar qualquer APK novo nos celulares):** hoje todo APK sai com chave de debug efêmera, e o Android recusa atualizar um app assinado com outra chave. Desinstalar apaga o `aquamap.db3` com as coletas pendentes. Antes de trocar o app de um técnico: (a) sincronizar todas as pendências com o APK antigo, ou (b) recuperar a keystore que assinou o APK instalado e passar a usá-la

## Fase 1 — Base de qualidade (antes de qualquer feature)

**Ambientes**
- [ ] Criar a API de **staging** no Render (serviço + Postgres separados, segredos próprios)
- [ ] Apps em Debug/Beta apontam para staging. `ApplicationId` beta (`...aquamap.beta`, `...aquamap.cidadao.beta`) com outro nome e ícone, para conviver com a versão de produção no mesmo celular. Remover junto o `BaseUrlWindows` (código morto em `ApiClientFactory.cs` e nos `appsettings.Development.json`)
- [ ] Seed de dados de teste **apenas** em Development/Staging (remover ou corrigir o `SeedDataService` morto)

**Testes**
- [ ] `tests/AquaMap.Domain.Tests` (xUnit): cobertura completa das faixas da Portaria 888, incluindo os limites (0,2 / 5,0 / 6,0 / 9,5 / 5,0 / 0,3)
- [ ] `tests/AquaMap.Api.Tests`: `WebApplicationFactory` + **Testcontainers (PostgreSQL real)**, cobrindo login, CRUD e as regras de autorização
- [ ] Testes do `SyncService` / `LocalWaterAnalysis` (mapeamento de todos os campos na sincronização)

**CI e regras do repositório**
- [ ] `ci.yml` no GitHub Actions: build da API + Domain + Infra, testes e build Android dos dois apps em todo PR para `v2`/`master`. O `build-apk.yml` atual só dispara em `master`/`hiago` e usa SDK 9
- [ ] Proteger as branches `master` e `v2`: exigir CI verde + 1 aprovação, sem force-push
- [ ] `.editorconfig`, `Nullable` habilitado, `TreatWarningsAsErrors` nos projetos novos/limpos, analisadores do .NET
- [ ] Dependabot (NuGet + GitHub Actions)
- [ ] Hooks do Claude Code (`.claude/settings.json`): build/teste automático ao final das tarefas

## Fase 2 — Segurança, dados e LGPD

- [ ] Modelo de papéis definido com o SAAE (proposta: `Citizen`, `Technician`, `Administrator`) + migration que converte os técnicos gravados como `Citizen`
- [ ] Policies de autorização por endpoint (quem cria, edita e exclui reservatórios, usuários e análises)
- [ ] Rate limiting no `/login` + bloqueio progressivo
- [ ] Remover o seed `admin123` de produção. Primeiro admin criado por comando/variável de ambiente única
- [ ] `RequireHttpsMetadata = true` fora de Development. Chave JWT em UTF-8 com tamanho mínimo validado
- [ ] Chave do Google Maps fixa no `AndroidManifest.xml` do Técnico (viola a regra 5): restringir por pacote/SHA-1 no Google Cloud e injetar por propriedade de build. Rever `usesCleartextTraffic="true"`
- [ ] Refresh token (hoje o login expira em 8h sem renovação)
- [ ] `GET /users`: só para admin e sem CPF completo (mascarar)
- [ ] Política de privacidade + respostas do formulário de Data Safety da Google Play

## Fase 3 — Arquitetura

- [ ] DTOs de resposta em todos os endpoints. Remover o `IgnoreCycles`
- [ ] Paginação/limite nas análises (`/reservoirs` não pode carregar o histórico inteiro)
- [ ] Separar o `Program.cs` em grupos de endpoints (`MapGroup`) por recurso. Decidir: usar o `AquaMap.Application` para casos de uso ou removê-lo
- [ ] Validação com FluentValidation + `ProblemDetails` padronizado
- [ ] Usar no cliente `GET /metrics` e `/water-analysis/collection-points`, ou remover esses endpoints
- [ ] Migrar os apps MAUI e as libs para **.NET 10 LTS**. Alinhar as versões de pacotes (Npgsql, EF). Remover `EntityFrameworkCore.Sqlite` da Infrastructure
  - [x] Backend: Infrastructure/Application em net10.0, EF/Npgsql 10, sem EF Sqlite/Tools, OpenApi sem vulnerabilidade (PR `chore/dotnet10-backend`)
  - [ ] Domain, Client.Shared e apps em net10.0-android (PR `chore/dotnet10-apps`: compila sem avisos novos; falta testar em emulador/celular, inclusive atualizar por cima do APK .NET 9 sem perder coletas pendentes)
- [ ] PDF do Técnico: o QuestPDF não suporta mais Android desde a 2024.3 (aviso XA0141 de 16 KB e provável falha ao exportar no celular). Decidir: `Android.Graphics.Pdf` nativo, PDF gerado na API ou outra biblioteca. Conferir a licença do QuestPDF para órgão público
- [ ] Decidir o motor de mapa único para os dois apps (hoje: nativo no Técnico, WebView+Leaflet no Cidadão). No Técnico, o caminho WebView de `MapPage.xaml.cs` ainda carrega o OSM a cada `OnAppearing` antes de ser escondido

## Fase 4 — Experiência e design

- [ ] Identidade visual: nome, ícone, splash e paleta próprios. Remover os assets de template
- [ ] Design system em `Resources/Styles` compartilhado pelos dois apps (cores, tipografia, espaçamentos, componentes)
- [ ] Estados padronizados em todas as telas: carregando, vazio, erro com "tentar novamente", offline
- [ ] Acessibilidade: `SemanticProperties`, contraste AA, fonte dinâmica, alvos de toque ≥ 48dp, status da água não depender só de cor
- [ ] App Cidadão: linguagem simples ("Água própria para consumo"), explicação de cada parâmetro, data da última coleta em destaque
- [ ] App Técnico: fluxo de coleta em poucos toques, indicador claro de pendências de sincronização
- [ ] Decisão do SAAE sobre M-06: aceitar pH/turbidez fora da faixa como alerta, não como erro

## Fase 5 — Observabilidade e lançamento

- [ ] Sentry (MAUI + ASP.NET) com dados pessoais filtrados. Logs estruturados (Serilog). Health check com banco
- [ ] Avaliar o plano pago do Render ou outro host (cold start de ~1 min é inaceitável para o cidadão)
- [ ] Backup automático do Postgres de produção + teste de restauração
- [ ] `ApplicationId` definitivo (ex.: `br.gov.es.saaealegre.aquamap`), keystore de release guardada com segurança, versionamento (`ApplicationDisplayVersion`)
- [ ] Pipeline de release: AAB assinado → Google Play (teste interno → fechado → produção)
- [ ] Teste de campo com técnicos reais do SAAE por pelo menos 2 semanas em staging
- [ ] Checklist de lançamento: textos da loja, screenshots, política de privacidade publicada, canal de suporte

---

## Backlog de ideias (fora do escopo até o lançamento)

_Agentes: anotem aqui ideias de features novas em vez de implementá-las._

## Decisões registradas

| Data | Decisão |
|---|---|
| 2026-10-04 | `v2` criada a partir de `origin/hiago` + `fix/coleta-ferro-gps-cloro`. `master` congelada na tag `v1.0-estavel` |
| 2026-10-04 | Foco exclusivo em **Android**: alvos iOS, Mac Catalyst e Windows removidos dos apps. Migração para .NET 10 será antecipada (PR próprio) |
