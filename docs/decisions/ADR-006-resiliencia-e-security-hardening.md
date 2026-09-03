# ADR-006: Resiliência HTTP, ordem persistir-antes-de-integrar e security hardening

## Status

Aceito

## Contexto

Fechada a Fase 8 (observabilidade, ADR-005), o Correlation ID recém-criado
mostrou em produção uma falha real: uma submissão que criou a Issue no
GitHub e depois falhou ao salvar no Azure SQL (auto-pause do serverless,
ver ADR-005), deixando uma Issue órfã em um sistema onde não existe
rollback. Isso motivou uma análise das Fases 9 (Retry/idempotência) e 10
(Security hardening) do roadmap para decidir o que vale a pena implementar
antes de fechar o projeto - critério: o que é justificável numa entrevista
e o que é exposição real, não o que está bonito no checklist.

## Decisão

### Feito

1. **Resiliência HTTP nas chamadas ao GitHub.** `GitHubIssueClient` e
   `GitHubPullRequestClient` não tinham nenhum retry - um 502/503 ou um 429
   de rate limit da API do GitHub derrubava a submissão inteira.
   `GitHubPullRequestClient` é o mais exposto: publica uma submissão com
   quatro chamadas em sequência (SHA → branch → arquivo → PR), e qualquer
   uma falhando media o processo pela metade. Adicionado
   `AddStandardResilienceHandler()` (`Microsoft.Extensions.Http.Resilience`)
   em ambos os `HttpClient` no `Program.cs` - retry com backoff exponencial
   e jitter, circuit breaker e timeout, sem tocar no código dos clients.

2. **Ordem persistir → integrar no `SubmissionService.CreateAsync`.** Antes:
   criava a Issue no GitHub e só then salvava no banco - exatamente a ordem
   que causou a Issue órfã em produção. Agora: a submissão é persistida
   como `Validated` primeiro, a Issue é criada depois, e o número da Issue é
   gravado num `UpdateAsync` subsequente. Isso não elimina a janela de falha
   parcial entre dois sistemas sem transação distribuída - só uma
   outbox/saga eliminaria - mas inverte o modo de falha: em vez de lixo
   irrecuperável num sistema de terceiros, uma falha agora deixa uma linha
   recuperável no *nosso* banco, visível e reprocessável.

3. **Removidos `GET /submissions` e `GET /submissions/{id}`.** O primeiro
   devolvia a lista completa de submissões - incluindo o e-mail de cada
   autor - para qualquer requisição não autenticada; o segundo tinha o
   mesmo problema por id. Nenhum dos dois é consumido pelo frontend (que só
   chama `POST /submissions`), então a correção mais simples e honesta foi
   remover, não proteger com API key. Consultas de leitura/depuração agora
   são feitas diretamente contra o Azure SQL (ver a query KQL de
   observabilidade da Fase 8 e uma consulta SQL direta ao workspace, não
   contra a API pública).

4. **Rate limiting em `POST /submissions`.** Endpoint público e sem
   autenticação (por design - é o formulário de submissão do blog) que cria
   uma Issue no GitHub e grava no banco a cada chamada. Sem limite, um
   script simples esgota a cota de rate limit da API do GitHub e o free
   tier do Azure SQL. Usado `AddRateLimiter` nativo do ASP.NET Core (sem
   pacote novo): janela fixa de 5 requisições/minuto por IP do cliente,
   `429 Too Many Requests` na rejeição. Um limite bem mais alto é aplicado
   no ambiente `Testing` porque `WebApplicationFactory` reusa a mesma
   conexão/IP entre muitos `POST`s por classe de teste.

### Cortado, com justificativa

- **Entidade `ProcessingAttempt`** (prevista em `architecture.md`): foi
  desenhada para um mundo com fila e retry em background. Com retry
  síncrono via `Microsoft.Extensions.Http.Resilience`, não existe produtor
  desses dados - construir a tabela agora seria o mesmo "comprometimento
  prematuro" que a Fase 4 já evitou.
- **Idempotency key no `POST /submissions`** (duplo clique gerando duas
  Issues): real, mas de baixíssima probabilidade num blog pessoal, e a
  correção decente exige uma tabela de chaves dedicada.
- **Security headers** (HSTS, CSP, X-Content-Type-Options): é uma API JSON
  consumida por `fetch`, não uma página renderizada - o ganho é
  praticamente nulo. HTTPS já é forçado pelo ingress do Container Apps.
- **`DisableAntiforgery()` no POST**: não é uma falha, é a decisão correta
  para uma API cross-origin chamada de outro domínio, onde a defesa real é
  o CORS restrito já existente.
- **Autenticação completa no `POST /submissions`**: o formulário precisa
  ser público - é o produto. Rate limiting é a defesa adequada aqui, não
  login.

## Consequências

- Falhas transientes na API do GitHub não derrubam mais a submissão
  inteira; o custo é uma pequena latência adicional em retries.
- Uma falha ao criar a Issue do GitHub agora deixa uma submissão
  `Validated` no banco - recuperável e reprocessável -, não uma Issue órfã.
- Nenhuma lista de e-mails de autores fica mais acessível publicamente.
  Consultas administrativas passam a ser feitas via SQL/KQL direto, fora do
  path HTTP público.
- Tráfego malicioso ao `POST /submissions` é limitado a 5 req/min por IP;
  não protege contra um atacante distribuído (fora de escopo para um blog
  pessoal).
