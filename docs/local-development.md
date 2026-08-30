# Ambiente de desenvolvimento local

Existem dois jeitos independentes de rodar o Submission Service localmente.
Use o que fizer mais sentido pra você em cada momento — eles não interferem
um no outro.

## Opção 1 — nativo (`dotnet run`)

O que foi usado nas Fases 2-4. Requer .NET 9 SDK e um SQL Server instalado
(qualquer edição — Express, Developer, LocalDB via instância nomeada).

```bash
dotnet run --project src/ContentSubmission.Api
```

A connection string vem de `ConnectionStrings:SubmissionDb`. Um valor
genérico está em `appsettings.Development.json` (versionado, serve de
fallback); a instância real da sua máquina deve ir em
[.NET User Secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets),
nunca commitada:

```bash
cd src/ContentSubmission.Api
dotnet user-secrets set "ConnectionStrings:SubmissionDb" "Server=SEU_SERVIDOR;Database=ContentSubmissionDb;Trusted_Connection=True;TrustServerCertificate=True;"
```

Migrações pendentes são aplicadas automaticamente no startup quando
`ASPNETCORE_ENVIRONMENT=Development` (que é o padrão do `dotnet run`).

## Opção 2 — Docker Compose

Não precisa ter .NET SDK nem SQL Server instalados — só Docker. Sobe o
Submission Service e um SQL Server **próprio, isolado** (porta e volume
diferentes de qualquer instância nativa que você já tenha, então os dois
podem coexistir sem conflito).

```bash
cp .env.example .env    # ajuste SQL_SA_PASSWORD se quiser
docker compose up --build
```

A API fica em `http://localhost:5211` — a mesma porta padrão do `dotnet run`
nativo, de propósito: o frontend (`NEXT_PUBLIC_SUBMISSION_SERVICE_URL`) não
precisa saber se está falando com a versão nativa ou containerizada.

Migrações também são aplicadas automaticamente no startup do container
(mesmo mecanismo do modo nativo).

Para derrubar tudo (mantendo os dados):

```bash
docker compose down
```

Para derrubar e apagar os dados também:

```bash
docker compose down -v
```

### Por que RabbitMQ e o blog Next.js não estão aqui

RabbitMQ ainda não tem nenhum código publicando ou consumindo mensagens —
isso chega na Fase 6. Subir o container agora seria infraestrutura sem uso
real. O blog Next.js já roda bem sozinho (`npm run dev`, ou deploy via
Vercel) sem precisar de SQL Server nem do .NET SDK — conteinerizá-lo não
resolve nenhuma fricção real que exista hoje. Ambos podem ser adicionados
depois, se/quando fizerem sentido (ex: um ambiente de demonstração completo,
ou os testes de CI da Fase 11).

## Qual escolher?

- **Nativo**: iteração rápida no dia a dia (hot reload do `dotnet run`,
  depuração direta na IDE).
- **Docker**: validar que o serviço builda e roda de forma isolada, ou
  onboarding de alguém sem .NET/SQL Server instalados na máquina.
