# Revisão técnica da base GarageFlow (2026-04-28)

## Escopo
- Revisão estática de arquitetura, domínio, aplicação, API, infraestrutura e testes.
- Sem execução de build/testes por limitação de ambiente (`dotnet` indisponível).

## Achados priorizados

### 1) Acoplamento indevido da API com camadas internas (Alta)
**Sintoma**
- A API referencia diretamente `Infrastructure` no projeto e no `Program`.
- Contratos da API (`Request/Response`) usam enums do `Domain`.
- Alguns endpoints dependem de `SharedKernel` para lançar exceções.

**Impacto**
- Viola fronteiras de arquitetura e dificulta evolução independente das camadas.
- Aumenta risco de quebra na API ao evoluir modelo de domínio interno.

**Evidências**
- `Api/GarageFlow.Api.csproj` referencia `Infrastructure`.  
- `Api/Program.cs` usa extensão de infraestrutura para data access/migration.  
- `Api/Users/CreateUser/CreateUserRequest.cs` usa `GarageFlow.Domain.Users.Enums`.  
- `Api/InventoryItems/CreateInventoryItem/CreateInventoryItemRequest.cs` usa `GarageFlow.Domain.InventoryItems.Enums`.  
- `Api/Customers/GetCustomerById/GetCustomerByIdEndpoint.cs` usa `NotFoundException` de `SharedKernel`.

**Recomendação**
- Introduzir contratos próprios da API para enums (ex.: `UserRoleContract`, `InventoryItemTypeContract`) e mapear para Application.
- Mover tratamento de not-found para Application (handler lança `NotFoundException`) ou para uma camada de tradução no pipeline.
- Isolar bootstrap de infraestrutura em composition root com acoplamento explícito e mínimo (idealmente fora da assembly da API).

---

### 2) Dívida arquitetural já conhecida e institucionalizada em allowlist (Alta)
**Sintoma**
- Teste arquitetural mantém allowlist de tipos com violação permitida.

**Impacto**
- Normaliza desvio arquitetural e enfraquece proteção regressiva dos testes.

**Evidências**
- `Tests/Unit/Architecture/DependencyRulesTests.cs` possui `KnownApiEndpointDependencyDrift` com endpoints acoplados.

**Recomendação**
- Criar backlog técnico explícito para reduzir allowlist por sprint.
- Exigir documentação de drift (arquivo dedicado) e data alvo de remoção por item.

---

### 3) Inconsistência de responsabilidade entre API e Application para "not found" (Média)
**Sintoma**
- Handlers `GetById` retornam `null` e endpoint converte para 404 lançando exceção.

**Impacto**
- Endpoints deixam de ser “thin” e passam a conter regra de tradução semântica.
- Multiplica lógica de erro entre endpoints.

**Evidências**
- `Application/Customers/GetCustomerById/GetCustomerByIdHandler.cs` retorna `CustomerDto?` e `null`.
- `Api/Customers/GetCustomerById/GetCustomerByIdEndpoint.cs` testa `null` e lança `NotFoundException`.
- Padrão semelhante em `Application/Services/GetServiceById/GetServiceByIdHandler.cs`.

**Recomendação**
- Padronizar `GetById` para lançar `NotFoundException` no handler.
- Endpoint apenas mapeia request/response.

---

### 4) Domínio de Users sem eventos de domínio em mudanças-chave (Média)
**Sintoma**
- Entidade `User` não publica eventos em create/update/password change.

**Impacto**
- Perde rastreabilidade de mudanças relevantes de segurança/auditoria.
- Fica inconsistente com demais módulos (ex.: Services/InventoryItems).

**Evidências**
- `Domain/Users/Entities/User.cs` não chama `RaiseDomainEvent`.
- `Domain/Services/Entities/Service.cs` e `Domain/InventoryItems/Entities/InventoryItem.cs` publicam eventos.

**Recomendação**
- Introduzir eventos `UserCreated`, `UserProfileUpdated`, `UserPasswordChanged`.
- Adicionar testes unitários cobrindo publicação dos eventos.

---

### 5) Política de senha inicial previsível (Média)
**Sintoma**
- Senha inicial é derivada de sobrenome + ano de nascimento.

**Impacto**
- Forte previsibilidade e aumento de risco de comprometimento de contas recém-criadas.

**Evidências**
- `Domain/Users/Entities/User.cs` em `GenerateInitialPassword` retorna `${lastName}${year}`.
- `Application/Users/CreateUser/CreateUserHandler.cs` usa esse valor para hash da senha inicial.

**Recomendação**
- Gerar senha aleatória forte (ou token de ativação de conta com expiração curta).
- Forçar troca no primeiro login (já existe `MustChangePassword`), com política mínima de complexidade.

---

### 6) Migração automática ligada por padrão (Média)
**Sintoma**
- `Database:AutoMigrate` assume `true` quando não configurado.

**Impacto**
- Risco operacional em produção (startup lento/falho, concorrência entre instâncias, mudanças não planejadas de schema).

**Evidências**
- `Infrastructure/DataAccess/AutoMigrateGarageFlowExtensions.cs`: fallback `?? true` e execução de `Migrate()`.

**Recomendação**
- Default seguro: `false` em produção.
- Manter auto-migrate apenas em dev/teste via environment/profile explícito.

---

### 7) Duplicação de normalização/validação de email (Baixa)
**Sintoma**
- Handlers normalizam email e a entidade também normaliza.

**Impacto**
- Redundância e potencial divergência futura de regra.

**Evidências**
- `Application/Users/CreateUser/CreateUserHandler.cs` e `Application/Users/UpdateMyProfile/UpdateMyProfileHandler.cs` fazem lower-case.
- `Domain/Users/Entities/User.cs` também normaliza em `NormalizeEmail`.

**Recomendação**
- Definir único ponto de verdade (preferencialmente VO/Domain) e remover duplicidade em Application.

---

## Plano de melhoria (roadmap)

### Fase 1 (1 sprint) — Correções de arquitetura e segurança
1. Remover acoplamento API↔Domain nos contratos de request/response via enums próprios da API.
2. Migrar not-found para Application em todos os `GetById`.
3. Substituir geração de senha inicial por fluxo seguro (senha random/token ativação).
4. Introduzir documento formal de drift arquitetural com owner + prazo.

**Critérios de aceite**
- Zero `using GarageFlow.Domain.*Enums` em `Api/**`.
- Zero endpoints lançando exceções de domínio diretamente.
- `KnownApiEndpointDependencyDrift` reduzido ou zerado.

### Fase 2 (1 sprint) — Consistência de domínio e observabilidade
1. Criar eventos de domínio para Users.
2. Adicionar testes unitários para eventos de Users.
3. Padronizar normalização de email em único ponto de verdade.

**Critérios de aceite**
- Testes de domínio cobrindo eventos em create/update/password.
- Redução de lógica duplicada nos handlers.

### Fase 3 (1 sprint) — Operação segura e governança
1. Alterar `AutoMigrate` default para seguro por ambiente.
2. Definir pipeline de migração controlado (CI/CD) para produção.
3. Evoluir testes arquiteturais para falhar sem allowlists permanentes.

**Critérios de aceite**
- Produção não executa auto-migrate por padrão.
- Documentação de deploy com fluxo de migration explícito.
- Testes de arquitetura sem exceções permanentes.

## Métricas sugeridas
- **Architecture Drift Index**: nº de tipos em allowlist de violação.
- **API Purity Index**: nº de referências `Api -> Domain/Infrastructure/SharedKernel` fora de composição permitida.
- **Security Bootstrap Score**: % de usuários criados via fluxo seguro (token/senha aleatória).
- **Domain Event Coverage**: % de mutações de agregados com evento + teste.
