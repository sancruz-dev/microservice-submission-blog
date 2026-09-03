# ADR-002: Por que cortamos RabbitMQ e a integração com Pipefy do roadmap

## Status

Aceito

## Contexto

O roadmap original (PROMPT INICIAL, Fase 1) previa RabbitMQ como a cola
assíncrona entre o Submission Service e duas integrações externas — Pipefy
(curadoria humana) e GitHub (publicação via Pull Request) — e listava
mensageria, sistemas distribuídos e event-driven architecture como objetivos
de aprendizado explícitos do projeto.

Até a Fase 5 (Docker), nenhuma dessas peças tinha sido implementada:
RabbitMQ nunca teve um produtor ou consumidor real, e a integração com
Pipefy nunca saiu do diagrama conceitual. A prioridade do autor mudou para
um objetivo de estudo mais alinhado a Engenharia de Dados — Databricks,
Python, Power BI, SQL Server, CI/CD, orquestração de pipelines,
observabilidade, arquitetura de dados/aplicações — área que não inclui
mensageria, processamento assíncrono via fila, nem integração com uma
ferramenta de workflow externa como o Pipefy.

## Decisão

Cortar do roadmap:

- **Fase 6 (RabbitMQ)**: sem produtor/consumidor implementado, sem
  dependência de código em nenhuma camada do serviço — remover é uma
  atualização de plano, não uma migração.
- **Fases 7-8 (Pipefy + Webhooks)**: a curadoria humana continua fazendo
  parte do fluxo (`UnderReview` → `Approved`/`Rejected` continua existindo
  na máquina de estados do `Submission`), mas o mecanismo concreto que
  substitui o Pipefy **ainda não foi desenhado**. Fica como ponto em aberto
  para quando a integração com GitHub (nova Fase 6) for retomada —
  provavelmente um endpoint interno simples (ex: `PATCH
  /submissions/{id}/approve`), mas essa decisão foi deliberadamente adiada
  em vez de tomada às pressas aqui.

Redefinir:

- **Fase 13 → nova Fase 9 (Retry + Idempotência)**: Dead Letter Queue era um
  conceito específico de fila de mensagens e não se aplica mais. Retry e
  idempotência continuam válidos e necessários — agora aplicados a chamadas
  HTTP diretas a integrações externas (ex: a futura chamada à API do
  GitHub), registradas em `ProcessingAttempt` como já estava desenhado em
  [architecture.md](../architecture.md).

Fases 9-15 renumeradas para 6-11 (ver tabela em
[architecture.md](../architecture.md)).

**Alternativa considerada**: manter Pipefy, mas trocar RabbitMQ por uma
chamada HTTP síncrona direta (Submission Service → Pipefy API). Rejeitada
por ora — o problema não é a fila em si, é o próprio Pipefy não agregar
sinal técnico relevante para o objetivo atual do projeto (aprofundar, na
prática, tópicos de engenharia de dados); mantê-lo só trocaria uma
complexidade descartada por outra.

## Consequências

- O diagrama de arquitetura passa a ser `Next.js --HTTP--> Content
  Submission Service --HTTP--> GitHub`, sem intermediário assíncrono.
- Fica um gap real e assumido: não há, ainda, nenhum mecanismo de curadoria
  humana desenhado. `UnderReview` é hoje um estado alcançável na máquina de
  estados, mas sem nenhuma integração real que o leve a `Approved`.
- RabbitMQ pode ser reintroduzido depois sem refatoração, porque nada no
  Domain ou na Application depende dele hoje. Reintroduzir depois de GitHub
  já estar implementado via chamada HTTP direta exigiria converter esse
  ponto de chamada em publish/subscribe — custo moderado, não uma reescrita.
