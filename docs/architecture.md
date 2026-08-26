# Arquitetura

## Visão geral

O Content Submission Service é um serviço de domínio único responsável pelo
workflow de submissão e curadoria de artigos técnicos para o
[sancruzblog-nextjs](../../sancruzblog-nextjs). Ele **não** é um conjunto de
microsserviços por responsabilidade (upload, validação, Pipefy, GitHub,
notificação) — essas responsabilidades vivem como módulos dentro deste único
serviço, a menos que surja uma justificativa arquitetural real para separar
algum deles (ver [ADR-001](decisions/ADR-001-why-a-separate-submission-service.md)).

```
Next.js (blog) --HTTP--> Content Submission Service --RabbitMQ--> Pipefy / GitHub
                                    |
                              banco próprio
```

## Camadas (Fase 2)

- **Domain**: `Submission` (aggregate root) e seus value objects
  (`SubmissionAuthor`, `Slug`). Não depende de nenhuma outra camada. Contém a
  máquina de estados do ciclo de vida da submissão.
- **Application**: casos de uso (`SubmissionService`). Depende só do Domain e
  de abstrações (`ISubmissionRepository`), nunca de uma implementação
  concreta de infraestrutura.
- **Infrastructure**: implementações concretas das abstrações da Application.
  Hoje só existe `InMemorySubmissionRepository` — placeholder até a Fase 4
  trazer persistência real.
- **Api**: endpoints HTTP (ASP.NET Core Minimal APIs), tradução entre
  contratos HTTP (DTOs) e o domínio.

## Ciclo de vida da Submission

```
Received → Validating → Validated → UnderReview → Approved → Publishing → Published
               ↓                        ↓
           Rejected                 Rejected
```

Decisão importante: **falha e retry não são estados da Submission.**
`FAILED`/`RETRYING` foram cogitados na fase de análise, mas modelá-los como
estados do enum principal explodiria a máquina de estados sem necessidade.
Quando a Fase 13 (Retry + Idempotência + DLQ) chegar, tentativas de
processamento (ex: uma chamada ao Pipefy que falhou e será tentada de novo)
serão registradas em uma entidade separada (`ProcessingAttempt`, ainda não
implementada) associada à submissão — a submissão em si permanece no mesmo
status enquanto uma nova tentativa acontece, e só transiciona para um estado
terminal se todas as tentativas se esgotarem.

## Por que persistência em memória agora?

A Fase 2 existe para validar a estrutura do serviço e o ciclo de vida da
Submission, não o schema do banco. Fixar um schema de banco antes de saber
exatamente quais campos os fluxos de Pipefy/GitHub (Fases 7-10) vão precisar
seria comprometimento prematuro. `ISubmissionRepository` isola essa decisão:
trocar a implementação em memória por EF Core na Fase 4 não deve exigir
mudanças no Domain, na Application ou na Api.

## O que ainda não existe (por fase)

| Fase | Escopo |
|---|---|
| 3 | Upload e validação de conteúdo MDX (o payload de criação hoje é só metadados) |
| 4 | Persistência real (EF Core + banco), incluindo `ProcessingAttempt` e auditoria |
| 5 | Docker Compose para desenvolvimento local |
| 6 | RabbitMQ e os primeiros eventos assíncronos |
| 7-8 | Integração com Pipefy + webhooks |
| 9-10 | Integração com GitHub + Pull Requests automáticos |
| 11 | CI/CD do próprio serviço e do pipeline de publicação |
| 12 | Observabilidade (correlation ID, logging estruturado, métricas) |
| 13 | Retry, idempotência, Dead Letter Queue |
| 14 | Security hardening |
