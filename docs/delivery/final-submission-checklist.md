# Checklist final — GarageFlow Fase 2

Marque exatamente uma opção por linha somente depois de verificar a evidência.
Itens humanos/live começam pendentes; não transforme “pendente” em sucesso sem
URL, arquivo ou saída verificável e sem dados sensíveis.

| Item obrigatório | Sim | Não | Estado atual | Local da evidência |
| --- | :---: | :---: | --- | --- |
| Repositório acessível ao avaliador com permissão de leitura | [ ] | [ ] | Pendente — ação humana autorizada | URL/estado do convite |
| Commit final identificado por SHA completo | [ ] | [ ] | Pendente | Evidência final JSON/PDF |
| Quality Gate e testes obrigatórios verdes | [ ] | [ ] | Pendente — execução final | URL do workflow |
| Deploy protegido AWS Academy concluído | [ ] | [ ] | Pendente — live | URL do workflow deploy |
| `POST /work-orders` demonstrado com IDs existentes | [ ] | [ ] | Pendente — live | vídeo/Postman/evidência |
| `POST /work-orders/intake` demonstrado com `201`, replay `200` e conflito `409` | [ ] | [ ] | Pendente — live | vídeo/Postman/evidência |
| `GET /work-orders/{id}/status` demonstrado | [ ] | [ ] | Pendente — live | vídeo/Postman/evidência |
| Fila ativa priorizada e terminal excluído | [ ] | [ ] | Pendente — live | vídeo/Postman/evidência |
| Histórico do cliente mantém ordem terminal | [ ] | [ ] | Pendente — live | vídeo/Postman/evidência |
| Webhook HMAC válido e replay idempotente demonstrados | [ ] | [ ] | Pendente — live | vídeo/Postman/evidência |
| E-mail SNS confirmado e recebido | [ ] | [ ] | Pendente — ação humana/live | captura não sensível |
| Duas réplicas prontas antes da carga | [ ] | [ ] | Pendente — live | `kubectl`/vídeo |
| HPA escala acima de 2 e nunca além de 6 | [ ] | [ ] | Pendente — live | `kubectl`/vídeo |
| HPA retorna a 2 depois da carga | [ ] | [ ] | Pendente — live | `kubectl`/vídeo |
| RDS privado e sem endpoint público | [ ] | [ ] | Pendente — live | AWS CLI/Terraform output seguro |
| Imagem ECR imutável identificada pelo commit SHA | [ ] | [ ] | Pendente — live | AWS CLI/workflow |
| Service LoadBalancer removido antes do Terraform destroy | [ ] | [ ] | Pendente — live | workflow destroy |
| EKS, EC2, RDS, ECR, SNS, VPC e secrets ausentes após destroy | [ ] | [ ] | Pendente — live | `verify-destroy.sh`/workflow |
| Bucket de backend retido, privado e versionado | [ ] | [ ] | Pendente — live | AWS CLI/workflow |
| Vídeo público/não listado, anônimo e com até 15 minutos | [ ] | [ ] | Pendente — ação humana | URL verificada sem login |
| PDF final possui repositório, vídeo, arquitetura e cleanup | [ ] | [ ] | Pendente — Task 6 | PDF final |
| Todas as páginas do PDF foram renderizadas e revisadas | [ ] | [ ] | Pendente — Task 6 | `rendered-phase2/` |
| Links do README, PDF, vídeo e coleção abrem anonimamente | [ ] | [ ] | Pendente — verificação final | checklist/saída do validator |
| Nenhum segredo, state, plan, token ou environment export foi commitado | [ ] | [ ] | Pendente — auditoria final | `git status`/scan |

## Fechamento

- [ ] `scripts/validate-doc-links.ps1` passou no commit final.
- [ ] Todos os comandos obrigatórios de `AGENTS.md` passaram com Docker ativo.
- [ ] Evidência live foi capturada antes do destroy.
- [ ] Destroy protegido terminou e a ausência dos recursos faturáveis foi verificada.
- [ ] O bucket retido foi mantido para avaliação ou removido explicitamente após autorização final.
