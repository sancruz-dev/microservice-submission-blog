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

O Next.js chama esta API diretamente do navegador (página `/submit`), não
por um proxy interno — por isso o serviço precisa de CORS explícito para a
origem do frontend (`Cors:AllowedOrigins` em `appsettings`; em
desenvolvimento, `http://localhost:3000`). Nenhuma origem é permitida por
padrão fora do ambiente de desenvolvimento até que a origem de produção do
blog seja configurada.

## Camadas

- **Domain**: `Submission` (aggregate root) e seus value objects
  (`SubmissionAuthor`, `Slug`). Não depende de nenhuma outra camada, nem de
  bibliotecas de terceiros. Contém a máquina de estados do ciclo de vida da
  submissão.
- **Application**: casos de uso (`SubmissionService`) e o parsing/validação
  do conteúdo MDX enviado (`MdxDocumentParser`, `MdxContentValidator`, em
  `Submissions/Mdx/`). Depende do Domain, de abstrações
  (`ISubmissionRepository`) e de bibliotecas específicas do que ela resolve
  (YamlDotNet, para o frontmatter) — nunca de uma implementação concreta de
  infraestrutura.
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

## Contrato de frontmatter da submissão (Fase 3)

O upload é um arquivo `.mdx` com frontmatter YAML — a mesma forma que
`gray-matter` já lê no blog (ver
[sancruzblog-nextjs/utils/mdx-utils.js](../../sancruzblog-nextjs/utils/mdx-utils.js)).
O frontmatter é a **única fonte de verdade** para os metadados do artigo:
título, descrição, slug, autor, categoria, nível e tags não são repetidos
como campos de formulário separados, porque isso permitiria que o formulário
e o arquivo dissessem coisas diferentes.

```yaml
---
title: "Como funciona RabbitMQ"
description: "Introdução à mensageria."
slug: como-funciona-rabbitmq
author: "João da Silva"
category: "Backend"
level: "Intermediate" # Beginner | Intermediate | Advanced
tags:
  - rabbitmq
  - messaging
---
```

**Correção em relação à proposta da Fase 1**: o campo `date` foi removido do
contrato de submissão. Atribuir uma data de publicação é uma decisão de
curadoria (a data real em que o PR é criado/mergeado, Fase 9-10), não algo
que o autor deveria adivinhar no momento do upload.

O único dado que **não** vem do arquivo é o e-mail de contato do
submetedor (`authorEmail`, campo separado do multipart) — ele existe para o
workflow de curadoria (notificações), não para o artigo publicado, então não
faz sentido morar dentro do frontmatter.

### O que é validado, e por quê

| Validação | Onde | Por quê |
|---|---|---|
| Extensão `.mdx`, arquivo não vazio, tamanho máximo (300 KB) | `SubmissionEndpoints` (borda da API) | Rejeitar rápido, antes de gastar processamento em algo que já está errado |
| Estrutura do frontmatter (delimitadores `---`, YAML válido) | `MdxDocumentParser` | Sem isso não há como nem começar a validar os campos |
| Campos obrigatórios e seus formatos (slug kebab-case, nível dentre o enum, e-mail válido) | `SubmissionService` | Mesmas regras que já protegiam a criação de uma `Submission` na Fase 2, agora aplicadas ao que vem do arquivo |
| `import`/`export` no corpo | `MdxContentValidator` | Não é sobre segurança: `next-mdx-remote` **não bundla** o conteúdo pelo webpack, então um `import` compila mas quebra em tempo de renderização — ver a própria observação do blog em `components/ComponentsForMDX.js` |
| `<script>`, `<iframe>`, `javascript:`, atributos `on*=` | `MdxContentValidator` | Defesa em profundidade contra conteúdo de terceiros; a mitigação principal continua sendo o fato de `pages/posts/[slug].js` só renderizar uma lista fixa de componentes via `MDXRemote components={...}` |

**O que foi cogitado e descartado**: validar que caminhos de imagem
(`![alt](../img/...)`) não contêm `..`. Os posts atuais do blog **já usam**
exatamente esse padrão (`../img/posts/x/arquivo.png`) para referenciar
imagens a partir de `posts/`, então bloquear `..` ali quebraria o próprio
uso legítimo do blog. Além disso, esse caminho nunca é lido do disco por
este serviço — é só texto que vira um `<img src>` resolvido pelo navegador
— então não é path traversal real. O caso que **de fato** vira um caminho de
arquivo de verdade é o `slug` (vira o nome do arquivo `.mdx` no repo do
blog), e esse já é validado à exaustão pelo value object `Slug`.

**O que foi deliberadamente NÃO implementado**: um parser de MDX/JSX
completo (AST). O blog já tem um toolchain de MDX autoritativo
(`next-mdx-remote`, exercitado pelo próprio build/CI do Next.js na Fase 11).
Reimplementar um compilador de MDX em C# só para duplicar essa checagem
seria tecnologia pela tecnologia — o valor real está nas checagens acima,
que são baratas e pegam erros comuns antes de gastar um ciclo de CI ou o
tempo de um curador.

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
| 4 | Persistência real (EF Core + banco), incluindo `ProcessingAttempt` e auditoria |
| 5 | Docker Compose para desenvolvimento local |
| 6 | RabbitMQ e os primeiros eventos assíncronos |
| 7-8 | Integração com Pipefy + webhooks |
| 9-10 | Integração com GitHub + Pull Requests automáticos |
| 11 | CI/CD do próprio serviço e do pipeline de publicação |
| 12 | Observabilidade (correlation ID, logging estruturado, métricas) |
| 13 | Retry, idempotência, Dead Letter Queue |
| 14 | Security hardening |
