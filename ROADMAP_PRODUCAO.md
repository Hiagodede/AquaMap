# 🚀 AquaMap - Roadmap de Estabilização e Produção (FAPES)

## 📌 Contexto e Objetivo Principal
O AquaMap é um sistema Cliente-Servidor (.NET MAUI + .NET 10 Minimal APIs + PostgreSQL) para monitoramento de qualidade da água (SAAE Alegre). A base tecnológica e as regras de negócio (Portaria 888) estão concluídas no backend. 
**Objetivo Atual:** Sair do ambiente de laboratório (`localhost`, `Docker` local, testes via `Thunder Client`) e transformar a PoC em um produto maduro, seguro e testável End-to-End no dispositivo móvel para homologação da FAPES.

## 🛑 Diretrizes Restritivas (O que NÃO fazer)
1. **Zero Feature Creep:** Não sugira nem crie novas funcionalidades de negócio. O foco é estabilização e infraestrutura.
2. **Zero Hardcoding:** É estritamente proibido criar código com IPs fixos (ex: `10.0.2.2`, `localhost`) ou expor chaves secretas (JWT `IssuerSigningKey`) no código-fonte.
3. **Fim da Gambiarra no Mapa:** O uso de `WebView` com iframe do OpenStreetMap no Android deve ser extirpado devido à performance e falta de offline capabilities.

## 🛠️ Plano de Ação Sequencial (Prioridade Máxima)

### Fase 1: Higiene de Configuração e Segurança
- **Tarefa:** Implementar gestão profissional de ambientes.
- **Ações:** - Criar injeção de configuração baseada no ambiente (Development vs. Production).
  - Configurar a Base URL da API no App MAUI via configuração, removendo o redirecionamento manual de IPs.
  - Isolar a chave JWT no backend utilizando `UserSecrets` (dev) ou variáveis de ambiente/Key Vault (produção).

### Fase 2: Fechamento do Ciclo do Usuário (MAUI Frontend)
- **Tarefa:** Eliminar a dependência do Postman/Thunder Client para inserção de dados.
- **Ações:**
  - Criar a **Tela de Login** nativa no MAUI para técnicos do SAAE.
  - Implementar armazenamento seguro do token usando `Microsoft.Maui.Storage.SecureStorage`.
  - Criar o **Formulário de Coleta** no MAUI (inputs de Cloro, pH, Turbidez, E. Coli) conectando à rota `POST /water-analysis` com o token no Header `Bearer`.

### Fase 3: Refatoração do Motor de Mapeamento
- **Tarefa:** Garantir fluidez e performance de interface no Android.
- **Ações:**
  - Substituir o `WebView` no Android pela implementação nativa `Microsoft.Maui.Controls.Maps`.
  - Configurar as chaves de API necessárias (Google Maps API Key).
  - Implementar lógica na View para renderizar Pinos (Pins) cujas cores mudam dinamicamente com base no status da água (Verde/Amarelo/Vermelho) recebido da API.

### Fase 4: Cloud Deploy (Preparação)
- **Tarefa:** Colocar o cérebro do sistema na nuvem.
- **Ações:**
  - Migrar o banco PostgreSQL do Docker local para um serviço Cloud (Supabase, Neon, ou RDS).
  - Fazer o deploy da API (.NET 10) em um serviço de hospedagem (Render, Azure App Service, AWS).