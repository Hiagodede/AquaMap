# Relatório da força-tarefa — 01/10/2026

Branch `fix/entrega-16h`, criado a partir do `hiago` (`a90b8b5`). São 19 commits de correção, um por achado, e nada foi mesclado no `hiago` nem no `master`.

## 1. Status final

| Pergunta | Resposta |
|---|---|
| O app Cidadão abre? | **Provavelmente sim, mas ainda não foi confirmado num celular.** A causa do crash foi achada e corrigida (M-01): a tela inicial usava duas cores que só existiam no app Técnico. Não havia emulador aqui para abrir o app. Isso é o teste nº 1 da seção 4. |
| O Técnico continua funcionando? | Compila sem erros e sem avisos novos. As mudanças nele são só tratamento de erro e mensagens. Não testei no aparelho. |
| APKs gerados? | Sim, pelo build Release no Codespace: `out/apk/AquaMap-Tecnico-8be471a.apk` (38,4 MB) e `out/apk/AquaMap-Cidadao-8be471a.apk` (26,8 MB). Os dois foram assinados com a chave de debug, então servem para teste e não para a loja. |
| GitHub Actions | Não rodou: o Codespace não tem permissão para disparar workflow (erro 403). Para rodar, vá em **Actions → Build APK → Run workflow → branch `fix/entrega-16h`**. |
| Revisões | Cada lote foi revisado por um agente que não fez as correções, e o diff completo passou por uma revisão final. Nenhum item bloqueante ficou aberto. |

## 2. O que foi corrigido

**Mais urgente**
| ID | Problema | Arquivo |
|---|---|---|
| M-01 | O Cidadão fechava ao abrir porque a tela inicial usava as cores `TextMuted`/`TextSecondary`, que ele não tinha | `AquaMap.Public/Resources/Styles/Colors.xaml` |
| B-01 | **Nenhuma coleta chegava ao servidor:** a data ia sem fuso e o Postgres respondia erro 500. Agora a API converte para UTC | `AquaMap.Api/Program.cs` |

**Segurança e dados (API)**
| ID | Problema | Arquivo |
|---|---|---|
| B-03 | Qualquer usuário logado podia criar um Administrador | `Program.cs` |
| B-03b | Era possível apagar a si mesmo ou o último Administrador | `Program.cs` |
| B-04 | Apagar um reservatório levava todo o histórico de análises junto. Agora a exclusão é recusada (409) se houver análises | `Program.cs` |
| B-05 | A API aceitava campos internos vindos do app e GPS fora da faixa | `Program.cs` |
| B-06 | Cadastro de usuário sem validação, e campos faltando davam erro 500 | `Program.cs` |
| B-08 | A regra de potabilidade estava copiada em `/metrics`. O resultado continua igual | `Program.cs` |
| B-10 | Novo `GET /health` para acordar a API no Render | `Program.cs` |
| B-11 | Erros inesperados agora devolvem 500 em JSON, sem detalhes internos | `Program.cs` |

**Uso do app (UX)**
| ID | Problema | Arquivo |
|---|---|---|
| M-07, M-07b, R-04 | Erros ao abrir telas ou tocar no mapa derrubavam o app. Agora aparece um aviso | `MainPage.xaml.cs` (Cidadão), `MapPage.xaml.cs` e `LoginPage.xaml.cs` (Técnico) |
| M-08, R-03 | O Cidadão ficava com a tela vazia enquanto a API acordava. Agora mostra "Conectando… (pode levar até 1 minuto)" e o botão **Tentar novamente** | `AquaMap.Public/ViewModels/MainViewModel.cs`, `Views/MainPage.xaml(.cs)` |
| M-09 | O mapa do Técnico esperava 1 segundo fixo pelos dados. Agora espera o carregamento real | `MapViewModel.cs`, `MapPage.xaml(.cs)` |
| B-03c, R-01 | Mensagens erradas no cadastro e na exclusão de usuário ("CPF em uso" quando o problema era outro). Agora há mensagens para 400/401/403/409 e para falta de rede | `UserFormViewModel.cs`, `UserListViewModel.cs`, `ApiService.cs` |
| R-02 | Um erro de navegação no login apagava o token salvo | `LoginPage.xaml.cs` |

## 3. O que ficou pendente

**Precisa de você**
1. **Redeploy da API no Render (B-02).** A produção roda uma versão antiga, que descarta Ferro e GPS, e **nenhuma correção da API vale lá antes do redeploy**. A migration `AddIronAndCollectionGps` só adiciona colunas e roda sozinha na inicialização.
2. **Siga esta ordem para não perder dados:**
   1. Merge do seu `80bb98e` (`fix/coleta-ferro-gps-cloro`) no `hiago`.
   2. Merge deste branch.
   3. Gere e instale o APK novo do Técnico.
   4. Só então faça o redeploy.

   Motivo: quando a API voltar a aceitar coletas, as coletas presas nos celulares vão subir todas de uma vez. Se o APK antigo ainda estiver instalado, elas sobem **sem Ferro e sem GPS**.

   O revisor simulou o merge do `80bb98e` com este branch e **não dá conflito**.
3. **Papéis de usuário.** Qualquer usuário logado ainda pode apagar reservatórios e usuários, e o `GET /users` mostra CPF, e-mail e telefone para todos. Só foi aplicada a proteção mínima, porque os técnicos estão gravados com papel 0 (Cidadão) e restringir mais bloquearia o trabalho deles. Falta definir quem pode o quê.
4. **Admin criado automaticamente (B-07).** O admin com CPF 000.000.000-00 não consegue entrar pelo app. Falta definir as credenciais.
5. **Leituras fora do padrão (M-06).** O formulário de coleta recusa pH e turbidez fora da faixa, então alertas reais nunca são registrados. Mudar isso é decisão do SAAE, e mexe no mesmo arquivo do `80bb98e`.
6. **Tela de template no Cidadão (M-13).** O `MauiProgram.cs` ainda registra a `MainPage` de template da raiz do projeto. Apagá-la muda qual página é registrada, então precisa de teste no aparelho.
7. **Exclusão em cascata no banco.** A proteção do B-04 está no código. A definitiva é `OnDelete(Restrict)`, que exige uma migration.

**Não foi feito, por decisão**
- **M-04 e M-05 (Ferro/GPS no sync e faixa de cloro):** já estão no seu `80bb98e`.
- **Parte mobile do B-01 (data no `SyncService`):** não precisa, porque o servidor já corrige a data. Fazer isso só criaria conflito com o `80bb98e`.
- **Erro na exportação de PDF** (`ReservoirDetailViewModel.cs:114-117`): mostra a mensagem técnica do erro ao usuário, e o aviso não está protegido. Ficou de fora para não invalidar o build final.

## 4. Como testar no celular em 5 passos

1. **Baixe os APKs.** No VS Code do Codespace, abra `out/apk/`, clique com o botão direito em cada APK e escolha **Download**.
2. **Instale.** Passe os arquivos para o celular e instale. Se o Android disser que o app já existe com outra assinatura, desinstale a versão antiga antes. Atenção: **desinstalar o Técnico apaga as coletas pendentes no aparelho**. Nesse caso, sincronize antes ou use outro celular.
3. **Teste o Cidadão.** Abra o app: deve aparecer "Conectando ao servidor…" e depois o mapa com os reservatórios. Teste também sem internet: deve aparecer o erro com o botão **Tentar novamente**.
4. **Teste o Técnico.** Faça login, abra o mapa e um reservatório, e cadastre um usuário de teste com "Administrador" ligado, usando uma conta que não é admin. Deve aparecer "Somente administradores podem criar outro administrador".
5. **Teste a sincronização** (só depois do redeploy da seção 3). Registre uma coleta e confira se ela aparece na API (`GET /reservoirs`).

## 5. Riscos conhecidos

- **PDF do Técnico.** O QuestPDF 2024.3.4 coloca no APK uma biblioteca nativa de Linux desktop, não de Android. A exportação de PDF provavelmente falha no celular. Já era assim antes destas correções.
- **Páginas de 16 KB (aviso XA0141).** O Android 16 vai exigir páginas de 16 KB, e o QuestPDF e o SQLite ainda não atendem. Não impede o uso hoje.
- **Lista vazia no Cidadão.** Se a base não tiver nenhum reservatório, o Cidadão mostra "erro de conexão", porque o `ApiService` devolve lista vazia quando dá erro.
- **Coleta recusada fica pendente.** Uma coleta recusada pela nova validação de GPS (400) fica pendente no aparelho para sempre. É improvável, porque o GPS vem do próprio celular.
- **Exclusões simultâneas.** Duas exclusões ao mesmo tempo podem passar pelas proteções do último admin e do reservatório com análises. A correção definitiva exige migration.
- **Testes.** Nenhuma correção foi testada em aparelho. Só a falha do B-01 foi reproduzida, numa API local com banco descartável; a correção dela não foi rodada.

Registro completo do que foi decidido: `.claude/work/` (log, plano, achados, revisões e builds). Essa pasta fica só no Codespace, porque está no `.gitignore`.
