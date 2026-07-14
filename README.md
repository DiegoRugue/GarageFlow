# GarageFlow

GarageFlow é uma API para gestão de oficinas automotivas, desenvolvida como entrega do Tech Challenge - Fase 1 da pós-graduação em Software Architecture da FIAP.

O projeto adota Clean Architecture com DDD no domínio, adapters explícitos e organização modular por vertical slices/casos de uso. A API foi construída com Minimal APIs, Mediator, EF Core e PostgreSQL. O ambiente local foi preparado para subir a aplicação completa com Docker Compose, incluindo API, banco de dados e pgAdmin.

Na Fase 2, a entrega evolui o comportamento funcional — incluindo tanto a abertura de ordens de serviço com os identificadores dos cadastros existentes quanto a abertura completa que realiza todos os cadastros a partir de um único payload —, amplia a conteinerização com artefatos Kubernetes, adiciona a infraestrutura como código em Terraform e automatiza o ciclo de deploy e remoção na AWS Academy.

## Sumário

- [Visão geral](#visão-geral)
- [Stack](#stack)
- [Justificativa do banco de dados](#justificativa-do-banco-de-dados)
- [Arquitetura](#arquitetura)
- [Documentação DDD](#documentação-ddd)
- [Análise de vulnerabilidades](#análise-de-vulnerabilidades)
- [Como subir com Docker Compose](#como-subir-com-docker-compose)
- [Seed local de dados](#seed-local-de-dados)
- [URLs úteis](#urls-úteis)
- [Credenciais locais](#credenciais-locais)
- [Comandos de desenvolvimento](#comandos-de-desenvolvimento)
- [Estrutura do repositório](#estrutura-do-repositório)

## Visão geral

A API centraliza fluxos comuns de uma oficina, como cadastro e manutenção de clientes, usuários, veículos, serviços, itens de estoque e ordens de serviço.

Principais módulos implementados:

- Auth
- Users
- Customers
- Vehicles
- Services
- InventoryItems
- WorkOrders

## Stack

- .NET 10
- ASP.NET Core Minimal APIs
- Mediator
- Entity Framework Core 10
- PostgreSQL com Npgsql
- pgAdmin
- Scalar para documentação da API
- Docker e Docker Compose
- xUnit, Moq e coverlet collector

## Justificativa do banco de dados

O GarageFlow utiliza PostgreSQL por ser um banco relacional robusto, open source e maduro para cenários transacionais. O domínio da oficina depende de consistência entre clientes, veículos, ordens de serviço, orçamentos, peças, insumos e movimentações de estoque; por isso, recursos como transações ACID, constraints, chaves estrangeiras, índices e integridade referencial são importantes para manter as regras do negócio protegidas também na camada de persistência.

A escolha também se alinha bem ao stack técnico do projeto: o PostgreSQL possui suporte estável no Entity Framework Core via Npgsql, funciona de forma simples em ambientes Docker e é adequado para evoluir o MVP sem trocar a base de dados quando surgirem necessidades como relatórios, auditoria, consultas administrativas e otimização de leitura.

## Arquitetura

O GarageFlow foi organizado para combinar Clean Architecture, DDD tático e vertical slices. As camadas internas concentram regras de negócio e contratos; as camadas externas adaptam HTTP, persistência, autenticação, documentação da API e infraestrutura de runtime.

Os módulos de negócio (`Users`, `Customers`, `Vehicles`, `Services`, `InventoryItems` e `WorkOrders`) aparecem verticalmente dentro das camadas. Cada caso de uso possui seus próprios comandos/queries, handlers, resultados e contratos HTTP, evitando que a arquitetura vire apenas uma separação técnica por pastas.

Direção das dependências:

```text
Host
|-- Adapters.Api -----------> Application ---> Domain ---> SharedKernel
|-- Adapters.Infrastructure -> Application
|                            -> Domain
|                            -> SharedKernel
`-- SharedKernel
```

Responsabilidades principais:

- `Host`: composition root e único executável. Configura DI, Mediator, pipeline transacional, dispatch de eventos de domínio, autenticação, middlewares, OpenAPI/Scalar, migrations/seed e registro dos módulos HTTP.
- `Adapters.Api`: adapter inbound HTTP. Contém Minimal API endpoints, contratos de request/response, mapeamento HTTP e políticas de autorização. Depende apenas de `Application`.
- `Application`: casos de uso, comandos, queries, handlers, portas, read models, resultados, pipeline behaviors e handlers de eventos de domínio.
- `Domain`: entidades, value objects, eventos de domínio, enums e invariantes de negócio.
- `Adapters.Infrastructure`: adapter outbound. Implementa portas da aplicação com EF Core, PostgreSQL, migrations, configurações, repositórios, queries, autenticação, hashing e integrações externas.
- `SharedKernel`: primitivas genéricas compartilhadas, exceções, eventos, value objects reutilizáveis e contratos transversais como `IUnitOfWork`.
- `Tests`: projetos de testes unitários, integração, E2E e builders/contratos compartilhados.

### Fluxo de uma requisição

```text
HTTP request
  -> Host middleware/auth
  -> Adapters.Api endpoint
  -> Mediator
  -> Application use case handler
  -> Domain aggregate/value objects
  -> Application ports
  -> Adapters.Infrastructure implementations
  -> TransactionBehavior commit
  -> Domain event dispatch after commit
  -> Adapters.Api response
```

Endpoints HTTP não acessam `Domain`, `SharedKernel` ou `Adapters.Infrastructure` diretamente. Eles traduzem entrada/saída HTTP para comandos/queries da aplicação. O domínio não conhece HTTP, EF Core, Mediator, banco de dados ou adapters.

### Transações e eventos de domínio

Handlers de comandos não abrem nem fecham transações manualmente. O pipeline `TransactionBehavior` inicia a unidade de trabalho, executa o handler, salva mudanças, coleta eventos de domínio antes do commit, confirma a transação e só então despacha os eventos pelo Mediator.

Esse desenho mantém os handlers focados em orquestração de caso de uso, deixa invariantes dentro do domínio e evita side effects antes de uma transação confirmada. Um exemplo é o envio de email de aprovação de orçamento, disparado por handler de evento em `Application/WorkOrders/Events` e implementado por uma abstração de saída.

### Convenções por módulo

```text
Adapters.Api/<Module>/<UseCase>/
Application/<Module>/UseCases/<UseCase>/
Application/<Module>/Ports/
Application/<Module>/ReadModels/
Domain/<Module>/Entities/
Domain/<Module>/ValueObjects/
Domain/<Module>/Events/
Adapters.Infrastructure/<Module>/Configurations/
Adapters.Infrastructure/<Module>/Repositories/
Tests/Shared/<Module>/
Tests/Unit/<Module>/
Tests/Integration/Api/<Module>/
Tests/E2E/<Module>/
```

Regras arquiteturais são protegidas por testes em `Tests/Unit/Architecture`, incluindo direção de dependências, convenções de módulos e ausência de acoplamento da API com domínio/infraestrutura.

## Documentação DDD

| Artefato | Caminho |
| --- | --- |
| Event Storming: criação e acompanhamento da OS | [event-storming-work-orders.png](docs/ddd/diagrams/images/event-storming-work-orders.png) |
| Event Storming: gestão de peças e insumos | [event-storming-inventory.png](docs/ddd/diagrams/images/event-storming-inventory.png) |
| Domain Storytelling: ordem de serviço | [domain-storytelling-work-orders.png](docs/ddd/diagrams/images/domain-storytelling-work-orders.png) |
| Domain Storytelling: peças e insumos | [domain-storytelling-inventory.png](docs/ddd/diagrams/images/domain-storytelling-inventory.png) |
| Mapa de contextos e módulos | [bounded-contexts.png](docs/ddd/diagrams/images/bounded-contexts.png) |
| Modelo de agregados | [aggregates.png](docs/ddd/diagrams/images/aggregates.png) |
| Máquina de estados da OS | [work-order-state-machine.png](docs/ddd/diagrams/images/work-order-state-machine.png) |
| Linguagem ubíqua | [ubiquitous-language.md](docs/ddd/ubiquitous-language.md) |

## Análise de vulnerabilidades

| Ferramenta | Relatório |
| --- | --- |
| OWASP ZAP / Checkmarx | [ZAP by Checkmarx Scanning Report.pdf](<docs/security/vulnerability-scan/ZAP by Checkmarx Scanning Report.pdf>) |
| SonarQube Cloud | [Overview - GarageFlow in Diego Ruguê SonarQube Cloud.pdf](<docs/security/vulnerability-scan/Overview - GarageFlow in Diego Ruguê SonarQube Cloud.pdf>) |

## Como subir com Docker Compose

Pré-requisitos:

- Docker Desktop instalado e em execução.
- Porta `8080` livre para a API.
- Porta `5050` livre para o pgAdmin.
- Porta `5432` livre para o PostgreSQL.

Na raiz do repositório, execute:

```bash
docker compose up -d --build
```

Esse comando sobe três serviços:

- `api`: aplicação GarageFlow em `http://localhost:8080`.
- `postgres`: banco PostgreSQL usado pela API.
- `pgadmin`: interface web para administrar o banco.

Para acompanhar os logs da API:

```bash
docker compose logs -f api
```

Para verificar se a API está respondendo:

```bash
curl http://localhost:8080/health
```

Resposta esperada:

```json
{
  "status": "ok"
}
```

Para parar os containers:

```bash
docker compose down
```

Para parar os containers e remover os volumes locais do PostgreSQL e pgAdmin:

```bash
docker compose down -v
```

## Seed local de dados

No ambiente local com Docker Compose, a API aplica automaticamente o script idempotente em [scripts/seed-local.sql](scripts/seed-local.sql) após as migrations e a criação do usuário administrador inicial.

O comportamento é controlado por `Database__AutoSeed` (ou `DATABASE_AUTO_SEED` no `.env`):

- `true`: aplica o seed automaticamente no boot.
- `false`: não aplica seed automático.

Por padrão da aplicação, `Database:AutoSeed` é `false` para evitar seed acidental fora de desenvolvimento. No `docker-compose.yml`, o padrão local está definido como `true`.

Também é possível customizar o caminho do script com `Database:SeedScriptPath` (ou `Database__SeedScriptPath` via ambiente). O padrão é `scripts/seed-local.sql`.

Se quiser executar manualmente, com `psql` instalado na máquina:

```bash
psql "postgresql://garageflow:garageflow@localhost:5432/garageflow" -f scripts/seed-local.sql
```

O script cria pelo menos 5 clientes, 5 veículos, 5 serviços e 5 ordens de serviço, além dos cadastros auxiliares necessários para veículos e orçamentos.

## URLs úteis

| Recurso | URL |
| --- | --- |
| API | http://localhost:8080 |
| Health check | http://localhost:8080/health |
| Documentação Scalar | http://localhost:8080/scalar/#description/introduction |
| OpenAPI JSON | http://localhost:8080/openapi/v1.json |
| pgAdmin | http://localhost:5050/browser/ |

## Credenciais locais

As credenciais abaixo são usadas apenas no ambiente local criado pelo `docker-compose.yml`.

### pgAdmin

| Campo | Valor |
| --- | --- |
| Email | `admin@garageflow.dev` |
| Senha | `admin` |

### PostgreSQL

| Campo | Valor |
| --- | --- |
| Host dentro do Docker | `postgres` |
| Host a partir da máquina local | `localhost` |
| Porta | `5432` |
| Database | `garageflow` |
| Usuário | `garageflow` |
| Senha | `garageflow` |

Ao criar um servidor no pgAdmin, use:

- `Host name/address`: `postgres`
- `Port`: `5432`
- `Maintenance database`: `garageflow`
- `Username`: `garageflow`
- `Password`: `garageflow`

### Usuário administrador inicial da API

| Campo | Valor |
| --- | --- |
| Nome | `Development Admin` |
| Email | `admin-dev@garageflow.local` |
| Senha | `Admin@12345` |

O administrador inicial é criado como usuário ativo, mas com troca de senha obrigatória (`MustChangePassword=true`). Para testar endpoints protegidos, faça primeiro o login com a senha inicial e, em seguida, altere a senha pela rota:

```http
PUT /users/me/password
```

Exemplo de payload:

```json
{
  "currentPassword": "Admin@12345",
  "newPassword": "Admin.Dev.Active#123"
}
```

Depois da troca, faça login novamente com a nova senha e use o token JWT retornado para acessar os endpoints protegidos. Enquanto a senha temporária não for alterada, o acesso às rotas de negócio permanece bloqueado pela regra de usuário ativo.

### Senha inicial de usuários criados pela API

Usuários criados pela API, como atendentes criados em `POST /users` e clientes ativados em `POST /customers/{id}/portal-user`, recebem uma senha inicial temporária calculada a partir do nome completo e da data de nascimento:

```text
<ultimo-sobrenome-em-minusculo><ano-de-nascimento>
```

Exemplos:

| Nome completo | Data de nascimento | Senha inicial |
| --- | --- | --- |
| `Maria Oliveira` | `1991-01-10` | `oliveira1991` |
| `João Carlos Santos` | `1985-05-20` | `santos1985` |

Esses usuários também são criados com `MustChangePassword=true`. O primeiro login serve apenas para obter um token temporário e chamar `PUT /users/me/password`; depois disso, faça login novamente com a nova senha para acessar endpoints protegidos por políticas de usuário ativo.

## Variáveis de ambiente

O `docker-compose.yml` possui valores padrão para desenvolvimento local, mas eles podem ser sobrescritos com um arquivo `.env` na raiz do projeto.

Exemplo:

```env
API_PORT=8080
PGADMIN_PORT=5050
POSTGRES_PORT=5432
POSTGRES_DB=garageflow
POSTGRES_USER=garageflow
POSTGRES_PASSWORD=garageflow
DATABASE_AUTO_MIGRATE=true
DATABASE_AUTO_SEED=true
DATABASE_SEED_SCRIPT_PATH=scripts/seed-local.sql
BOOTSTRAP_ADMIN_EMAIL=admin-dev@garageflow.local
BOOTSTRAP_ADMIN_PASSWORD=Admin@12345
```

## Comandos de desenvolvimento

Restaurar e compilar a solução:

```bash
dotnet build GarageFlow.slnx
```

Executar testes unitários:

```bash
dotnet test Tests/Unit/GarageFlow.Tests.Unit.csproj
```

Executar testes de integração:

```bash
dotnet test Tests/Integration/GarageFlow.Tests.Integration.csproj
```

Executar testes E2E reais:

```bash
dotnet test Tests/E2E/GarageFlow.Tests.E2E.csproj
```

Executar testes E2E reais via script PowerShell:

```powershell
./scripts/run-e2e.ps1
```

```powershell
.\scripts\run-e2e.ps1
```

Executar todos os testes da solution:

```bash
dotnet test GarageFlow.slnx
```

Observações:

- Docker precisa estar em execução para os testes E2E, pois a suíte sobe PostgreSQL via Testcontainers.
- O projeto `Tests/E2E/GarageFlow.Tests.E2E.csproj` está incluído no `GarageFlow.slnx`, então `dotnet test GarageFlow.slnx` também exige Docker em execução.

## Estrutura do repositório

```text
GarageFlow/
|-- Adapters.Api/
|-- Adapters.Infrastructure/
|-- Application/
|-- SharedKernel/
|-- Domain/
|-- Host/
|-- scripts/
|-- Tests/
|   |-- E2E/
|   |-- Integration/
|   |-- Shared/
|   `-- Unit/
|-- docker-compose.yml
|-- Dockerfile
`-- GarageFlow.slnx
```

## Observações

- A API aplica migrations automaticamente no boot quando `Database__AutoMigrate=true`.
- O seed local automático é controlado por `Database__AutoSeed` (padrão da aplicação: `false`; padrão do Docker Compose local: `true`).
- O Dockerfile publica a API em modo `Release` e expõe a porta `8080`.
- O ambiente local usa JWT com chave de desenvolvimento definida no `docker-compose.yml`.
- Valores sensíveis devem ser alterados antes de qualquer uso fora do ambiente local.
