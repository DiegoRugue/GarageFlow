# Linguagem Ubíqua - GarageFlow

Este glossário registra os principais termos usados no domínio da oficina e no código do GarageFlow. A intenção é manter a conversa entre negócio, documentação e implementação usando os mesmos nomes.

| Termo | Significado no domínio | Representação no código |
| --- | --- | --- |
| Cliente | Pessoa física ou jurídica que solicita atendimento da oficina. | `Customer` |
| CPF/CNPJ | Documento usado para identificar o cliente. | `TaxDocument` |
| Veículo | Automóvel vinculado a um cliente e atendido pela oficina. | `Vehicle` |
| Placa | Identificador do veículo. | `LicensePlate` |
| Ordem de Serviço | Registro principal do atendimento, acompanhamento e execução dos serviços. | `WorkOrder` |
| OS Recebida | Estado inicial da ordem de serviço após sua criação. | `WorkOrderStatus.Created` |
| Diagnóstico | Etapa em que a oficina avalia o veículo e prepara o orçamento. | `WorkOrderStatus.Diagnosing` |
| Orçamento | Proposta com serviços, peças, insumos e valor total. | `Estimate` |
| Orçamento em rascunho | Orçamento ainda editável pela oficina. | `EstimateStatus.Draft` |
| Orçamento submetido | Orçamento enviado ao cliente para decisão. | `EstimateStatus.Pending` |
| Aguardando aprovação | Estado da OS enquanto o cliente avalia o orçamento. | `WorkOrderStatus.WaitingApproval` |
| Aprovação do cliente | Confirmação do orçamento pelo cliente. | `EstimateStatus.Approved` |
| Rejeição do cliente | Recusa do orçamento pelo cliente. | `EstimateStatus.Rejected` |
| Execução | Etapa em que os serviços aprovados são realizados. | `WorkOrderStatus.InProgress` |
| Serviço | Atividade prestada pela oficina, como troca de óleo ou alinhamento. | `Service` |
| Linha de serviço | Serviço incluído no orçamento com preço congelado no momento da inclusão. | `EstimateServiceLine` |
| Serviço iniciado | Linha de serviço em execução. | `EstimateServiceLineStatus.InProgress` |
| Serviço concluído | Linha de serviço finalizada. | `EstimateServiceLineStatus.Completed` |
| Peça ou insumo | Item de estoque usado ou previsto em um orçamento. | `InventoryItem` |
| Linha de estoque | Peça ou insumo incluído no orçamento com quantidade, custo e preço. | `EstimateInventoryLine` |
| Estoque | Quantidade disponível de uma peça ou insumo. | `InventoryItemStockQuantity` |
| OS finalizada | Estado da OS após todos os serviços aprovados serem concluídos. | `WorkOrderStatus.Completed` |
| Veículo entregue | Encerramento operacional da OS após devolução ao cliente. | `WorkOrderStatus.Delivered` |
| OS cancelada | Encerramento antecipado permitido antes da finalização. | `WorkOrderStatus.Cancelled` |
