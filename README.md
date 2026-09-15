# GarageFlow

API de gestão de oficina: clientes, veículos, catálogo de serviços, estoque, orçamentos e ordens de serviço. Este README é a documentação principal da aplicação, dos seus fluxos e da execução no Kubernetes. As entregas anteriores estão preservadas nas seções Fase 2 e Fase 1; a configuração vigente em nuvem é a Fase 3.

## Sumário

- [Fase 3](#fase-3)
  - [Mapa da documentação](#mapa-da-documentação)
  - [Arquitetura da aplicação](#arquitetura-da-aplicação)
  - [Autenticação e autorização](#autenticação-e-autorização)
  - [Abertura e execução de OS](#abertura-e-execução-de-os)
  - [Persistência e decisões](#persistência-e-decisões)
  - [Observabilidade](#observabilidade)
  - [Execução e documentação da API](#execução-e-documentação-da-api)
  - [Deploy da aplicação](#deploy-da-aplicação)
  - [Artefatos e verificação](#artefatos-e-verificação)
- [Fase 2 — entrega anterior](#fase-2)
- [Fase 1 — entrega anterior](#fase-1)

## Fase 3

A solução utiliza AWS Academy em `us-east-1`, API Gateway HTTP API, duas funções Lambda, ALB interno, EKS, PostgreSQL gerenciado e New Relic. O login administrativo continua na API; a autenticação por CPF atende exclusivamente o portal do cliente. A aplicação é um monólito modular em C#/.NET 10, ASP.NET Core Minimal APIs, Mediator, EF Core 10/Npgsql e JWT Bearer.

### Mapa da documentação

Cada README concentra a arquitetura, os diagramas, os artefatos e a operação do seu projeto. ADRs registram decisões permanentes e RFCs preservam a fundamentação dos contratos.

| Projeto | Documentação de referência |
| --- | --- |
| **Aplicação — este repositório** | Módulos, políticas de acesso, abertura de OS, migrations, imagem Docker e workload Kubernetes |
| [Serverless](https://github.com/DiegoRugue/garageflow-serverless#readme) | Componentes e sequência de CPF, JWT, authorizer, pacote Lambda e deploy |
| [Plataforma Kubernetes](https://github.com/DiegoRugue/garageflow-infra-kubernetes#readme) | Visão de nuvem, rede, EKS, Gateway, ALB, contratos e dashboards |
| [Banco gerenciado](https://github.com/DiegoRugue/garageflow-infra-database#readme) | PostgreSQL/RDS, justificativa, ER, relacionamentos, índices e backups |

### Arquitetura da aplicação

As setas abaixo representam dependências de código. O `Host` é o único executável e composition root; configura autenticação, middleware, observabilidade, banco e registro de endpoints. A API traduz HTTP para casos de uso, a aplicação orquestra e o domínio protege as invariantes.

![Dependências entre as camadas da aplicação](docs/diagrams/architecture-dependencies.png)

[Fonte editável do diagrama](docs/diagrams/architecture-dependencies.mmd).

`Customers`, `Vehicles`, `Users`, `Services`, `InventoryItems` e `WorkOrders` seguem vertical slices por caso de uso. `Adapters.Api` e `Adapters.Infrastructure` não dependem entre si. Ports pertencem à Application e os adapters implementam a entrada HTTP e as integrações externas. A [linguagem ubíqua](docs/ddd/ubiquitous-language.md), os [agregados](docs/ddd/diagrams/images/aggregates.png) e a [máquina de estados](docs/ddd/diagrams/images/work-order-state-machine.png) complementam esta visão.

### Autenticação e autorização

| Consumidor | Entrada | Responsabilidade |
| --- | --- | --- |
| Administrador/funcionário | `POST /auth/login`, e-mail e senha | API verifica credenciais e emite JWT; políticas limitam operações por perfil |
| Cliente | `POST /auth/customers/token`, CPF e senha | Lambda valida CPF, consulta API privada e emite JWT |
| Lambda de autenticação | `POST /internal/auth/customer-credentials/verify` | API verifica senha, vínculo e status; resposta interna não contém JWT de usuário |
| Cliente autenticado | `/me/work-orders` e decisões de orçamento | API exige cliente ativo e propriedade da OS |
| Integração de orçamento | `POST /webhooks/estimate-decisions` | Validação HMAC e deduplicação da decisão |

A [sequência de autenticação](https://github.com/DiegoRugue/garageflow-serverless#sequência-de-autenticação) fica no README serverless. O Gateway usa Lambda authorizer para validar o JWT nas rotas protegidas; a API valida novamente o token e aplica perfil, troca obrigatória de senha, situação do cliente e propriedade. Cliente suspenso perde acesso inclusive com token previamente emitido. CPF de cliente não concede abertura ou gestão administrativa de OS.

O verificador interno é opt-in por `Auth__Internal__Enabled`; exige uma chave própria em `Auth__Internal__Key`, diferente da chave JWT de usuários. O JWT de serviço tem `iss=GarageFlow.Serverless`, `aud=GarageFlow.InternalAuth`, `sub=customer-auth-function`, `scope=customer-credentials:verify` e validade máxima de 60 segundos. A rota não entra no OpenAPI público nem no catálogo do Gateway. Configuração inválida impede a inicialização quando habilitada. O [RFC de identidade](docs/architecture/rfcs/0001-phase-3-platform-and-identity.md) detalha os contratos.

### Abertura e execução de OS

Funcionários ativos podem abrir OS com cadastros existentes (`POST /work-orders`) ou com o payload de entrada completa (`POST /work-orders/intake`). A segunda opção resolve os cadastros necessários e usa recibo de idempotência. O fluxo abaixo descreve a primeira, sem pressupor que toda criação de OS gera uma notificação SNS.

![Sequência de criação de uma ordem de serviço](docs/diagrams/work-order-creation.png)

[Fonte editável do diagrama](docs/diagrams/work-order-creation.mmd).

Os endpoints declaram os status esperados no [OpenAPI local](http://localhost:8080/openapi/v1.json). Validação, recurso inexistente e conflito são erros distintos; detalhes internos de exceções inesperadas não são devolvidos no `500`.

O orçamento aprovado inicia `InProgress` e registra `StartedAt`; a conclusão dos serviços do orçamento aprovado leva a `Completed` e preenche `CompletedAt`. A entrega do veículo ocorre depois, em `Delivered`, preservando os timestamps. O indicador de execução da OS é **`CompletedAt − StartedAt`**, contado uma vez por ordem. Ele não mede separadamente Diagnóstico, Execução e Finalização nem o tempo de espera anterior à aprovação.

### Persistência e decisões

O [README do banco](https://github.com/DiegoRugue/garageflow-infra-database#modelo-relacional) documenta o ER, cardinalidades, constraints, índices e justificativa do PostgreSQL. Este repositório é dono das [configurações EF](Adapters.Infrastructure) e [migrations](Adapters.Infrastructure/DataAccess/Migrations); Terraform provisiona a instância, não as tabelas.

Commands passam por `TransactionBehavior`: begin, handler, save, coleta de eventos, commit e dispatch após commit. Queries são livres de efeitos colaterais. A outbox de integração armazena mensagens para publicação posterior no SNS, com retries e lease por processamento. A entrega é pelo menos uma vez: sucesso de publicação no SNS não comprova recebimento pelo destinatário. A inbox e o recibo de intake têm finalidades próprias de idempotência.

| Registro | Decisão |
| --- | --- |
| [ADR 0001](docs/architecture/adrs/0001-four-repositories-on-aws-academy.md) | Quatro repositórios e continuidade na AWS Academy |
| [ADR 0002](docs/architecture/adrs/0002-private-api-opentelemetry.md) | OpenTelemetry privado, privacidade e limites de coleta |
| [ADR 0003](docs/architecture/adrs/0003-work-order-business-metrics.md) | Snapshots diários e duração calculada pelos timestamps persistidos |
| [RFC 0001](docs/architecture/rfcs/0001-phase-3-platform-and-identity.md) | Contratos, identidade, transporte e separação de responsabilidades |

### Observabilidade

O `Host` exporta traces, métricas e logs estruturados para o coletor privado da plataforma quando `Observability__Enabled=true`. Configure `Observability__OtlpEndpoint=http://garageflow-otel.newrelic.svc.cluster.local:4318` e ambiente explícito. A chave New Relic fica no coletor. A instrumentação usa `service.name=garageflow-api`; Lambda e Npgsql não são instrumentados.

| Sinal | Origem e interpretação |
| --- | --- |
| Latência, volume HTTP e 5xx | Métricas HTTP da API; incluem probes |
| Logs HTTP | Método, template da rota, status, duração, Trace ID e Span ID |
| Volume diário e duração | Snapshots do banco, hoje e seis dias anteriores em São Paulo; atualização a cada cinco minutos |
| Integrações | Resultados e polls da outbox, com categorias fixas e correlação sanitizada |
| Saúde | `/health/live` verifica o processo; `/health/ready` inclui banco; `/health` responde estado básico |

Os snapshots diários usam `latest` por data; somá-los entre réplicas duplicaria valores. As métricas de resultados/polls são counters e usam soma. A média fica vazia sem conclusões elegíveis; zero é uma duração zero registrada. Logs excluem CPF, credenciais, corpos, URLs brutas, SQL e mensagens de exceção. Traces podem ser amostrados; ausência de telemetria não prova uptime.

Os [dashboards, consultas e condições de alerta](https://github.com/DiegoRugue/garageflow-infra-kubernetes#observabilidade-com-new-relic) são artefatos da plataforma. Os templates de alerta são desabilitados por padrão. Probes internos, disponibilidade externa e ativação das notificações são verificações separadas.

### Execução e documentação da API

Pré-requisitos locais: Docker Desktop, .NET SDK definido em [global.json](global.json) e portas livres conforme [docker-compose.yml](docker-compose.yml).

```bash
docker compose up -d --build
curl http://localhost:8080/health/ready
```

- [Scalar / referência interativa](http://localhost:8080/scalar/).
- [OpenAPI JSON, importável no Postman](http://localhost:8080/openapi/v1.json).
- [Contrato do login CPF, publicado pela Lambda](https://github.com/DiegoRugue/garageflow-serverless#contrato-http).
- [Pipelines da aplicação](https://github.com/DiegoRugue/GarageFlow/actions).

Na nuvem, descubra a URL HTTPS do ambiente pelo contrato da plataforma, conforme seu [README](https://github.com/DiegoRugue/garageflow-infra-kubernetes#acesso-e-documentação-das-apis). O Gateway não publica Scalar, OpenAPI, probes ou `/internal/*`. Para consultar a documentação de uma implantação, um operador com acesso autorizado ao cluster pode usar `kubectl -n garageflow port-forward service/garageflow-api 8080:80`. Não há promessa de endpoint permanente na Academy: sessão e credenciais duram cerca de quatro horas.

O seed e os parâmetros de desenvolvimento estão na [seção local preservada](#como-subir-com-docker-compose). Para encerrar os containers sem remover os volumes: `docker compose down`.

### Deploy da aplicação

O workflow `Deploy Phase 3 Application` executa a qualidade do commit e implanta após push em `develop` (homologação) ou `main` (produção). Recuperação manual usa as mesmas branches e validações. O workflow manual da Fase 2 permanece separado; não o execute sobre recursos cuja propriedade já tenha sido transferida para os novos roots.

Ordem de provisionamento: **platform → database e ingress → aplicação → serverless → edge**. A aplicação lê contratos platform v1, database v1 e ingress v2 do bucket protegido, confere conta AWS, VPC, EKS, banco privado, listener interno e regras de acesso ao ALB/NodePort antes de buscar os secrets. Falta de contrato ou ambiente expirado interrompe o deploy.

Configurar os GitHub Environments `homologation` e `production` com secrets `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`, `AWS_SESSION_TOKEN`, `TF_STATE_BUCKET`; variáveis `AWS_ACCOUNT_ID`, `EKS_PUBLIC_ACCESS_CIDRS` (array JSON de CIDRs dos operadores) e, se necessário, `EKS_VERSION` (padrão `1.36`). O acesso temporário do runner ao EKS acrescenta somente seu IPv4 público `/32` aos CIDRs configurados e restaura os CIDRs protegidos ao terminar, inclusive em falhas. Sem CIDRs de operadores, a limpeza desabilita o endpoint público e mantém o privado. Um passo `always()` tenta novamente a limpeza após interrupção do script. A sessão Academy e as credenciais duram cerca de quatro horas; renovar as credenciais de cada Environment antes da execução.

O overlay `k8s/phase3` reutiliza os manifests existentes e troca o Service para NodePort 30080. O ALB interno, gerenciado pela plataforma, é seu chamador externo autorizado. A pipeline habilita o verificador privado após validar essa rede e resolve os secrets em memória; valores entram no Kubernetes pelo stdin, sem arquivos versionados nem saída de logs. A chave de serviço é distinta da chave JWT de usuários. Publicação Docker, migrations, rollout, targets saudáveis e smoke privado precedem o sucesso da pipeline. O smoke usa um túnel local do `kubectl`, ativa a senha administrativa e cria dados sintéticos para validação; a jornada pública pelo Gateway exige verificação posterior.

O HTTPS público usa o endereço execute-api gerenciado. O trecho Lambda/ALB/API usa HTTP privado **sem criptografia nesse trecho**, com restrição por security groups e identidade de serviço. A aplicação continua responsável pelas senhas, status, perfis, troca obrigatória de senha e acesso do cliente às próprias OS.

Antes de publicar a imagem, o destino ECR precisa corresponder à conta/região protegidas e ao repositório existente na AWS; todas as credenciais de bootstrap, incluindo a senha ativa distinta da inicial, são validadas. Na transição da Fase 2, o Secret usa `data` em base64 com aplicação no servidor e remoção verificada da antiga anotação `last-applied-configuration`. Os demais recursos continuam com aplicação pelo cliente, preservando as réplicas atuais do Deployment. A pipeline aguarda a API de métricas antes de aplicar o HPA.

Para inspecionar o overlay localmente, mantendo o resultado fora do repositório:

```bash
kubectl kustomize k8s/phase3 --load-restrictor LoadRestrictionsNone > /tmp/garageflow-phase3.yaml
python -m unittest discover -s scripts/tests -v
bash -n scripts/deploy-phase3.sh
```

`LoadRestrictionsNone` permite que este overlay local reutilize os manifests no diretório pai; ele não referencia manifests remotos. A pipeline verifica os inputs reais antes de aplicar o resultado. Diagramas, contratos e a divisão entre os quatro repositórios estão no [RFC 0001](docs/architecture/rfcs/0001-phase-3-platform-and-identity.md). Os testes locais e a existência da pipeline não comprovam implantação na AWS.

### Artefatos e verificação

| Artefato | Uso |
| --- | --- |
| [Dockerfile](Dockerfile) | Imagem da API e processo de migrations |
| [Overlay da Fase 3](k8s/phase3) | Workload privado; reutiliza Deployment, probes e HPA |
| [HPA](k8s/hpa.yaml) | 2 a 6 réplicas, CPU alvo 60%, memória 70%; depende de Metrics Server e capacidade dos nós |
| [Deploy](.github/workflows/deploy-phase3.yml) | Quality gate, imagem por SHA, migrations, rollout e smoke |
| [Quality gate](.github/workflows/quality-gate.yml) | Build, testes, cobertura, análise e validação de imagem/manifests |
| [Contratos](contracts) | Interface de metadados entre produtores e consumidores |

```bash
dotnet build GarageFlow.slnx
dotnet test Tests/Unit/GarageFlow.Tests.Unit.csproj
dotnet test Tests/Integration/GarageFlow.Tests.Integration.csproj
dotnet test Tests/E2E/GarageFlow.Tests.E2E.csproj
dotnet test GarageFlow.slnx
```

E2E usa PostgreSQL via Testcontainers e requer Docker. Os workflows mapeiam `develop` para `homologation` e `main` para `production`; criar as branches/Environments, configurar credenciais e aplicar proteção com PR/checks obrigatórios são pré-requisitos do setup do GitHub. O YAML não configura essas proteções.

As seções seguintes preservam as entregas anteriores. Não executar o provisionamento monolítico da Fase 2 sobre recursos administrados pelos roots separados da Fase 3.

## Fase 2

### Descrição da solução e objetivos

Na Fase 2, a entrega evolui o comportamento funcional — incluindo tanto a abertura de ordens de serviço com os identificadores dos cadastros existentes quanto a abertura completa que realiza todos os cadastros a partir de um único payload —, amplia a conteinerização com artefatos Kubernetes, adiciona a infraestrutura como código em Terraform e automatiza o ciclo de deploy e remoção na AWS Academy.

Os objetivos desta fase são:

- manter as regras de negócio protegidas pela Clean Architecture e pelos limites de dependência já adotados na Fase 1;
- disponibilizar os dois fluxos de abertura de ordem de serviço: por identificadores existentes e por payload completo;
- empacotar a API em uma imagem Docker reproduzível e identificada pelo SHA do commit;
- executar a aplicação no Amazon EKS com duas réplicas iniciais e escalabilidade horizontal de 2 a 6 pods;
- provisionar rede, processamento, banco, registro de imagens, segredos, notificações e estado remoto por Terraform;
- automatizar validação, provisionamento, migrations, deploy, smoke tests e remoção do ambiente por GitHub Actions.

### Arquitetura proposta

![Arquitetura do GarageFlow na AWS Academy com GitHub Actions, Terraform, S3, ECR, VPC, Load Balancer, EKS, RDS, Secrets Manager e SNS](docs/architecture/garageflow-aws-fase2.png)

> **Figura 1 — Arquitetura GarageFlow na AWS Academy.** Diagrama elaborado com os ícones oficiais do [AWS Architecture Icons](https://aws.amazon.com/architecture/icons/).

#### Componentes da aplicação

- `GarageFlow.Host`: composition root e processo executável da API.
- `GarageFlow.Adapters.Api`: endpoints Minimal API, contratos HTTP e políticas de autorização.
- `GarageFlow.Application`: casos de uso, handlers, portas, pipeline transacional e eventos de aplicação.
- `GarageFlow.Domain`: agregados, entidades, value objects, invariantes e eventos de domínio.
- `GarageFlow.Adapters.Infrastructure`: EF Core, PostgreSQL, autenticação, repositórios e integrações AWS.
- `GarageFlow.SharedKernel`: primitivas e contratos transversais reutilizáveis.
- `WorkOrders`: mantém a abertura por IDs e adiciona a abertura completa com os cadastros necessários em um único payload.
- `Dockerfile`: gera a imagem da API utilizada localmente e no EKS.
- `k8s/`: contém Namespace, ConfigMap, template de Secret, migration Job, Deployment, Service e HPA.

#### Infraestrutura provisionada

| Recurso | Responsabilidade |
| --- | --- |
| Amazon VPC | Isola a solução em duas Availability Zones, com subnets públicas e privadas. |
| Internet Gateway e Classic Load Balancer | Publicam a API e encaminham o tráfego HTTP para o Service Kubernetes. |
| Amazon EKS | Orquestra a migration e os pods da API. |
| Managed Node Group/EC2 | Fornece dois nodes `t3.small` para as cargas do cluster. |
| Kubernetes HPA e Metrics Server | Ajustam a aplicação entre 2 e 6 pods com base em CPU e memória. |
| Amazon RDS | Hospeda o PostgreSQL em subnets privadas e aceita acesso apenas do EKS. |
| Amazon ECR | Armazena imagens imutáveis identificadas pelo SHA do commit. |
| AWS Secrets Manager | Mantém credenciais do banco, chave JWT, administrador inicial e segredo do webhook. |
| Amazon SNS | Envia notificações de eventos de ordens de serviço por e-mail. |
| Amazon S3 | Armazena o estado remoto versionado e criptografado do Terraform. |
| GitHub Actions | Executa quality gate, provisionamento, publicação da imagem, migrations, deploy e smoke tests. |

#### Fluxo de deploy

1. O workflow valida a confirmação `APPLY`, as credenciais temporárias, os limites da AWS Academy e o suporte da versão do EKS.
2. O Terraform inicializa o backend S3, valida o código, gera o plano e provisiona a infraestrutura em `us-east-1`.
3. O workflow cria ou reutiliza no ECR a imagem imutável marcada com o SHA do commit.
4. O `kubectl` é configurado para o cluster EKS e o Metrics Server é instalado e validado.
5. Configurações públicas são aplicadas por ConfigMap; valores sensíveis são obtidos do Secrets Manager e aplicados diretamente como Secret Kubernetes, sem arquivo renderizado no repositório.
6. O migration Job é executado e precisa concluir antes da atualização da API.
7. O Deployment, o Service `LoadBalancer` e o HPA são aplicados; o workflow aguarda duas réplicas prontas e o endereço público.
8. Smoke tests validam health check, autenticação e o fluxo funcional publicado.

### Instruções de execução

#### Execução local

Pré-requisitos: Docker Desktop em execução e portas `8080`, `5050` e `5432` disponíveis.

```bash
docker compose up -d --build
curl http://localhost:8080/health
```

A API fica disponível em `http://localhost:8080` e a documentação em `http://localhost:8080/scalar/#description/introduction`. Para encerrar:

```bash
docker compose down
```

O detalhamento do ambiente local, seed e credenciais de desenvolvimento permanece na seção [Como subir com Docker Compose](#como-subir-com-docker-compose), preservada da Fase 1.

#### Deploy em Kubernetes

O caminho recomendado é o workflow [Deploy AWS Academy](.github/workflows/deploy-aws-academy.yml), que provisiona a infraestrutura, publica a imagem, aplica os manifests e executa as verificações finais.

O laboratório opera em sessões de quatro horas, com credenciais temporárias e ambiente disponível durante essa janela. A retomada usa a pipeline de provisionamento existente, com o laboratório iniciado e as credenciais renovadas no GitHub Environment. Se o bucket de state ainda não existir na sessão/conta utilizada, execute primeiro o [bootstrap do backend S3](#provisionamento-com-terraform). A ausência de recursos entre sessões é compatível com esse ciclo de uso.

Crie no GitHub o Environment protegido `aws-academy` com:

- Secrets: `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`, `AWS_SESSION_TOKEN`, `TF_STATE_BUCKET`, `EKS_CLUSTER_ROLE_ARN` e `EKS_NODE_ROLE_ARN`.
- Variables: `TF_OWNER`, `TF_EXPIRES_ON`, `SNS_NOTIFICATION_EMAIL` e `BOOTSTRAP_ADMIN_EMAIL`.

Em **Actions → Deploy AWS Academy → Run workflow**, informe `APPLY` e uma versão do Kubernetes em `STANDARD_SUPPORT` na região `us-east-1`. O workflow executa o deploy completo.

Para aplicar manualmente os artefatos em um cluster já provisionado, exporte `IMAGE`, `SNS_TOPIC_ARN` e as variáveis referenciadas por [k8s/secret.template.yaml](k8s/secret.template.yaml). Em um shell Bash com `aws`, `kubectl` e `envsubst`:

```bash
aws eks update-kubeconfig --region us-east-1 --name garageflow-academy

kubectl apply -f k8s/namespace.yaml
kubectl apply -f k8s/configmap.yaml
kubectl -n garageflow patch configmap garageflow-config --type merge \
  --patch "{\"data\":{\"Integrations__Sns__TopicArn\":\"${SNS_TOPIC_ARN}\"}}"
envsubst < k8s/secret.template.yaml | kubectl apply -f -

kubectl -n garageflow delete job garageflow-migration --ignore-not-found --wait=true
kubectl set image --local -f k8s/migration-job.yaml \
  migration="${IMAGE}" -o yaml | kubectl apply -f -
kubectl -n garageflow wait --for=condition=complete \
  job/garageflow-migration --timeout=10m

kubectl set image --local -f k8s/deployment.yaml \
  api="${IMAGE}" -o yaml | kubectl apply -f -
kubectl apply -f k8s/service.yaml
kubectl apply -f k8s/hpa.yaml
kubectl -n garageflow rollout status deployment/garageflow-api --timeout=10m
kubectl -n garageflow get pods,service,hpa
```

O cluster precisa ter o Metrics Server funcional para o HPA. O workflow automatizado instala e valida a versão utilizada pela entrega.

#### Provisionamento com Terraform

Pré-requisitos: Terraform, AWS CLI e credenciais temporárias válidas da AWS Academy exportadas como `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY` e `AWS_SESSION_TOKEN`.

Na primeira execução, crie o bucket de estado remoto. Copie [infra/bootstrap/state-backend/terraform.tfvars.example](infra/bootstrap/state-backend/terraform.tfvars.example) para `terraform.tfvars`, substitua os valores de exemplo e execute:

```bash
terraform -chdir=infra/bootstrap/state-backend init
terraform -chdir=infra/bootstrap/state-backend validate
terraform -chdir=infra/bootstrap/state-backend plan -out=backend.tfplan
terraform -chdir=infra/bootstrap/state-backend apply backend.tfplan
```

Depois, copie [infra/environments/academy/terraform.tfvars.example](infra/environments/academy/terraform.tfvars.example) para `terraform.tfvars`, informe os ARNs e valores da sessão da AWS Academy e inicialize o ambiente com o bucket retornado pelo bootstrap:

```bash
export TF_STATE_BUCKET="nome-globalmente-unico-do-bucket"

terraform -chdir=infra/environments/academy init \
  -reconfigure \
  -backend-config="bucket=${TF_STATE_BUCKET}" \
  -backend-config="key=academy/terraform.tfstate" \
  -backend-config="region=us-east-1" \
  -backend-config="encrypt=true" \
  -backend-config="use_lockfile=true"

terraform -chdir=infra/environments/academy validate
terraform -chdir=infra/environments/academy plan -out=academy.tfplan
terraform -chdir=infra/environments/academy apply academy.tfplan
terraform -chdir=infra/environments/academy output
```

Os arquivos `terraform.tfvars`, planos e valores sensíveis não devem ser commitados. No fluxo oficial, esses parâmetros são fornecidos pelo Environment protegido do GitHub e o mesmo processo é executado pelo workflow de deploy.

## Fase 1

GarageFlow é uma API para gestão de oficinas automotivas, desenvolvida como entrega do Tech Challenge - Fase 1 da pós-graduação em Software Architecture da FIAP.

O projeto adota Clean Architecture com DDD no domínio, adapters explícitos e organização modular por vertical slices/casos de uso. A API foi construída com Minimal APIs, Mediator, EF Core e PostgreSQL. O ambiente local foi preparado para subir a aplicação completa com Docker Compose, incluindo API, banco de dados e pgAdmin.

### Visão geral

A API centraliza fluxos comuns de uma oficina, como cadastro e manutenção de clientes, usuários, veículos, serviços, itens de estoque e ordens de serviço.

Principais módulos implementados:

- Auth
- Users
- Customers
- Vehicles
- Services
- InventoryItems
- WorkOrders

### Stack

- .NET 10
- ASP.NET Core Minimal APIs
- Mediator
- Entity Framework Core 10
- PostgreSQL com Npgsql
- pgAdmin
- Scalar para documentação da API
- Docker e Docker Compose
- xUnit, Moq e coverlet collector

### Justificativa do banco de dados

O GarageFlow utiliza PostgreSQL por ser um banco relacional robusto, open source e maduro para cenários transacionais. O domínio da oficina depende de consistência entre clientes, veículos, ordens de serviço, orçamentos, peças, insumos e movimentações de estoque; por isso, recursos como transações ACID, constraints, chaves estrangeiras, índices e integridade referencial são importantes para manter as regras do negócio protegidas também na camada de persistência.

A escolha também se alinha bem ao stack técnico do projeto: o PostgreSQL possui suporte estável no Entity Framework Core via Npgsql, funciona de forma simples em ambientes Docker e é adequado para evoluir o MVP sem trocar a base de dados quando surgirem necessidades como relatórios, auditoria, consultas administrativas e otimização de leitura.

### Arquitetura

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

#### Fluxo de uma requisição

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

#### Transações e eventos de domínio

Handlers de comandos não abrem nem fecham transações manualmente. O pipeline `TransactionBehavior` inicia a unidade de trabalho, executa o handler, salva mudanças, coleta eventos de domínio antes do commit, confirma a transação e só então despacha os eventos pelo Mediator.

Esse desenho mantém os handlers focados em orquestração de caso de uso, deixa invariantes dentro do domínio e evita side effects antes de uma transação confirmada. Um exemplo é o envio de email de aprovação de orçamento, disparado por handler de evento em `Application/WorkOrders/Events` e implementado por uma abstração de saída.

#### Convenções por módulo

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

### Documentação DDD

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

### Análise de vulnerabilidades

| Ferramenta | Relatório |
| --- | --- |
| OWASP ZAP / Checkmarx | [ZAP by Checkmarx Scanning Report.pdf](<docs/security/vulnerability-scan/ZAP by Checkmarx Scanning Report.pdf>) |
| SonarQube Cloud | [Overview - GarageFlow in Diego Ruguê SonarQube Cloud.pdf](<docs/security/vulnerability-scan/Overview - GarageFlow in Diego Ruguê SonarQube Cloud.pdf>) |

### Como subir com Docker Compose

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

### Seed local de dados

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

### URLs úteis

| Recurso | URL |
| --- | --- |
| API | http://localhost:8080 |
| Health check | http://localhost:8080/health |
| Documentação Scalar | http://localhost:8080/scalar/#description/introduction |
| OpenAPI JSON | http://localhost:8080/openapi/v1.json |
| pgAdmin | http://localhost:5050/browser/ |

### Credenciais locais

As credenciais abaixo são usadas apenas no ambiente local criado pelo `docker-compose.yml`.

#### pgAdmin

| Campo | Valor |
| --- | --- |
| Email | `admin@garageflow.dev` |
| Senha | `admin` |

#### PostgreSQL

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

#### Usuário administrador inicial da API

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

#### Senha inicial de usuários criados pela API

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

### Variáveis de ambiente

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

### Comandos de desenvolvimento

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

### Estrutura do repositório

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

### Observações

- A API aplica migrations automaticamente no boot quando `Database__AutoMigrate=true`.
- O seed local automático é controlado por `Database__AutoSeed` (padrão da aplicação: `false`; padrão do Docker Compose local: `true`).
- O Dockerfile publica a API em modo `Release` e expõe a porta `8080`.
- O ambiente local usa JWT com chave de desenvolvimento definida no `docker-compose.yml`.
- Valores sensíveis devem ser alterados antes de qualquer uso fora do ambiente local.
