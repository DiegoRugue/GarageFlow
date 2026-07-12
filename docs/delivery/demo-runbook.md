# Demonstração de carga limitada e HPA

Este runbook demonstra o `HorizontalPodAutoscaler` do GarageFlow com uma ordem
de serviço sintética e ativa. O script gera somente consultas autenticadas e
limitadas; não cria nem altera dados.

> Status desta entrega: o `k6 inspect` local é reproduzível e deve passar. A
> evidência de escala real permanece **pendente** até existir um deploy AWS
> Academy ativo. Não considere a inspeção estática como prova de HPA.

## Segurança, custo e pré-requisitos

- Use exclusivamente dados sintéticos. O endpoint público da demonstração é
  HTTP sem TLS; não envie CPF, telefone, e-mail ou senha reais.
- O workflow `Deploy AWS Academy` deve ter terminado com sucesso, com duas
  réplicas prontas, LoadBalancer disponível e assinatura SNS confirmada.
- Confirme `kubectl version --client`, `kubectl cluster-info` e `k6 version`.
- Atualize as credenciais temporárias do Learner Lab antes da sessão. Não cole
  chaves, senha, JWT ou connection string em comandos, histórico, screenshots
  ou logs.
- EKS, dois nós EC2 e RDS geram custo enquanto ativos. Faça a captura sem
  interrupções e execute o workflow protegido de destroy no mesmo dia.
- Reserve uma ordem sintética cujo status permaneça em `Received`,
  `Diagnosing`, `WaitingApproval` ou `InProgress` durante toda a carga.

Valide o estado inicial:

```bash
kubectl -n garageflow get deployment,pods,hpa
kubectl -n garageflow top pods
kubectl -n kube-system get deployment metrics-server
kubectl get --raw /apis/metrics.k8s.io/v1beta1/nodes >/dev/null
```

Só prossiga quando `garageflow-api` tiver exatamente duas réplicas prontas, o
HPA indicar mínimo 2/máximo 6 e o Metrics Server responder. Se `kubectl top`
falhar ou mostrar métricas desconhecidas, diagnostique o Metrics Server antes
de gerar carga.

## Inspeção estática local

Este comando valida sintaxe e opções, mas não acessa a API e não prova escala:

```powershell
docker run --rm -v "${PWD}:/work" -w /work `
  -e BASE_URL=http://127.0.0.1:8080 `
  -e STAFF_EMAIL=inspect@garageflow.invalid `
  -e STAFF_PASSWORD=inspect-only-not-a-credential `
  -e WORK_ORDER_ID=00000000-0000-4000-8000-000000000001 `
  grafana/k6:1.7.1 inspect --include-system-env-vars scripts/load-test/work-orders.js
```

O resultado deve mostrar os quatro estágios limitados e os thresholds
`http_req_failed < 2%` e `p(95) < 1000ms`.
Os valores acima existem somente para a validação de init e não são usados em
requisições pelo subcomando `inspect`.

## Demonstração ao vivo em dois terminais

### Terminal A — estado, métricas e watch

Capture o estado inicial e mantenha o watch aberto:

```bash
kubectl -n garageflow get deployment,pods,hpa
kubectl -n garageflow top pods
kubectl -n garageflow get hpa,pods -w
```

Durante a carga, registre a tela quando CPU ou memória cruzar o alvo do HPA
(CPU 60% ou memória 70%) e quando `DESIRED`/`CURRENT` subir acima de 2. O valor
observado nunca pode passar de 6. Após capturar a escala, interrompa apenas o
watch com `Ctrl+C`, execute `kubectl -n garageflow top pods` e reinicie o watch
para acompanhar a recuperação.

### Terminal B — ambiente local e carga

Leia os valores sem colocar a senha na linha de comando ou no histórico:

```bash
read -r -p 'BASE_URL HTTP do LoadBalancer: ' BASE_URL
read -r -p 'E-mail sintético do staff: ' STAFF_EMAIL
read -r -s -p 'Senha ativa do staff: ' STAFF_PASSWORD; printf '\n'
read -r -p 'WORK_ORDER_ID sintético e ativo: ' WORK_ORDER_ID
export BASE_URL STAFF_EMAIL STAFF_PASSWORD WORK_ORDER_ID
k6 run scripts/load-test/work-orders.js
kubectl -n garageflow top pods
kubectl -n garageflow get deployment,pods,hpa
unset BASE_URL STAFF_EMAIL STAFF_PASSWORD WORK_ORDER_ID
```

O script autentica uma vez por VU, consulta
`GET /work-orders?page=1&pageSize=20` e
`GET /work-orders/{id}/status`, e dura 210 segundos. Os thresholds devem passar.
Não redirecione a saída para um log que possa capturar o ambiente do processo.

## Evidência obrigatória

Registre, sem qualquer segredo:

- commit SHA, URL da execução de deploy e horário UTC da demonstração;
- snapshot inicial com duas réplicas prontas e HPA 2–6;
- `kubectl top pods` com métricas disponíveis antes da carga;
- CPU ou memória cruzando o alvo configurado;
- `DESIRED` e `CURRENT` subindo acima de 2 e nunca acima de 6;
- término do k6 com `http_req_failed < 2%` e `p(95) < 1000ms`;
- carga encerrada e HPA retornando gradualmente a duas réplicas.

O HPA usa estabilização de scale-down de 60 segundos e reduz no máximo um pod a
cada 30 segundos. Portanto, o retorno completo pode levar mais de 60 segundos,
dependendo do pico. Mantenha o watch até `DESIRED=2`, `CURRENT=2` e duas réplicas
prontas; não edite a evidência para ocultar o período de recuperação.

## Abort, recuperação e limpeza

Para abortar a carga, use `Ctrl+C` no Terminal B. Se o processo não encerrar:

```bash
pkill -INT -x k6 || true
kubectl -n garageflow get hpa,pods -w
```

Não delete o HPA nem force a escala do Deployment durante a evidência. Depois de
parar a carga, o próprio HPA deve retornar a duas réplicas. Se houver respostas
`401`, valide a conta sintética e `mustChangePassword=false`; se houver `404`,
substitua `WORK_ORDER_ID` por uma ordem ativa; se houver falhas de schema/status,
pare a carga e confirme que a ordem não chegou a um estado terminal.

Após capturar a recuperação e as evidências live, finalize os valores do
ambiente com `unset`, feche os watches e dispare o workflow protegido
`Destroy AWS Academy` com a confirmação exata `DESTROY`. Aguarde a verificação
de ausência dos recursos faturáveis; o bucket S3 de bootstrap permanece retido
para limpeza final explícita conforme o runbook de infraestrutura.
