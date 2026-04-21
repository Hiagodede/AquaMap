# AquaMap - Contexto de Engenharia e Arquitetura

## 1. Regra de Negócio (Domínio)
- **Cliente:** SAAE Alegre.
- **Escopo Base:** Monitoramento da qualidade da água baseado na Portaria GM/MS Nº 888.
- **Modelo Relacional:** Relação estrita de 1:N entre `Reservoir` (Reservatório/Zona de Abastecimento) e `Neighborhood` (Bairros Atendidos). A entidade antiga de "Pontos de Coleta" foi descartada.
- **Cálculo de Potabilidade:** Encapsulado na entidade `WaterAnalysis`. Parâmetros avaliados: Cloro Residual (0.2 a 5.0), pH (6.0 a 9.5), Turbidez (Máx 5.0) e Ausência de E. Coli.

## 2. Estado Atual do Projeto (Fase 1 - Concluída)
- **Stack:** .NET 9 MAUI, C#, SQLite local, Entity Framework Core.
- **Status:** Prova de Conceito (PoC) rodando na máquina local. O banco SQLite é destruído e recriado via `SeedDataService` durante o App Startup para validar a UI. A tela principal lista os reservatórios com formatação visual de status (Verde, Amarelo, Vermelho).

## 3. Próximo Passo Arquitetural (Fase 2 - API em Nuvem)
- **Objetivo Imediato:** Extrair o banco de dados do aplicativo mobile (descartar SQLite local) e transferir a inteligência de persistência para uma API REST em Nuvem (C# ou Node.js).
- **Justificativa:** O SAAE necessita de um sistema centralizado para inserção de dados. O app MAUI será refatorado para atuar estritamente como *Client*, consumindo endpoints de leitura via `HttpClient`.
- **Roadmap Sequencial:**
  1. Definir e levantar a API REST e Banco Relacional (PostgreSQL).
  2. Implementar segurança/JWT para rotas de inserção (técnicos do SAAE).
  3. Refatorar o App MAUI para consumir a API.
  4. Implementar tela de geomapeamento com *Pins* (Microsoft.Maui.Controls.Maps).