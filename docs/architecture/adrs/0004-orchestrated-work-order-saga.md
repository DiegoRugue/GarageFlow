# ADR 0004 — Orquestração da Saga de ordem de serviço

**Status** · Implementada em código. Homologação distribuída pendente.

## Contexto

A oficina precisa coordenar aprovação, reparo, cobrança e entrega. A entrega depende da conclusão técnica e da confirmação financeira da mesma versão de orçamento. Execução e Pagamentos podem falhar ou ficar temporariamente indisponíveis após a API registrar a intenção de avançar.

## Decisão

A API de Ordens de Serviço orquestra a Saga. A OS concentra o estado da jornada e valida as transições. Execução é a fonte da decisão de agenda e dos horários do reparo. Pagamentos é a fonte do checkout e da confirmação financeira. Cada serviço persiste os fatos de sua responsabilidade no próprio banco.

A aprovação e a conclusão usam a outbox da API para registrar comandos na transação da OS. Execução registra o resultado no DynamoDB antes de publicar. Pagamentos grava a alteração financeira e os eventos de saída na mesma transação do PostgreSQL. Os consumidores confirmam as mensagens após a operação local correspondente.

| Identidade | Uso |
| --- | --- |
| `correlationId` | Identificador da OS ao longo da Saga. |
| `estimateId` | Versão de orçamento que autoriza os serviços e fixa o valor. |
| `executionId` | Agendamento e ciclo do reparo. |
| `chargeId` | Cobrança derivada do `executionId`. Vincula a ordem de pagamento à versão aprovada. |
| `messageId` | Identidade da mensagem. Permanece estável ao republicar o mesmo resultado registrado. |
| Revisão de pagamento | Projeção da versão financeira mais recente na OS. |

O agendamento recusado é compensado pela devolução das reservas ativas. Falhas transitórias seguem retry. Pagamento pendente ou revogado bloqueia a entrega. A API conserva o histórico de uma entrega já realizada.

## Alternativas consideradas

| Abordagem | Consequência neste fluxo |
| --- | --- |
| Orquestração na API de OS | Centraliza a visão da jornada e a invariante de entrega junto ao agregado. A API conhece os contratos de Execução e Pagamentos. |
| Coreografia entre os três serviços | Exige reconstruir a jornada a partir de múltiplos consumidores. O ponto que decide a liberação do veículo fica menos explícito. |
| Orquestrador separado | Introduz um serviço e estado adicionais para coordenar uma regra que já pertence à OS. |

## Consequências

A confirmação entre serviços é assíncrona. O status retorna a projeção disponível na API. Comandos e resultados podem ser reentregues. As transições da OS, as gravações condicionais de Execução e a revisão de Pagamentos sustentam a repetição segura.

O orçamento continua na API da oficina. A extração cobre a execução do reparo e a cobrança. A emissão de uma nova order após expiração ou cancelamento fica para outra evolução.

## Referências de implementação

- [Arquitetura, fluxo e aceite da Fase 4](../../../README.md#fase-4--arquitetura-e-saga-da-os)
- [Agregado WorkOrder](../../../Domain/WorkOrders/Entities/WorkOrder.cs)
- [Consumidor de resultados](../../../Adapters.Infrastructure/WorkOrders/Execution/SqsWorkOrderResultConsumer.cs)
- [Cenários de integração da Saga](../../../Tests/Integration/WorkOrders/ExecutionSchedulingSagaTests.cs)
