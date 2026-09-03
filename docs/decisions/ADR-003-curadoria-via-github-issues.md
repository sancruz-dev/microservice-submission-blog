# ADR-003: Curadoria humana via GitHub Issues

## Status

Aceito

## Contexto

O corte do Pipefy ([ADR-002](ADR-002-why-not-rabbitmq-and-pipefy.md)) deixou
um gap real: nada decide a transição `UnderReview` → `Approved`/`Rejected`
na máquina de estados do `Submission`. Duas opções foram avaliadas:

1. **Endpoint interno simples** (`PATCH /submissions/{id}/approve|reject`,
   chamado via curl/Postman). Zero integração externa nova, zero superfície
   de webhook — mas não deixa nenhum rastro navegável do artigo em revisão,
   e não reaproveita nada que a Fase 6 já vai precisar construir.
2. **GitHub Issues como quadro de curadoria.** A integração com GitHub já é
   a própria Fase 6 (Pull Requests automáticos) — reaproveitar a mesma API e
   a mesma infraestrutura de webhook para curadoria evita introduzir uma
   ferramenta externa nova (o problema original do Pipefy), mantendo o sinal
   técnico de "webhook + integração com API externa" que eu queria
   aprofundar como estudo.

Opção 2 foi escolhida. Restava decidir **onde** essas Issues vivem.

## Onde as Issues vivem

**Um repositório novo, privado, dedicado só à curadoria** — não o repositório
do próprio serviço (`sancruz-dev/microservice-submission-blog`, hoje
público) e não o repositório público do blog.

Por que não o repositório do serviço: mesmo sendo o candidato mais óbvio (é
onde a integração com GitHub já vive), colocar as Issues lá acopla o
histórico de curadoria — que inclui conteúdo de artigo ainda não publicado,
e potencialmente rejeitado — ao mesmo repositório onde vive o código do
serviço. Manter os dois separados evita que uma decisão sobre visibilidade
de um afete o outro, e evita misturar "conteúdo de terceiros em revisão" com
"código do produto" no mesmo lugar.

**Benefício prático de ser um repositório novo**: como ele nasce privado
desde a criação, não existe a conversão público→privado que seria necessária
se as Issues ficassem no repositório do serviço (que está público hoje). Não
há janela de exposição nem risco de fork público anterior — o repositório
simplesmente nunca é público.

Repositório: [`sancruz-dev/sancruzblog-content-curation`](https://github.com/sancruz-dev/sancruzblog-content-curation)
(privado).

## Decisão

Fluxo:

1. Submissão chega em `Validated` → o serviço cria uma Issue via API do
   GitHub em `sancruz-dev/sancruzblog-content-curation`, com título, autor, categoria,
   nível, descrição, submission ID e o corpo do MDX (ou um preview) para
   leitura direta na própria Issue.
2. Submissão transiciona para `UnderReview`; o número da Issue é persistido
   na `Submission` (o campo que a Fase 4 já previa como "Pipefy card ID"
   passa a ser `GitHubIssueNumber`). O repositório de curadoria é
   configuração da aplicação, não um dado por submissão — não precisa de um
   campo próprio.
3. Curadoria acontece fechando a Issue usando o motivo de fechamento nativo
   do GitHub — **"Close as completed" → `Approved`**, **"Close as not
   planned" → `Rejected`** — sem label customizada nem comando em
   comentário.
4. Um webhook do evento `issues` (`action: closed`, campo `state_reason`),
   configurado em `sancruz-dev/sancruzblog-content-curation`, chega no serviço, valida a
   assinatura HMAC contra o segredo do webhook, localiza a submissão pelo
   `GitHubIssueNumber` e aplica a transição de estado. A entrega do webhook
   pode ser duplicada (reentrega do próprio GitHub, ou retry) — a transição
   precisa ser idempotente (mesma preocupação já prevista para a Fase 9).

**Alternativa mais simples que consideramos**: o endpoint interno (opção 1
do Contexto). Não foi escolhida porque, dado que a integração com GitHub já
ia existir nesta mesma fase, o custo marginal de reaproveitá-la para
curadoria é baixo, e o retorno (nenhuma ferramenta externa nova, histórico
navegável da revisão) supera esse custo.

## Consequências

- Dois repositórios GitHub entram em jogo na Fase 6, não um: o repositório
  privado de curadoria (Issues) e o repositório público do blog (destino dos
  Pull Requests). O serviço precisa de configuração e credenciais de API do
  GitHub para os dois — mesmo token, se o escopo permitir, mas dois nomes de
  repositório distintos em `appsettings`.
- O repositório do serviço (`microservice-submission-blog`) **não** precisa
  virar privado por causa desta decisão — ele não guarda conteúdo de
  submissão. Continua público sem restrição adicional.
- `Submission` ganha um campo `GitHubIssueNumber` (substitui o antigo
  "Pipefy card ID" já previsto em [architecture.md](../architecture.md)).
- O repositório privado de curadoria já existe
  ([`sancruz-dev/sancruzblog-content-curation`](https://github.com/sancruz-dev/sancruzblog-content-curation)),
  criado como privado desde o início. Falta configurá-lo no serviço
  (nome do repositório + credenciais de API) quando a Fase 6 for
  implementada.
