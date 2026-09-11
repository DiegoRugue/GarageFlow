# ADR 0001 - Quatro repositórios por responsabilidade na AWS Academy

Data: 11/09/2026. Status: aceito; implementação pendente.

## Contexto

O Tech Challenge Fase 3 exige repositórios separados para função serverless, infraestrutura Kubernetes, infraestrutura do banco gerenciado e aplicação principal. A Fase 2 já possui .NET 10, aplicação modular e automação de EKS/RDS com Terraform e GitHub Actions. A evolução mantém a AWS Academy, cujo ambiente temporário é disponibilizado pela pipeline de provisionamento em sessões de quatro horas.

## Decisão

Manter a aplicação principal como monólito modular com Clean Architecture, DDD e vertical slices, em `GarageFlow`. Separar código e entrega em três outros repositórios por responsabilidade: `garageflow-serverless`, `garageflow-infra-kubernetes` e `garageflow-infra-database`. Manter AWS Academy e PostgreSQL gerenciado como base de evolução.

Cada recurso terá um único dono; cada repositório terá CI/CD e documentação próprios. A aplicação continua dona do schema de negócio e das migrations EF. Infraestrutura do banco provisiona a instância e seus controles operacionais. A solution principal mantém o Host como único executável; a função possui composition root na sua própria solution.

## Alternativas consideradas

- Manter um repositório até a entrega: adia a separação e não satisfaz a estrutura final obrigatória.
- Transformar módulos de negócio em microsserviços: amplia a coordenação de dados/contratos sem corresponder a uma exigência dessa fase.
- Trocar de nuvem: descarta parte relevante do estudo e da infraestrutura existente; o usuário escolheu continuar na Academy.

## Consequências

É possível evoluir as entregas sem reescrever o domínio. Em contrapartida, é necessário versionar contratos entre pipelines, separar states por dono/ambiente, planejar sua migração e coordenar mudanças incompatíveis. As limitações de permissão, capacidade e duração de sessão do laboratório precisam de evidência e não podem ser confundidas com capacidades de uma conta AWS irrestrita.

Os nomes concretos, propriedade fina, sequência de bootstrap e opções de identidade estão detalhados no [RFC 0001](../rfcs/0001-phase-3-platform-and-identity.md). O registro desta decisão não afirma que os quatro repositórios ou recursos de nuvem já foram criados.
