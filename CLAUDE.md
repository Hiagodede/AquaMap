# AquaMap — instruções para agentes

Sistema de monitoramento da qualidade da água do **SAAE Alegre/ES**, baseado na **Portaria GM/MS nº 888/2021**. O objetivo da branch `v2` é levar o produto a um nível **lançável de verdade** (loja de apps, usuários reais), sem quebrar a versão em uso.

Leia antes de qualquer tarefa:
- `docs/PLANO_V2.md`: fases, checklist e o que está em andamento. **É a fonte de verdade do trabalho.**
- `HANDOFF.md` e `RELATORIO_CORRECOES.md`: estado real e bugs conhecidos (alguns já resolvidos na `v2`; confira no código).
- `ARCHITECTURE.md`: fluxo de dados, autenticação e sincronização offline.

## Estrutura

| Projeto | O que é | Framework |
|---|---|---|
| `AquaMap/` | App do **técnico** (MAUI): login JWT, CRUD de reservatórios, coleta, PDF, offline-first com SQLite | net9.0-android/ios/maccatalyst/windows |
| `AquaMap.Public/` | App do **cidadão** (MAUI): só leitura, sem login, sempre online | net9.0-* |
| `AquaMap.Client.Shared/` | `ApiService` HTTP compartilhado pelos dois apps | net9.0 |
| `AquaMap.Api/` | Minimal APIs, tudo em `Program.cs`, JWT, EF Core + Npgsql | net10.0 |
| `AquaMap.Domain/` | Entidades e regras puras (`WaterAnalysis.IsPotable` etc.) | net9.0 |
| `AquaMap.Infrastructure/` | `AppDbContext` e migrations (PostgreSQL) | net9.0 |
| `AquaMap.Application/` | Vazio (só `Class1.cs`). Decisão pendente no plano | net9.0 |

Produção: API em `https://aquamap-g0at.onrender.com` (Render.com, PostgreSQL gerenciado). Cold start de até ~1 min; existe `GET /health`.

## Regras inegociáveis

1. **Nunca quebre a produção.** A tag `v1.0-estavel` e a branch `master` representam a versão em uso. Todo trabalho vai para branches a partir de `v2` e entra por PR. Não faça push em `master` e não force-push em nada.
2. **Nunca grave no banco de produção durante o desenvolvimento.** Enquanto o ambiente de staging (Fase 1) não existir, teste a API localmente (`docker compose up -d db` + `dotnet run`), nunca contra o Render.
3. **Regras de negócio da Portaria 888 vivem em um único lugar: o domínio** (`AquaMap.Domain/Entities/WaterAnalysis.cs`). Apps, API e PDF consomem essas propriedades e **nunca reimplementam faixas**. Faixas atuais de potabilidade:
   - Cloro residual livre: 0,2 a 5,0 mg/L
   - pH: 6,0 a 9,5
   - Turbidez: até 5,0 NTU
   - Ferro: até 0,3 mg/L
   - E. coli: ausente

   Alterar uma faixa exige citar a Portaria e ter um teste.
4. **Leitura fora do padrão é um dado válido, não um erro.** O registro de alertas é o propósito do sistema. A validação só rejeita valores fisicamente impossíveis (ex.: pH > 14).
5. **Sem segredos ou URLs fixas no código.** Use configuração por ambiente (`appsettings.{Env}.json`, env vars, `dotnet user-secrets`). Nada de `10.0.2.2`, `localhost` ou chaves em código.
6. **LGPD:** CPF, e-mail, telefone e endereço são dados pessoais. Nunca exponha em endpoints públicos, logs, mensagens de erro ou URLs. Retorne só o necessário (DTOs).
7. **Sem feature creep.** Só implemente o que está no plano. Ideias novas vão para a seção "Backlog de ideias" do `docs/PLANO_V2.md`.

## Convenções de código

- Código (classes, variáveis, comentários) em **inglês**. Documentação, commits e mensagens ao usuário final em **português**.
- `async`/`await` em toda I/O. Nunca `.Result` ou `.Wait()`.
- MAUI: MVVM, `Border` em vez de `Frame`, atualização de UI em `MainThread`. Toda exceção em handler de UI deve virar mensagem amigável, nunca crash.
- API: retorne **DTOs**, não entidades do EF. Erros no formato `ProblemDetails`. Valide entrada na borda.
- Commits no padrão Conventional Commits, em português: `fix(api): ...`, `feat(tecnico): ...`, `test(domain): ...`.
- Um PR = um assunto. PR pequeno e revisável vence PR grande.

## Definição de pronto (DoD)

Uma tarefa só está pronta quando:
1. `dotnet build AquaMap.sln` passa sem erros e **sem novos avisos**.
2. Os testes passam (`dotnet test`), e **todo bug corrigido tem um teste que falhava antes da correção**.
3. A mudança foi revisada por um agente que não a escreveu (`code-reviewer`; para auth, dados pessoais ou entrada de usuário, também `security-reviewer`).
4. O comportamento foi verificado de verdade: API via teste de integração ou chamada HTTP local, app executado no Windows/emulador quando a mudança é visual.
5. O item no `docs/PLANO_V2.md` foi marcado e o que ficou de fora foi anotado.
6. Nunca declare "pronto" sem ter rodado os passos acima. Se algo não pôde ser verificado (ex.: sem emulador), **diga isso explicitamente**.

## Comandos

```bash
dotnet build AquaMap.Api/AquaMap.Api.csproj
dotnet build AquaMap/AquaMap.csproj -f net9.0-windows10.0.19041.0
dotnet build AquaMap.Public/AquaMap.Public.csproj -f net9.0-android
dotnet test                                   # após a Fase 1 criar os projetos de teste
docker compose up -d db                       # Postgres local (copie .env.example para .env)
dotnet ef migrations add <Nome> -p AquaMap.Infrastructure -s AquaMap.Api
```

## Time de agentes (`.claude/agents/`)

| Agente | Quando usar |
|---|---|
| `architect` | Antes de qualquer tarefa média ou grande: quebra o trabalho, define contratos e escreve o plano no PR |
| `backend-dev` | API, EF Core, migrations, autenticação e autorização |
| `mobile-dev` | Apps MAUI: telas, MVVM, sincronização offline, mapa, PDF |
| `qa-engineer` | Projetos de teste, testes que reproduzem bugs, cobertura das regras da Portaria |
| `code-reviewer` | Revisa todo diff antes do PR (correção, simplicidade, aderência a este arquivo) |
| `security-reviewer` | Revisa mudanças em auth, dados pessoais (LGPD), entrada de usuário e segredos |
| `ux-designer` | Design system, acessibilidade, estados de carregamento/erro/vazio, textos |

Fluxo padrão: `architect` planeja → `qa-engineer` escreve o teste que falha → `backend-dev`/`mobile-dev` implementam → `code-reviewer` (+ `security-reviewer`) revisam → PR para `v2` → o dono do projeto valida e faz o merge.
