AquaMap - Documentação do Projeto

1. Visão Geral
O AquaMap é uma aplicação multiplataforma (Mobile/Desktop) desenvolvida em .NET MAUI 9.0 para monitoramento da qualidade da água. O sistema permite o cadastro de pontos de coleta e a visualização de análises laboratoriais (pH, turbidez, etc.) com foco em áreas rurais e urbanas.


2. Arquitetura (Clean Architecture)
O projeto segue estritamente a separação de responsabilidades para garantir manutenção e testes.

Estrutura da Solução
A solução está dividida em projetos distintos, incluindo dois apps MAUI (AquaMap para o técnico do SAAE, AquaMap.Public somente leitura para o cidadão) que compartilham o mesmo backend. É proibido que a camada de Domínio conheça a camada de Tela ou Banco de Dados.

Plaintext

📂 AquaMap.sln
 ├── 📂 AquaMap (Camada de Apresentação / UI - App Técnico)
 │    ├── 📂 ViewModels       # Lógica de apresentação (MVVM)
 │    ├── 📂 Views            # Telas (XAML) - Atualmente MainPage na raiz
 │    ├── 📄 MauiProgram.cs   # Injeção de Dependência e Configuração
 │    └── 📄 AppShell.xaml    # Navegação central
 │
 ├── 📂 AquaMap.Public (Apresentação - App Cidadão, somente leitura, sem autenticação)
 │    ├── 📂 ViewModels
 │    └── 📂 Views
 │
 ├── 📂 AquaMap.Client.Shared (código client compartilhado pelos dois apps acima)
 │    ├── 📄 ApiService.cs         # Cliente HTTP da AquaMap.Api
 │    └── 📄 ApiClientFactory.cs   # Resolve BaseUrl a partir do appsettings.json de cada app
 │
 ├── 📂 AquaMap.Domain (Camada Core - Pura)
 │    └── 📂 Entities         # Modelos (User, Reservoir, Neighborhood, WaterAnalysis)
 │
 ├── 📂 AquaMap.Infrastructure (Camada de Dados)
 │    ├── 📂 Data             # Contexto do Banco (AppDbContext)
 │    ├── 📂 Migrations       # Migrações do EF Core
 │    └── 📂 Services         # Serviços de infra (ex: SeedDataService)
 │
 └── 📂 AquaMap.Application (Camada de Aplicação)
      └── (Reservado para casos de uso complexos futuros)

      
3. Tecnologias Principais
.NET 9 (MAUI): Framework principal.

SQLite: Banco de dados local embarcado.

Entity Framework Core 9: ORM para manipulação do banco (Code-First).

MVVM: Padrão de projeto para interface (Model-View-ViewModel).


4. Configuração e Execução (Getting Started)
Pré-requisitos
Visual Studio 2022 (v17.12 ou superior).

Workload instalada: "Desenvolvimento com .NET Multi-platform App UI".

Como Rodar o Projeto
Abra o arquivo AquaMap.sln no Visual Studio.

Defina o projeto AquaMap como "Projeto de Inicialização" (clique direito -> Definir como Projeto de Inicialização).

Escolha o emulador/dispositivo: Windows Machine (mais rápido para testes) ou Android Emulator.

Pressione F5 ou o botão de Play.

Nota sobre o Banco de Dados: O sistema possui um SeedDataService que roda automaticamente na inicialização. Se o banco não existir, ele cria o arquivo aquamap.db e insere dados de teste (ex: "Praça Central"). Não é necessário rodar scripts SQL manualmente.


5. Guia de Desenvolvimento (Padrões)
Fluxo de Dados (Data Flow)
O aplicativo segue um fluxo unidirecional estrito:

Banco de Dados (SQLite) ➔ Carregado pelo Repository (Infra).

Repository ➔ Retorna Entidades de Domínio para a ViewModel.

ViewModel ➔ Prepara os dados em ObservableCollection.

View (XAML) ➔ Exibe os dados via Binding.

Como adicionar uma nova funcionalidade?
Siga o fluxo "De dentro para fora":

Domain: Crie a Entidade e a Interface do Repositório.

Infrastructure: Implemente o Repositório e configure no AppDbContext.

Presentation: Crie a ViewModel, a View (Page) e registre tudo no MauiProgram.cs.

Convenções
Idioma: Todo o código (classes, variáveis e comentários) deve ser em Inglês.

Async: Utilize async/await para todas as operações de I/O (Banco de dados).

UI: Use Border em vez de Frame (padrão .NET 9).


6. Status Atual do Projeto (Fase 1 - Concluída)
[x] Estrutura de projetos criada.

[x] Banco de dados SQLite configurado e rodando.

[x] Seed Data (Dados iniciais) implementado.

[x] Listagem de Pontos de Coleta na tela principal.

[ ] (Próximo) Implementação de indicadores visuais de status (Cores).

[ ] (Próximo) Navegação para detalhes.
