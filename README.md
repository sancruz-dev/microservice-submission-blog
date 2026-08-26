# Content Submission Service

Serviço de domínio responsável por receber, validar e conduzir submissões de
artigos técnicos através de um workflow de curadoria humana até a publicação
no [blog](../sancruzblog-nextjs). Ver [docs/architecture.md](docs/architecture.md)
para a visão completa do sistema e o roadmap de fases.

## Estado atual (Fase 2)

Este serviço está no estágio de **scaffolding**: estrutura do projeto,
modelo de domínio (`Submission` e seu ciclo de vida) e dois endpoints HTTP,
com persistência **em memória** (não sobrevive a um restart). Ainda não há:

- upload/validação de conteúdo MDX (Fase 3)
- persistência real / banco de dados (Fase 4)
- Docker (Fase 5)
- mensageria, Pipefy, GitHub, CI/CD (Fases 6+)

## Stack

- .NET 9 / ASP.NET Core (Minimal APIs)
- xUnit + `Microsoft.AspNetCore.Mvc.Testing` para testes de integração

## Estrutura

```
src/
  ContentSubmission.Domain/          # Submission, regras de transição de estado, value objects
  ContentSubmission.Application/     # casos de uso (SubmissionService)
  ContentSubmission.Infrastructure/  # implementações concretas (hoje: repositório em memória)
  ContentSubmission.Api/             # endpoints HTTP (ASP.NET Core Minimal APIs)
tests/
  ContentSubmission.Domain.Tests/    # regras de negócio e máquina de estados
  ContentSubmission.Api.Tests/       # testes de integração via WebApplicationFactory
docs/
  architecture.md
  decisions/                         # ADRs
```

## Rodando localmente

```bash
dotnet run --project src/ContentSubmission.Api
```

A API sobe em `http://localhost:5080` (ou na porta definida em
`src/ContentSubmission.Api/Properties/launchSettings.json`).

### Endpoints

```
POST /submissions        cria uma submissão
GET  /submissions/{id}   consulta uma submissão pelo id
```

Exemplo:

```bash
curl -X POST http://localhost:5080/submissions \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Como funciona RabbitMQ",
    "description": "Introdução à mensageria.",
    "authorName": "João da Silva",
    "authorEmail": "joao@example.com",
    "category": "Backend",
    "level": "Intermediate",
    "slug": "como-funciona-rabbitmq",
    "tags": ["rabbitmq", "messaging"]
  }'
```

## Testes

```bash
dotnet test
```

### Provocando falhas de propósito

Para ver as validações de domínio rejeitando entrada inválida (sem precisar
ler o código):

```bash
# slug com tentativa de path traversal -> 400
curl -X POST http://localhost:5080/submissions \
  -H "Content-Type: application/json" \
  -d '{"title":"x","description":"y","authorName":"a","authorEmail":"a@a.com","category":"Backend","level":"Intermediate","slug":"../../etc/passwd","tags":[]}'

# level fora do enum -> 400
curl -X POST http://localhost:5080/submissions \
  -H "Content-Type: application/json" \
  -d '{"title":"x","description":"y","authorName":"a","authorEmail":"a@a.com","category":"Backend","level":"Expert","slug":"algum-slug","tags":[]}'

# id inexistente -> 404
curl http://localhost:5080/submissions/00000000-0000-0000-0000-000000000000
```

No domínio, `SlugTests` e `SubmissionAuthorTests` cobrem especificamente
tentativas de path traversal e e-mails malformados; `SubmissionTests` cobre
transições de estado inválidas (ex: pular direto de `Received` para
`UnderReview`).
