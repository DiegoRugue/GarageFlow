# GarageFlow — Tech Challenge Fase 2

GarageFlow é uma API .NET 10 para operação de oficinas, organizada com Clean
Architecture, DDD tático e vertical slices. A Fase 2 acrescenta abertura
completa e idempotente de ordens de serviço, consultas operacionais, webhook
assinado, inbox/outbox confiáveis, contêiner, Kubernetes e entrega protegida na
AWS Academy.

> Estado da entrega: os ativos reproduzíveis estão versionados. Deploy AWS,
> evidência HPA/SNS, vídeo, acesso do avaliador e PDF final ainda dependem dos
> gates live/humanos descritos abaixo; este README não afirma que ocorreram.

## Índice do avaliador

- [Arquitetura da aplicação](docs/architecture/diagrams/images/application.png)
- [Topologia AWS Academy](docs/architecture/diagrams/images/aws-academy.png)
- [Runtime Kubernetes](docs/architecture/diagrams/images/kubernetes.png)
- [Inbox, outbox e retries](docs/architecture/diagrams/images/database-reliability.png)
- [Fluxo deploy–demonstração–destroy](docs/architecture/diagrams/images/deployment-flow.png)
- [Ciclo da ordem de serviço](docs/ddd/diagrams/images/work-order-state-machine.png)
- [Execução local com Compose](#execução-local-com-docker-compose)
- [Scalar e OpenAPI](#urls-locais)
- [Coleção Postman](docs/postman/GarageFlow.Phase2.postman_collection.json) e [ambiente de exemplo](docs/postman/GarageFlow.Phase2.postman_environment.example.json)
- [Testes, cobertura e smoke](#verificação-local-e-ci)
- [Bootstrap Terraform](#bootstrap-retido-do-estado-terraform)
- [Workflow de deploy](.github/workflows/deploy-aws-academy.yml) e [workflow de destroy](.github/workflows/destroy-aws-academy.yml)
- [Troubleshooting Kubernetes/AWS](#troubleshooting)
- [Runbook da demonstração HPA](docs/delivery/demo-runbook.md)
- [Checklist final](docs/delivery/final-submission-checklist.md)
- [PDF final — pendente](#pdf-final-pendente): será gerado em `docs/entrega-final/Tech-Challenge-Fase-2-GarageFlow.pdf`

## Arquitetura

`GarageFlow.Host` é o único executável e a única raiz de composição. A direção
permitida é:

```text
Host -> Adapters.Api, Adapters.Infrastructure, Application, SharedKernel
Adapters.Api -> Application
Adapters.Infrastructure -> Application, Domain, SharedKernel
Application -> Domain, SharedKernel
Domain -> SharedKernel
```

Endpoints HTTP são adapters finos; regras e invariantes ficam no domínio;
transações e casos de uso ficam na aplicação; EF Core, PostgreSQL, JWT, AWS e
repositórios ficam no adapter de infraestrutura. Veja também a
[documentação DDD](docs/ddd/ubiquitous-language.md) e as fontes renderizáveis em
[`docs/architecture/diagrams/source`](docs/architecture/diagrams/source).

## Comportamento funcional

### Ciclo da ordem de serviço

| Código | Termo da entrega | Próxima transição normal |
| --- | --- | --- |
| `Received` | Recebida | `Diagnosing` |
| `Diagnosing` | Diagnóstico | `WaitingApproval` |
| `WaitingApproval` | Aguardando Aprovação | aprovação → `InProgress`; rejeição → `Diagnosing` |
| `InProgress` | Execução | `Completed` quando todos os serviços aprovados terminam |
| `Completed` | Finalizada | `Delivered` |
| `Delivered` | Entregue | terminal |
| `Cancelled` | Cancelada | terminal excepcional |

Uma ordem pode ser cancelada enquanto estiver em `Received`, `Diagnosing`,
`WaitingApproval` ou `InProgress`. `Cancelled` não faz parte do happy path.

### Duas formas de abertura

Ambas exigem JWT de staff ativo.

1. `POST /work-orders` reutiliza cadastros existentes:

```json
{
  "customerId": "11111111-1111-4111-8111-111111111111",
  "vehicleId": "22222222-2222-4222-8222-222222222222"
}
```

Retorna `201 Created`, header `Location`, `id`, `customerId`, `vehicleId`,
`status: "Received"` e `createdAt`. Orçamento, serviços e peças são adicionados
pelos endpoints específicos depois da abertura.

2. `POST /work-orders/intake` cadastra cliente, veículo, serviços, peças
opcionais, orçamento e ordem em uma única transação:

```json
{
  "requestId": "33333333-3333-4333-8333-333333333333",
  "customer": {
    "taxDocument": "52998224725",
    "fullName": "Cliente Sintético Fase Dois",
    "email": "cliente-fase2@example.invalid",
    "phoneNumber": "+5511999999999"
  },
  "vehicle": {
    "plate": "ABC1D23",
    "year": 2024,
    "brand": "Marca Sintética",
    "model": "Modelo Sintético",
    "color": "Azul"
  },
  "services": [
    { "description": "Inspeção sintética", "price": 150.00 }
  ],
  "inventoryItems": []
}
```

Na primeira execução retorna `201` e IDs de ordem, cliente, veículo, orçamento,
serviços e peças. Replay byte a byte com o mesmo `requestId` retorna `200` e os
mesmos IDs. Reutilizar o `requestId` com payload diferente retorna `409`. CPFs,
placas, e-mails e descrições devem ser únicos entre execuções; a
[coleção Postman](docs/postman/GarageFlow.Phase2.postman_collection.json) gera
dados válidos e prova os três resultados.

### Status, fila ativa e histórico

- `GET /work-orders/{id}/status` retorna somente `id`, `status` e `updatedAt`.
- `GET /work-orders?page=1&pageSize=20&customerId={id}` é a fila de staff:
  inclui apenas estados ativos e ordena por `InProgress`, `WaitingApproval`,
  `Diagnosing`, `Received`; empates usam `createdAt` e ID.
- `Completed`, `Delivered` e `Cancelled` ficam fora da fila ativa.
- `GET /me/work-orders?page=1&pageSize=20` é o histórico do cliente autenticado
  e mantém também as ordens terminais pertencentes a ele.

### Webhook, idempotência e notificação

`POST /webhooks/estimate-decisions` é anônimo no transporte, mas exige:

- `X-GarageFlow-Timestamp` em segundos Unix;
- `X-GarageFlow-Signature` em hexadecimal minúsculo;
- HMAC-SHA256 de bytes exatos `<timestamp>.<raw-body>`.

O corpo contém `eventId`, `workOrderId`, `estimateId`, `decision` e
`occurredAt`. Assinatura inválida/expirada retorna `401`; evento novo válido
retorna `204`; replay do mesmo `eventId` com os mesmos bytes não repete o efeito
e retorna `204`; o mesmo ID com hash diferente retorna `409`.

Na mesma transação do comando, mudanças persistem o agregado e mensagens de
outbox; no webhook novo, a inbox também é persistida. Um worker adquire lease,
publica a mudança no SNS e marca sucesso por update condicional. Falhas usam
backoff e retry. A entrega é eventual e pelo menos uma vez: consumidores devem
tolerar possível duplicata. No Compose local o outbox está desabilitado por
padrão, portanto nenhuma conta AWS é necessária.

## Execução local com Docker Compose

Pré-requisitos: Docker Desktop/Engine com Compose e portas locais configuráveis.
Crie um `.env` ignorado para sobrescrever os defaults de desenvolvimento; não
commite senha, chave JWT ou segredo HMAC.

```bash
docker compose config --quiet
docker compose up -d --build
docker compose ps
curl --fail http://localhost:8080/health/ready
```

### URLs locais

| Recurso | URL |
| --- | --- |
| API | http://localhost:8080 |
| Health simples | http://localhost:8080/health |
| Liveness | http://localhost:8080/health/live |
| Readiness com banco | http://localhost:8080/health/ready |
| Scalar | http://localhost:8080/scalar/#description/introduction |
| OpenAPI JSON | http://localhost:8080/openapi/v1.json |
| pgAdmin | http://localhost:5050/browser/ |

### Bootstrap e troca obrigatória de senha

Defina `BOOTSTRAP_ADMIN_EMAIL` e `BOOTSTRAP_ADMIN_PASSWORD` apenas no `.env`
local. Faça `POST /auth/login`; o primeiro resultado deve indicar
`mustChangePassword=true`. Use o token temporário em `PUT /users/me/password`,
faça novo login com a senha ativa e só então acesse rotas protegidas. Não copie
senha nem JWT para README, coleção, screenshots ou logs.

Overrides locais relevantes:

```text
API_PORT, POSTGRES_PORT, PGADMIN_PORT
POSTGRES_DB, POSTGRES_USER, POSTGRES_PASSWORD
DATABASE_AUTO_MIGRATE, DATABASE_AUTO_SEED, DATABASE_SEED_SCRIPT_PATH
AUTH_JWT_KEY, BOOTSTRAP_ADMIN_EMAIL, BOOTSTRAP_ADMIN_PASSWORD
ESTIMATE_DECISION_WEBHOOK_SECRET
```

O Compose usa migrations/seed automáticos apenas para desenvolvimento. O modo
one-shot usado por contêiner/Kubernetes é:

```bash
docker compose run --rm api --migrate-only
```

Em runtime Kubernetes, `Database__AutoMigrate=false`; o Job com
`args: --migrate-only` precisa chegar a `Complete` antes do rollout.

Para encerrar:

```bash
docker compose logs --no-log-prefix api
docker compose down
# Somente quando quiser apagar os dados locais:
docker compose down -v
```

## Verificação local e CI

Com Docker ativo:

```bash
dotnet build GarageFlow.slnx
dotnet test Tests/Unit/GarageFlow.Tests.Unit.csproj
dotnet test Tests/Integration/GarageFlow.Tests.Integration.csproj
dotnet test Tests/E2E/GarageFlow.Tests.E2E.csproj
dotnet test GarageFlow.slnx
```

A Quality Gate exige build sem warnings, testes, E2E PostgreSQL, cobertura total
de pelo menos 80%, Sonar quando configurado, Terraform, contêiner, Compose e
manifests Kubernetes.

Smoke independente da imagem:

```bash
docker build -t garageflow:phase2 .
bash scripts/smoke-container.sh garageflow:phase2
```

Postman/Newman usa segredos somente no ambiente do processo:

```powershell
npx --yes newman@6.2.1 run docs/postman/GarageFlow.Phase2.postman_collection.json `
  --environment docs/postman/GarageFlow.Phase2.postman_environment.example.json `
  --env-var "staffEmail=$env:GARAGEFLOW_STAFF_EMAIL" `
  --env-var "staffPassword=$env:GARAGEFLOW_STAFF_PASSWORD" `
  --env-var "staffActivePassword=$env:GARAGEFLOW_STAFF_ACTIVE_PASSWORD" `
  --env-var "webhookSecret=$env:GARAGEFLOW_WEBHOOK_SECRET"
```

Outras validações reproduzíveis:

```powershell
./scripts/render-phase2-diagrams.ps1
./scripts/validate-doc-links.ps1
```

## AWS Academy

### Princípios e custo

O ambiente efêmero usa `us-east-1`, VPC em duas AZs, duas subnets públicas para
o EKS managed node group, DB subnet group privado, RDS PostgreSQL Single-AZ,
ECR imutável por SHA, SNS/e-mail e Secrets Manager. Não há NAT Gateway. O
Service público é HTTP sem TLS: use somente dados sintéticos.

EKS, dois workers e RDS geram cobrança enquanto existem. Atualize as credenciais
temporárias do Learner Lab antes de cada workflow, faça deploy, demonstração,
captura e destroy na mesma sessão e nunca afirme custo zero; a verificação prova
ausência dos recursos do GarageFlow, não a fatura inteira da conta.

### Bootstrap retido do estado Terraform

Uma única vez, com credenciais temporárias exportadas e valores próprios:

```bash
aws sts get-caller-identity
terraform -chdir=infra/bootstrap/state-backend init
terraform -chdir=infra/bootstrap/state-backend apply \
  -var="state_bucket_name=${TF_STATE_BUCKET}" \
  -var="owner=${TF_OWNER}" \
  -var="expires_on=${TF_EXPIRES_ON}"
cp infra/bootstrap/state-backend/backend.tf.example infra/bootstrap/state-backend/backend.tf
terraform -chdir=infra/bootstrap/state-backend init -migrate-state \
  -backend-config="bucket=${TF_STATE_BUCKET}" \
  -backend-config="key=bootstrap/state-backend.tfstate" \
  -backend-config="region=us-east-1" \
  -backend-config="encrypt=true" \
  -backend-config="use_lockfile=true"
```

`backend.tf`, `terraform.tfvars`, `.terraform`, state e plans são ignorados. O
bucket é privado, criptografado, versionado e tem `prevent_destroy`; ele não é
removido pelo destroy normal do ambiente.

### Environment protegido e deploy

Configure o GitHub Environment `aws-academy` com exatamente:

- secrets: `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`, `AWS_SESSION_TOKEN`,
  `TF_STATE_BUCKET`, `EKS_CLUSTER_ROLE_ARN`, `EKS_NODE_ROLE_ARN`;
- variables: `TF_OWNER`, `TF_EXPIRES_ON`, `SNS_NOTIFICATION_EMAIL`,
  `BOOTSTRAP_ADMIN_EMAIL`.

Dispare [Deploy AWS Academy](.github/workflows/deploy-aws-academy.yml) com
confirmação exata `APPLY`. O workflow valida identidade/capacidade, aplica
Terraform remoto, publica/reutiliza `${GITHUB_SHA}` no ECR, instala Metrics
Server verificado, materializa o Secret sem gravá-lo, executa o migration Job,
aguarda duas réplicas e LoadBalancer, exige confirmação do e-mail SNS e roda o
smoke. Confirme o link recebido pelo destinatário antes do timeout.

Evidência segura enquanto o ambiente existe:

```bash
kubectl -n garageflow get deployment,pods,service,hpa
kubectl -n garageflow top pods
aws eks describe-cluster --region us-east-1 --name garageflow-academy --query 'cluster.status'
aws rds describe-db-instances --region us-east-1 --db-instance-identifier garageflow-academy --query 'DBInstances[0].PubliclyAccessible'
aws ecr describe-images --region us-east-1 --repository-name garageflow --query 'imageDetails[].imageTags'
```

Não capture `kubectl get secret`, environment do processo, Terraform state ou
saídas do Secrets Manager.

### HPA e demonstração

Siga o [runbook de carga limitada](docs/delivery/demo-runbook.md). Ele exige duas
réplicas iniciais, Metrics Server saudável, escala entre 2 e 6, thresholds do k6
e retorno a 2. O `k6 inspect` local não substitui a prova live. A evidência live
continua pendente até uma execução real registrada.

### Destroy protegido e retenção

Depois de capturar a evidência live, dispare
[Destroy AWS Academy](.github/workflows/destroy-aws-academy.yml) com confirmação
exata `DESTROY` e credenciais renovadas. O workflow primeiro remove Service e
namespace, aguarda o LoadBalancer desaparecer, executa `terraform destroy` e
usa [`verify-destroy.sh`](scripts/aws/verify-destroy.sh) para procurar EKS,
workers, RDS, ECR, SNS, secrets, LoadBalancer e VPC. O bucket versionado permanece.

Após a avaliação e somente com autorização explícita, liste todas as versões e
delete markers do bucket, remova cada versão, confirme que ele está vazio e só
então exclua o bucket retido. Essa limpeza final é irreversível e não faz parte
do workflow normal; confira o nome exato de `TF_STATE_BUCKET` antes de qualquer
mutação.

## Troubleshooting

| Sintoma | Diagnóstico/recuperação |
| --- | --- |
| Credenciais expiradas ou STS falha | Renove as três credenciais temporárias no Environment e execute novamente. |
| Backend/state ilegível | Não faça teardown manual; confira bucket, região, key e `use_lockfile`, depois restaure o acesso. |
| SNS `PendingConfirmation` | Abra o e-mail correto, confirme a assinatura e execute novamente o deploy. |
| Migration Job falha | Consulte `kubectl -n garageflow logs job/garageflow-migration`; corrija antes do Deployment. |
| Pods não ficam Ready | Verifique eventos, probes, ConfigMap e referência ao Secret sem imprimir valores. |
| `kubectl top` indisponível | Corrija o Metrics Server; não execute a demonstração HPA sem métricas. |
| LoadBalancer sem ingress | Aguarde o timeout, inspecione eventos do Service e capacidade/permissões Academy. |
| HPA não escala | Confirme requests de CPU/memória, métricas, alvo e uma ordem sintética ativa. |
| Destroy bloqueado pela rede | Garanta que Service/namespace e o LoadBalancer Kubernetes foram removidos primeiro. |
| Verificador encontra recurso | Não declare cleanup; corrija permissão/estado e repita o workflow protegido. |

## Segurança e artefatos

- JWT, HMAC, senhas, chaves AWS, connection strings, state, plans e exports de
  ambiente nunca entram no Git, PDF, vídeo, screenshots ou logs.
- CPF `52998224725`, telefone `+5511999999999` e demais exemplos deste guia são
  sintéticos; gere identidades únicas para novas execuções.
- Relatórios históricos: [ZAP/Checkmarx](<docs/security/vulnerability-scan/ZAP by Checkmarx Scanning Report.pdf>) e [SonarQube](<docs/security/vulnerability-scan/Overview - GarageFlow in Diego Ruguê SonarQube Cloud.pdf>).

## PDF final pendente

O gerador da Task 6 produzirá o arquivo
`docs/entrega-final/Tech-Challenge-Fase-2-GarageFlow.pdf` somente depois de
evidência real validada, vídeo acessível anonimamente e destroy verificado. O
arquivo ainda não existe e, por isso, não há link fictício. O status binário
permanece em [checklist final](docs/delivery/final-submission-checklist.md).
