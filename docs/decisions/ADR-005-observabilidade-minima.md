# ADR-005: Observabilidade mínima (Correlation ID, logs JSON, métricas nativas)

## Status

Aceito

## Contexto

Até a Fase 7 o serviço rodava em produção (Azure Container Apps) sem
nenhuma observabilidade própria: os logs eram o console padrão do ASP.NET
Core em texto livre, coletados automaticamente pelo Container Apps no Log
Analytics do ambiente. Isso é suficiente para ler o que aconteceu, mas não
para responder duas perguntas concretas:

- **"Essas 40 linhas de log pertencem a qual requisição?"** — sem um
  identificador por requisição, linhas de requisições concorrentes se
  intercalam e não há como separar.
- **"Quantas submissões chegaram esta semana, e quantas foram
  rejeitadas?"** — log serve para investigar um caso; contar ao longo do
  tempo é o que métrica resolve.

A Fase 8 do roadmap (ver [architecture.md](../architecture.md)) previa
"correlation ID, logging estruturado, métricas". A decisão a tomar era
**quanta** infraestrutura de observabilidade adotar: o ecossistema
OpenTelemetry completo (traces distribuídos, spans manuais, collector
próprio) ou o mínimo que responde às perguntas acima.

## Decisão

- **Correlation ID como middleware próprio** (`CorrelationIdMiddleware`),
  não uma biblioteca. Lê `X-Correlation-Id` do request (reaproveitando um ID
  que o chamador tenha gerado), gera um `Guid` quando ausente, devolve no
  header de resposta e o anexa a todo log da requisição via
  `ILogger.BeginScope`. São ~25 linhas; qualquer pacote de terceiros para
  isso seria mais dependência do que código economizado.
- **Logging estruturado com `AddJsonConsole` nativo**, não Serilog.
  `IncludeScopes = true` é o que faz o Correlation ID (e os scopes do
  próprio ASP.NET Core: `RequestPath`, `ConnectionId`, `TraceId`) aparecer
  em cada linha. Serilog só se pagaria se precisássemos de sinks que o
  provider nativo não tem — e não precisamos: o Container Apps já coleta o
  `stdout` para o Log Analytics, então "escrever JSON no console" **é** o
  pipeline de ingestão.
- **Métricas com `System.Diagnostics.Metrics` nativo**
  (`SubmissionMetrics`, um `Meter` e dois `Counter<long>`), não uma
  biblioteca de métricas. É a mesma API que o OpenTelemetry consome por
  baixo, sem acoplar o Domain/Application a nada.
- **Exportação via `Azure.Monitor.OpenTelemetry.AspNetCore`** para um
  recurso Application Insights *workspace-based*, ligado ao **mesmo**
  workspace de Log Analytics que o Container Apps Environment já usava.
  Esse é o único pacote NuGet novo da fase, e é incontornável: exportar
  telemetria para fora do processo sempre exige um cliente. Em troca,
  ele também liga a auto-instrumentação de requests e de chamadas HTTP de
  saída (as chamadas à API do GitHub) sem código adicional.
- **Registro condicional do Azure Monitor**: só quando
  `ApplicationInsights:ConnectionString` está configurada.
  `UseAzureMonitor()` lança na inicialização se recebe uma connection
  string vazia, o que derrubaria a aplicação inteira em CI e em qualquer
  máquina de desenvolvimento sem User Secrets configurados. Sem telemetria
  configurada, a aplicação sobe normalmente e apenas não exporta nada.
- **Provisionamento do Application Insights via Portal**, não `az` CLI —
  exceção consciente à convenção do
  [ADR-004](ADR-004-deploy-azure-container-apps.md), por preferência de
  visualização durante a configuração inicial.
- **`EnableRetryOnFailure()` no `UseSqlServer`, apenas fora de
  Development.** Não estava no escopo da fase: apareceu *por causa* dela.
  Com o Correlation ID no ar, o primeiro teste de ponta a ponta em produção
  expôs um `SqlException 40613` ("database is not currently available") ao
  persistir a submissão — o Azure SQL serverless
  ([ADR-004](ADR-004-deploy-azure-container-apps.md)) estava em auto-pause e
  não acordou a tempo. A Issue de curadoria já tinha sido criada no GitHub
  nesse ponto, então a falha deixou uma Issue órfã sem `Submission`
  persistida. O retry fica restrito a ambientes não-Development porque
  reativá-lo globalmente reabriria a pegadinha documentada na Fase 5
  (retry + `Database.MigrateAsync()` no startup quebra a criação de um SQL
  Server local do zero, já que `CREATE DATABASE` não é idempotente);
  produção nunca roda essa migração automática.

## O que foi deliberadamente deixado de fora

- **Tracing distribuído manual** (spans próprios, `ActivitySource`). O que a
  auto-instrumentação já entrega — a requisição HTTP e as chamadas de saída
  ao GitHub correlacionadas pelo mesmo `TraceId` — cobre a topologia real
  deste serviço, que é uma chamada síncrona para um único sistema externo.
  Instrumentar spans manualmente aqui seria decorar o que já está visível.
- **Endpoint `/metrics` no formato Prometheus.** Só faria sentido com algo
  fazendo scrape — não existe Prometheus/Grafana neste stack, e o
  Application Insights já recebe as mesmas métricas por push.
- **Alertas automáticos.** Faz sentido depois que houver uma linha de base
  de tráfego real; alertar sobre um serviço sem uso gera só ruído.

## Consequências

- Existe agora um recurso Azure a mais (Application Insights) e um consumo
  de ingestão que conta contra a cota gratuita de 5 GB/mês do workspace —
  compartilhada com os logs de console que o Container Apps já enviava.
  Para o volume deste projeto isso é folgado, mas deixa de ser "custo zero
  garantido" e passa a ser "pay-as-you-go dentro da faixa gratuita".
- A connection string do Application Insights vira mais um segredo a
  gerenciar (secret do Container App em produção, User Secrets localmente),
  seguindo o mesmo padrão já estabelecido no
  [ADR-004](ADR-004-deploy-azure-container-apps.md).
- O Log Analytics **não** quebra o JSON de cada linha de log em colunas
  automaticamente: consultar por `CorrelationId` exige `parse_json`/
  `extract` na query KQL. É o custo de logar JSON no `stdout` em vez de
  usar uma tabela customizada com Data Collection Rule — que seria mais
  infraestrutura para um ganho de ergonomia em consulta.
- A observabilidade provou seu valor antes mesmo de fechar a fase: o bug do
  auto-pause do banco (e a Issue órfã que ele gerou) só foi encontrado
  porque o Correlation ID permitiu reconstruir a requisição inteira a partir
  do log. Esse mesmo caso vira insumo direto do escopo da Fase 9.
