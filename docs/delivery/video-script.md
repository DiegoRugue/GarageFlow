# Roteiro do vídeo — GarageFlow Fase 2

> **Estado: PENDENTE.** Este arquivo é somente o roteiro reproduzível. Nenhuma
> captura live, publicação, URL, evidência de sucesso ou acesso do avaliador é
> afirmado aqui. Grave apenas depois de o ciclo real de deploy, demonstração e
> destroy ter sido ensaiado com sucesso.

A duração final planejada é **exatamente 14:30**, distribuída nos oito
segmentos abaixo. Use somente dados sintéticos e preserve no vídeo os horários
reais das observações. Quando houver aceleração ou corte, mostre uma legenda
explícita e não esconda falhas nem intervalos relevantes.

## Preparação e higiene de tela

Antes de iniciar a gravação:

- desative notificações, prévias de mensagens, preenchimento automático e
  extensões que possam exibir dados pessoais;
- use um perfil de navegador exclusivo para a demonstração e feche e-mail,
  chats, gerenciadores de senha, consoles de nuvem e abas não utilizadas;
- ajuste o terminal para um prompt neutro, sem nome de usuário, hostname,
  conta AWS ou caminho pessoal;
- prepare em uma janela fora da captura as credenciais temporárias, o ambiente
  local do Postman e as variáveis do k6; nunca digite nem revele esses valores
  durante a gravação;
- mantenha o Postman Console fechado, os valores de ambiente mascarados e as
  abas de autenticação e headers sensíveis recolhidas;
- confirme que todos os CPFs, telefones, e-mails, UUIDs, veículos e ordens de
  serviço usados na sessão são sintéticos;
- faça `collect-phase2-evidence.ps1 -CaptureLive` depois da recuperação do HPA
  e **antes** do destroy, em uma janela que não esteja sendo gravada;
- prepare os comandos filtrados deste roteiro previamente, sem histórico com
  valores sensíveis e sem habilitar rastreamento de shell.

Não mostre nem execute na tela `env`, `printenv`, `set`, `Get-ChildItem Env:`,
`kubectl get secret`, `aws secretsmanager get-secret-value`, `terraform show`,
`terraform state pull`, `terraform plan`, `gh run view --log`, arquivos `.env`,
tokens JWT, valores HMAC, chaves AWS, senhas ou connection strings. Erros que
possam conter valores de entrada devem ser investigados fora da gravação.

## 00:00–01:30 — Objetivo e Clean Architecture

**Tela:** título do projeto, índice do README e diagrama da aplicação já
renderizado.

**Narração e demonstração:**

1. Apresente o GarageFlow como uma API de gestão de oficina e explique o
   objetivo da Fase 2: operação reproduzível, implantação protegida,
   escalabilidade e evidências verificáveis.
2. Percorra do exterior para o núcleo: `Host` como única composition root,
   adapters de API e infraestrutura, `Application`, `Domain` e `SharedKernel`.
3. Destaque que as dependências apontam para dentro e que regras de negócio e
   invariantes permanecem fora de HTTP, EF Core e integrações externas.
4. Avise que nenhum resultado live será considerado concluído sem evidência do
   deploy, da carga, do destroy e das verificações finais.

## 01:30–03:00 — AWS, Kubernetes, Terraform e deploy protegido

**Tela:** diagramas AWS Academy, Kubernetes e fluxo de deployment; em seguida,
o resumo do workflow de deploy na interface do GitHub, sem abrir logs brutos.

**Narração e demonstração:**

1. Mostre as duas AZs, subnets públicas para EKS/LoadBalancer, subnets privadas
   para RDS, ECR, SNS, Secrets Manager e bucket S3 de estado retido; ressalte a
   ausência de NAT por restrição de custo da Academy.
2. Explique a sequência protegida: confirmação `APPLY`, preflight de identidade
   e capacidade, Terraform, imagem imutável, Metrics Server, configuração,
   migration Job, rollout de duas réplicas, HPA e smoke test.
3. Mostre no diagrama que o HPA opera entre duas e seis réplicas e que o
   Service é removido antes do Terraform destroy.
4. Exiba apenas o resumo de execução bem-sucedida do workflow real. Se ainda
   não houver uma execução live válida, interrompa a gravação: o item continua
   **PENDENTE**.

## 03:00–05:30 — EKS, RDS privado, ECR e duas réplicas prontas

**Tela:** terminal Bash com os comandos filtrados abaixo. As variáveis
necessárias já devem estar configuradas fora da captura.

```bash
git rev-parse HEAD

aws eks describe-cluster \
  --region us-east-1 \
  --name garageflow-academy \
  --query 'cluster.{Name:name,Status:status,Version:version}' \
  --output table

aws rds describe-db-instances \
  --region us-east-1 \
  --db-instance-identifier garageflow-academy \
  --query 'DBInstances[0].{Identifier:DBInstanceIdentifier,Status:DBInstanceStatus,Engine:Engine,PubliclyAccessible:PubliclyAccessible}' \
  --output table

commit_sha="$(git rev-parse HEAD)"
aws ecr describe-images \
  --region us-east-1 \
  --repository-name garageflow \
  --image-ids "imageTag=${commit_sha}" \
  --query 'imageDetails[0].{Digest:imageDigest,Tags:imageTags,PushedAt:imagePushedAt}' \
  --output table
unset commit_sha

kubectl -n garageflow get deployment garageflow-api \
  -o custom-columns='NAME:.metadata.name,DESIRED:.spec.replicas,READY:.status.readyReplicas,AVAILABLE:.status.availableReplicas'

kubectl -n garageflow get pods \
  -l app.kubernetes.io/name=garageflow-api \
  -o custom-columns='NAME:.metadata.name,PHASE:.status.phase,READY:.status.containerStatuses[0].ready,RESTARTS:.status.containerStatuses[0].restartCount'

kubectl -n garageflow get service garageflow-api \
  -o custom-columns='NAME:.metadata.name,TYPE:.spec.type,INGRESS:.status.loadBalancer.ingress[0].hostname'

kubectl -n garageflow get hpa garageflow-api \
  -o custom-columns='NAME:.metadata.name,MIN:.spec.minReplicas,MAX:.spec.maxReplicas,CURRENT:.status.currentReplicas,DESIRED:.status.desiredReplicas'
```

**Narração e demonstração:** confirme o EKS ativo, o RDS com
`PubliclyAccessible=false`, a tag ECR igual ao commit completo, o digest da
imagem, pelo menos duas réplicas prontas, o LoadBalancer e o HPA 2–6. Não mostre
endpoint do RDS, ARN, conta AWS, configuração do cluster ou conteúdo de Secret.

## 05:30–08:00 — APIs de abertura, status e fila ativa

**Tela:** Postman Runner com a coleção versionada e ambiente local já
configurado. Não abra a aba de variáveis, o Console, o token bearer nem headers
gerados.

**Narração e demonstração:**

1. Execute `POST /work-orders` com IDs sintéticos já existentes e mostre o
   `201`, o identificador da ordem e o status `Received`.
2. Execute `POST /work-orders/intake` com cadastro aninhado totalmente novo e
   mostre `201` e o header `Location`; repita os mesmos bytes e o mesmo
   `requestId` para obter `200` com os mesmos IDs; altere o preço mantendo o
   `requestId` para obter `409`.
3. Consulte `GET /work-orders/{id}/status` e mostre o payload `200`.
4. Consulte `GET /work-orders?page=1&pageSize=100`; mostre a ordem ativa e a
   prioridade da fila sem exibir dados reais. Use uma ordem sintética terminal
   previamente preparada para evidenciar que `Completed` e `Delivered` não
   aparecem na fila ativa.
5. Relacione os estados ao vocabulário da entrega: `Received=Recebida`,
   `Diagnosing=Diagnóstico`, `WaitingApproval=Aguardando Aprovação`,
   `InProgress=Execução`, `Completed=Finalizada` e `Delivered=Entregue`.

Se autenticação, payload, status ou idempotência divergirem, pare a gravação e
corrija o ambiente fora da tela; não edite a falha para simular sucesso.

## 08:00–10:00 — Webhook assinado, replay idempotente e SNS

**Tela:** requests de decisão externa no Postman com headers recolhidos; depois,
uma janela de e-mail preparada para mostrar somente a mensagem sintética da
notificação, sem destinatário, caixa lateral, outras mensagens ou links de
confirmação.

**Narração e demonstração:**

1. Explique que a assinatura é HMAC-SHA256 minúscula sobre os bytes exatos de
   `timestamp + "." + raw-body`, sem mostrar timestamp, raw body persistido,
   assinatura ou chave.
2. Execute `POST /webhooks/estimate-decisions` para aprovação e mostre apenas o
   resultado `204` dos testes do Postman.
3. Reenvie o mesmo event ID, timestamp e raw body byte a byte; mostre o segundo
   `204` e explique que o replay não duplica a transição.
4. Mostre a notificação SNS recebida, com assunto e conteúdo exclusivamente
   sintéticos. Explique que outbox/SNS é eventual e pode entregar mais de uma
   vez, portanto consumidores também precisam ser idempotentes.

Não abra o Postman Console, headers calculados, configurações do tópico,
subscription ARN ou dados da conta de e-mail.

## 10:00–12:30 — k6, scale-out e recuperação do HPA

**Tela:** dois terminais. O Terminal A mantém o watch; o Terminal B executa a
carga com suas entradas já injetadas fora da gravação.

**Terminal A:**

```bash
kubectl -n garageflow get deployment,pods,hpa
kubectl -n garageflow top pods
kubectl -n garageflow get hpa,pods -w
```

**Terminal B:**

```bash
k6 run scripts/load-test/work-orders.js
kubectl -n garageflow top pods
kubectl -n garageflow get deployment,pods,hpa
```

**Narração e demonstração:** registre a linha de base com duas réplicas, CPU ou
memória cruzando o alvo, `CURRENT`/`DESIRED` acima de dois e nunca acima de seis,
o encerramento do k6 com `http_req_failed < 2%` e `p(95) < 1000ms`, e a volta a
duas réplicas prontas após o scale-down.

A execução real do k6 e a recuperação podem exceder os 2:30 deste segmento.
Use aceleração ou cortes identificados por legenda, mantendo timestamps
visíveis na sequência de evidências. Não altere réplicas manualmente, não
delete o HPA e não omita o intervalo de recuperação. Se o HPA não escalar ou
não retornar a dois, o resultado permanece **PENDENTE**.

## 12:30–14:00 — Destroy protegido e verificação de ausência

**Tela:** resumo do workflow protegido `Destroy AWS Academy`, com a confirmação
`DESTROY`, seguido apenas pelo resultado filtrado da etapa de verificação. Não
abra os logs completos do workflow.

Quando for necessária uma conferência local, use somente:

```bash
bash scripts/aws/verify-destroy.sh "$TF_STATE_BUCKET"
```

O nome do bucket deve estar preparado fora da captura e o script não deve
imprimi-lo.

**Narração e demonstração:** mostre que o Service/LoadBalancer foi removido
antes do Terraform destroy e que EKS, node group, workers EC2, RDS, ECR, SNS,
Secrets Manager, LoadBalancer e VPC ficaram ausentes. Confirme somente que o
bucket de backend permanece acessível, privado e versionado; não revele seu
nome, objetos ou estado Terraform.

Se a verificação encontrar recurso faturável ou erro de permissão/API, não
declare cleanup: mantenha o item **PENDENTE** e faça a investigação fora da
gravação.

## 14:00–14:30 — Repositório, PDF e acesso do avaliador

**Tela:** página principal do repositório, caminho documentado do PDF e estado
de acesso do avaliador, sem abas administrativas ou dados de outros usuários.

**Narração e demonstração:**

1. Recapitule onde estão README, coleção Postman, diagramas, evidência e o
   documento final. Não afirme que o PDF está concluído antes da geração e da
   revisão visual da Task 6.
2. Após autorização explícita e concessão fora da gravação, mostre apenas uma
   das consultas read-only abaixo, conforme o estado real:

```bash
gh api "repos/${REPOSITORY}/collaborators/soat-architecture/permission" \
  --jq '.user.login + ":" + .permission'

gh api "repos/${REPOSITORY}/invitations" \
  --jq '.[] | select(.invitee.login == "soat-architecture") | .invitee.login + ":pending"'
```

3. Aceite somente `soat-architecture:pull` ou
   `soat-architecture:pending`. Não mostre nem execute a chamada que concede
   acesso durante a gravação.
4. Encerre informando que publicação, reprodução anônima, duração final,
   evidência final e PDF continuam **PENDENTES** até as verificações posteriores
   ao upload.

## Gates após a gravação

Fora da captura:

1. publique o vídeo como público ou não listado;
2. abra-o em navegador anônimo, sem sessão do criador, e verifique reprodução
   e duração máxima de 15:00;
3. somente então finalize a evidência com os valores reais verificados;
4. gere o DOCX/PDF, renderize e revise visualmente todas as páginas;
5. atualize os links reais e execute o checklist e as validações finais.

Até que todos esses gates tenham evidência verificável, o vídeo e a entrega
permanecem **PENDENTES**.
