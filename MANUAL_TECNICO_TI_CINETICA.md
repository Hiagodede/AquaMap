# Manual técnico para o time TI Cinética

Este manual explica, passo a passo, como pegar o código do AquaMap, abrir no Visual Studio, rodar os dois apps (Técnico e Cidadão) num computador Windows ou num emulador Android, e como gerar um instalável (.apk) para testar num celular Android real.

Não é preciso instalar nem configurar nenhum servidor/banco de dados para esses testes — os dois apps já vêm configurados para conversar com a API que está rodando online (gratuita, no Render.com), do mesmo jeito que vai funcionar quando o app estiver em uso real. Detalhes de arquitetura estão em `ARCHITECTURE.md`; o estado atual do projeto, incluindo bugs conhecidos, está em `HANDOFF.md` — vale ler antes de testar, para saber o que já é esperado dar errado.

---

## 1. O que instalar antes de começar

1. **Visual Studio 2022** (versão Community, que é gratuita, serve): [visualstudio.microsoft.com](https://visualstudio.microsoft.com/pt-br/downloads/)
   - No instalador, marque a carga de trabalho **".NET Multi-platform App UI development"** (aparece na lista de "Cargas de trabalho"/"Workloads"). Essa opção já instala junto o SDK do Android e o emulador — não precisa instalar Android Studio separado.
2. **Git for Windows**: [git-scm.com/download/win](https://git-scm.com/download/win) — para baixar o código. Se preferir uma interface gráfica em vez de linha de comando, pode instalar o **GitHub Desktop** ([desktop.github.com](https://desktop.github.com/)) no lugar.
3. (Opcional, só se for testar num celular físico) Um **cabo USB** e um celular Android.

## 2. Baixar o código

### Opção A — GitHub Desktop (mais simples, sem linha de comando)
1. Abra o GitHub Desktop, `File > Clone Repository`.
2. Cole a URL: `https://github.com/Hiagodede/AquaMap.git`.
3. Escolha uma pasta local e clique em `Clone`.
4. No topo, no seletor de branch, confirme com quem passou o projeto **qual branch usar** (o desenvolvimento mais recente está no branch `hiago`; depois que o Pull Request for aceito, vai estar em `master`).

### Opção B — Git por linha de comando
```powershell
git clone https://github.com/Hiagodede/AquaMap.git
cd AquaMap
git checkout hiago   # ou "master", conforme orientado
```

### Abrir no Visual Studio
Na pasta baixada, dê duplo clique em **`AquaMap.sln`**. O Visual Studio abre e começa a restaurar os pacotes automaticamente (pode levar alguns minutos na primeira vez).

## 3. Conexão com os servidores online — já está pronta

Não precisa configurar IP, banco de dados nem variável de ambiente nenhuma para testar os apps. Os dois projetos (`AquaMap` e `AquaMap.Public`) já apontam, mesmo em modo de desenvolvimento (Debug), para a API que está publicada em produção:

```
https://aquamap-g0at.onrender.com
```

Ou seja: assim que o app abrir (no Windows, no emulador ou no celular), ele já vai carregar reservatórios e dados reais dessa API online, sem passo extra de configuração.

**Login do app Técnico** (usuário administrador criado automaticamente):
- CPF: `000.000.000-00`
- Senha: `admin123`

> ⚠️ **Atenção**: como o app aponta direto para a API de produção, qualquer coleta ou reservatório que vocês cadastrarem durante os testes vai gravar no banco de dados real. Evitem cadastrar dados de teste com nomes aleatórios sem necessidade, e combinem com o responsável do projeto antes de fazer uma limpeza no banco, se precisar.

## 4. Rodar o app no Windows (mais rápido para testar telas e fluxos)

1. No **Gerenciador de Soluções** (painel à direita do Visual Studio), clique com o botão direito no projeto que quer testar — **`AquaMap`** (Técnico) ou **`AquaMap.Public`** (Cidadão) — e escolha **"Definir como Projeto de Inicialização"** ("Set as Startup Project").
2. Na barra de ferramentas superior, ao lado do botão verde de "play", tem um menu suspenso com o alvo de execução. Selecione **"Windows Machine"** (framework `net9.0-windows...`).
3. Clique no botão verde ▶ (ou aperte **F5**).
4. O app abre como uma janela normal do Windows, já conectado à API online.

Repita o processo trocando o projeto de inicialização para testar o outro app.

## 5. Rodar no emulador Android

1. No Visual Studio, abra o **Gerenciador de Dispositivos Android**: menu `Ferramentas > Android > Android Device Manager` (ou procure o ícone de celular na barra de ferramentas).
2. Se não houver nenhum dispositivo virtual criado, clique em **"New"** (Novo) e crie um — recomendo um perfil "Pixel" com Android 13 (API 33) ou superior. Clique em **Create**, depois **Start** para ligar o emulador.
3. Volte para o menu suspenso de alvo de execução (o mesmo do passo 4.2) e selecione o emulador Android que acabou de criar (o Visual Studio troca o TFM automaticamente para `net9.0-android`).
4. Aperte **F5**. A primeira execução demora mais (compila para Android e o emulador precisa terminar de ligar) — nas próximas é mais rápido.

## 6. Testar num celular Android físico

### 6.1 Rodar direto do Visual Studio no celular (mais rápido, para testar durante o desenvolvimento)
1. No celular: `Configurações > Sobre o telefone`, toque 7 vezes seguidas em **"Número da versão do build"** até aparecer a mensagem "Você agora é um desenvolvedor".
2. Volte em `Configurações`, entre em **"Opções do desenvolvedor"** (pode estar dentro de "Sistema") e ative **"Depuração USB"**.
3. Conecte o celular no computador via cabo USB. Na tela do celular vai aparecer um aviso perguntando se autoriza a depuração USB desse computador — toque em **Permitir**.
4. No Visual Studio, no mesmo menu suspenso de alvo de execução, o celular deve aparecer listado pelo nome do aparelho. Selecione-o.
5. Aperte **F5** — o Visual Studio compila, instala e abre o app automaticamente no celular.

### 6.2 Gerar um arquivo .apk para instalar sem precisar do Visual Studio depois
Use esse caminho se quiser distribuir o app para alguém testar sem precisar conectar no computador.

1. No Gerenciador de Soluções, clique com o botão direito no projeto (`AquaMap` ou `AquaMap.Public`) e escolha **"Publicar..."** ("Publish...").
2. Selecione o alvo **Android**.
3. No modo de distribuição, escolha **"Ad Hoc"** (instalação direta fora da Play Store, ideal para testes internos).
4. Se o assistente pedir uma identidade de assinatura ("Signing identity") e não houver nenhuma configurada ainda, escolha a opção de **criar uma nova** (o próprio Visual Studio gera um certificado de teste) — o projeto não tem uma keystore de produção configurada ainda, então essa é a opção esperada.
5. Conclua o assistente. Ele gera um arquivo `.apk` (por padrão em algo como `AquaMap\bin\Release\net9.0-android\publish\`).
6. Copie esse `.apk` para o celular por qualquer meio (cabo USB, e-mail, Google Drive, WhatsApp Web etc.).
7. No celular, abra o arquivo `.apk` pelo gerenciador de arquivos. Se aparecer um aviso de **"instalar apps de fontes desconhecidas"**, autorize para esse app/origem.
8. Toque em **Instalar** e depois **Abrir**.

> O menu exato pode variar um pouco conforme a versão do Visual Studio ("Publicar" às vezes aparece como `Compilar > Publicar Projeto` ou direto no clique-direito do projeto), mas o fluxo — Publicar → Android → Ad Hoc → gerar .apk — é sempre esse.

## 7. Roteiro sugerido de testes

**App Técnico (`AquaMap`)**
- Login com o usuário admin (§3).
- Listar reservatórios na tela inicial e conferir os novos cards de métricas (total de reservatórios, total de coletas, em alerta).
- Cadastrar um reservatório novo.
- Registrar uma coleta: testar os três jeitos de definir localização — captura por GPS, toque no mapa, e digitação manual de lat/lng.
- Abrir o detalhe de um reservatório com histórico e exportar o boletim em **PDF**.
- Ativar o modo avião, tentar registrar uma coleta offline, reconectar e conferir se ela sincroniza (ver aviso sobre perda de Ferro/GPS na sincronização em `HANDOFF.md`, item 3).

**App Cidadão (`AquaMap.Public`)**
- Abrir sem login, conferir o mapa com a legenda nova.
- Clicar num reservatório e ver o boletim de qualidade da água, incluindo o parâmetro **Ferro** novo.

## 8. Problemas comuns

- **App Cidadão fecha sozinho ao abrir o `.exe` direto pela pasta `bin/`, fora do Visual Studio**: isso é uma falha conhecida de empacotamento ao rodar o executável bruto no Windows (detalhada em `HANDOFF.md`). Sempre testem rodando via **F5 no Visual Studio** (§4), não clicando duas vezes no `.exe`.
- **Tela de login não entra / dados não carregam**: confirme que o dispositivo tem internet — a API é online, não há modo totalmente offline no app Cidadão, e o app Técnico só funciona offline para dados já carregados anteriormente.
- **"Sessão expirada" no app Técnico**: o token de login dura 8 horas; basta fazer login de novo.
- **Emulador Android muito lento para abrir**: normal na primeira execução; se persistir, verifique se a virtualização (Hyper-V/HAXM) está habilitada no BIOS do computador — o próprio instalador do Visual Studio avisa se faltar algum pré-requisito.

## 9. Onde tirar dúvidas técnicas

- **`README.md`** — visão geral do projeto e como rodar o backend localmente (só necessário se forem mexer na API, não para os testes deste manual).
- **`ARCHITECTURE.md`** — como o sistema é construído por dentro.
- **`HANDOFF.md`** — o que funciona, bugs conhecidos e prioridades — leitura recomendada antes de reportar um comportamento estranho como "bug novo", pode já ser um problema conhecido.
