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
  `EfSubmissionRepository` (EF Core + SQL Server) é a implementação real,
  usada pela Api. `InMemorySubmissionRepository` continua existindo só como
  dublê de teste para `ContentSubmission.Api.Tests` (ver
  [Persistência](#persistência-fase-4) abaixo).
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

## Persistência (Fase 4)

`ISubmissionRepository` (Application) tinha exatamente um propósito até
aqui: isolar a Application/Domain de qual implementação concreta guarda os
dados. A Fase 4 exercita isso trocando `InMemorySubmissionRepository` por
`EfSubmissionRepository` (EF Core + SQL Server) como implementação real —
sem qualquer mudança no Domain ou na Application.

**Correção em relação ao que este documento dizia antes**: a tabela de fases
listava `ProcessingAttempt` e auditoria como parte da Fase 4. Isso foi
revisto — essas entidades existem para rastrear tentativas de integração
externa (Pipefy/GitHub), que só chegam nas Fases 6-10. Desenhar esse schema
agora, sem nenhum produtor real desses dados, seria o mesmo tipo de
comprometimento prematuro que o uso de memória evitou na Fase 2. A Fase 4
persiste **só** `Submission`.

### Mapeamento objeto-relacional

`Submission` continua um modelo de domínio rico (setters privados, sempre
válido), não um DTO anêmico — isso tem um custo real de configuração do EF
Core que vale documentar:

- **`Slug`** e **`Tags`** são mapeados via `ValueConverter` para uma única
  coluna (`nvarchar` e `nvarchar(max)`/JSON, respectivamente) — o mesmo
  princípio do `Slug.Create()` sendo o único jeito de produzir um `Slug`
  válido se aplica aqui: a conversão garante que o que sai do banco já passou
  pela mesma validação.
- **`SubmissionAuthor`** é mapeado como *EF Complex Type* (`ComplexProperty`,
  não `OwnsOne`) — é um value object sem identidade própria, exatamente o
  caso de uso que complex types (novidade do EF Core 8) resolvem, ao
  contrário de owned entities (pensadas para algo com potencial de
  identidade/tabela própria).
- **`Status`** e **`Level`** são gravados como texto (`nvarchar`), não como
  `int` — convenção legível ao inspecionar a tabela manualmente; o custo de
  armazenamento extra é irrelevante para o volume esperado.
- `Submission` ganhou um construtor privado sem parâmetros, reservado para o
  EF Core materializar instâncias via reflection (setando cada propriedade
  depois). O construtor rico usado por `Create()` continua sendo o único
  caminho de construção pública — `Author` é um exemplo concreto de por que
  isso foi necessário: o EF Core **nunca** faz constructor binding de uma
  propriedade owned/complex no tipo dono, então a construção "tudo via
  parâmetros" que funcionava em memória não é compatível com o
  materializador do EF Core.

### Estratégia de testes

Três camadas, cada uma testando uma coisa diferente:

- **`ContentSubmission.Api.Tests`**: continuam usando
  `InMemorySubmissionRepository` (via `TestWebApplicationFactory`, que troca
  o registro de DI e força `ASPNETCORE_ENVIRONMENT=Testing` para não rodar
  migração automática). Testam comportamento HTTP/validação, não
  persistência — não precisam de banco nenhum, nem em CI.
- **`ContentSubmission.Infrastructure.Tests`** (novo): exercita
  `EfSubmissionRepository` de verdade contra SQLite em memória. SQLite não é
  SQL Server, mas aplica SQL real e valida exatamente o que este mapeamento
  precisa provar (constructor binding, os conversores, o complex type) sem
  exigir uma instância de banco disponível em CI.
- **Verificação manual contra SQL Server real**: migração gerada com
  `dotnet ef migrations add`, aplicada com `dotnet ef database update` contra
  uma instância SQL Server de verdade, e testada via `curl` — incluindo
  matar e reiniciar o processo da API para confirmar que os dados sobrevivem
  ao restart (a prova que realmente importa, que nenhum teste automatizado
  com SQLite substitui).

### Configuração local

A connection string vem de `ConnectionStrings:SubmissionDb`. Um valor
genérico (`Server=localhost;...`) está versionado em
`appsettings.Development.json` como *default* portável; a instância real de
cada máquina de desenvolvimento (nome/instância variam por pessoa) deve ir em
[.NET User Secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets)
(`dotnet user-secrets set "ConnectionStrings:SubmissionDb" "..."` dentro de
`src/ContentSubmission.Api`), nunca commitada. Em desenvolvimento, migrações
pendentes são aplicadas automaticamente no startup (`Database.MigrateAsync()`
em `Program.cs`) — conveniente por ainda não existir pipeline de deploy
(Fase 11); não é assim que migrações devem rodar num ambiente real.

## Docker (Fase 5)

`docker-compose.yml` sobe o Submission Service e um SQL Server próprio,
isolado de qualquer instância nativa já instalada (porta e volume
diferentes). Só esses dois — RabbitMQ ainda não tem produtor/consumidor
(chega na Fase 6) e o blog Next.js já roda bem sozinho, sem depender de SQL
Server nem do .NET SDK. Ver
[docs/local-development.md](local-development.md) para os dois fluxos
(nativo vs. Docker) lado a lado.

Não há Kubernetes nem orquestração além do Compose — não faz sentido nesta
etapa do projeto.

**Uma pegadinha real encontrada ao testar restart**: a primeira versão desta
fase adicionou `EnableRetryOnFailure()` ao `UseSqlServer(...)` como reforço
de resiliência (falhas transitórias de conexão são mais comuns falando com
um SQL Server dentro de um container). Isso quebrou o restart: como o
`Database.MigrateAsync()` do startup roda dentro dessa mesma estratégia de
retry, e `CREATE DATABASE` não é idempotente, uma tentativa que falhasse no
meio (ex: conexão caiu logo após criar o banco) fazia o retry tentar
`CREATE DATABASE` de novo — e falhar com "database already exists",
derrubando a API num loop de crash. Removido; a resiliência real para "SQL
Server ainda não terminou de subir" veio de `restart: unless-stopped` no
Compose (nível de container, sem re-executar DDL não-idempotente), não de
retry no nível da aplicação.

## O que ainda não existe (por fase)

| Fase | Escopo |
|---|---|
| 6 | RabbitMQ e os primeiros eventos assíncronos |
| 7-8 | Integração com Pipefy + webhooks |
| 9-10 | Integração com GitHub + Pull Requests automáticos |
| 11 | CI/CD do próprio serviço e do pipeline de publicação |
| 12 | Observabilidade (correlation ID, logging estruturado, métricas) |
| 13 | Retry, idempotência, Dead Letter Queue |
| 14 | Security hardening |
