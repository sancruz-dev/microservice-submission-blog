# ADR-004: Deploy do serviço no Azure Container Apps

## Status

Aceito

## Contexto

Até a Fase 6, o Content Submission Service só rodava localmente
(`dotnet run` ou `docker compose`). A Fase 7 (CI/CD) já tinha CI básica nos
dois repositórios; faltava o lado de **deploy contínuo**. Isso também é o
pedaço do roadmap com maior alinhamento direto ao objetivo de estudo que
motivou o pivô do projeto - infraestrutura Cloud e CI/CD são o foco
explícito dessa fase.

Três decisões precisavam ser tomadas: onde rodar o container, onde guardar
a imagem, e onde rodar o banco.

## Decisão

- **Compute: Azure Container Apps**, plano consumption. Escala a zero
  (sem tráfego, sem custo de computação) e tem um grant grátis mensal por
  assinatura (180k vCPU-segundos + 360k GiB-segundos + 2M requisições) que
  cobre um projeto pessoal de baixo tráfego integralmente. Alternativas
  descartadas: Azure App Service for Containers cobra 24/7 mesmo ocioso no
  plano com suporte a container customizado (não existe tier grátis pra
  isso); Azure Container Instances é mais simples mas não resolve
  HTTPS/domínio customizado de fábrica.
- **Registro de imagem: Docker Hub**, não Azure Container Registry. ACR
  Basic custa ~$5/mês fixo, mesmo parado - não é pay-per-use como o resto
  do stack. Docker Hub com repositório público é $0, e a imagem não carrega
  nenhum segredo (tudo vem de env var/secret do Container App em runtime) -
  não há motivo de segurança pra mantê-la privada.
- **Banco: Azure SQL Database serverless**, com auto-pause e opt-in
  explícito no free offer (`--use-free-limit
  --free-limit-exhaustion-behavior AutoPause`) - 100k vCore-segundos + 32GB
  dados + 32GB backup grátis por mês, permanente (não é o trial de $200/30
  dias, que já estava esgotado). `AutoPause` como comportamento de
  exaustão garante que o banco nunca gera cobrança além do previsto: se o
  limite gratuito acabar num mês, ele pausa em vez de continuar cobrando.
- **Segredos em produção: secrets nativos do Container App** (env vars),
  não Azure Key Vault. Volume pequeno de segredos (token do GitHub,
  segredo do webhook, connection string) não justifica o recurso e a
  identidade gerenciada extra que o Key Vault exigiria.
- **Autenticação do GitHub Actions no Azure: OIDC federado** (Azure AD App
  Registration + federated credential confiada ao emissor do GitHub
  Actions, escopo restrito a `repo:sancruz-dev/microservice-submission-blog:ref:refs/heads/main`),
  não um Service Principal com client secret de longa duração. Nenhum
  segredo do Azure precisa ser gerado, rotacionado ou vazar.

## Consequências

- Dois registries de fato existem agora (Docker Hub p'ras imagens do
  serviço, e os dois repositórios GitHub já usados pra curadoria/PR) -
  mais uma credencial (Docker Hub) a gerenciar como secret do GitHub
  Actions.
- Migrations do EF Core não rodam automaticamente em produção (só em
  Development, por design - ver architecture.md). Aplicar uma migration
  nova em produção continua sendo um passo manual (`dotnet ef database
  update` apontando pra connection string do Azure), documentado em
  [local-development.md](../local-development.md), não automatizado - o
  volume de migrations não justifica construir esse mecanismo agora.
- O Container App expõe uma URL pública HTTPS permanente
  (`*.brazilsouth.azurecontainerapps.io`) - os dois webhooks (Issues no
  repo de curadoria, Pull Request no repo do blog) passam a apontar pra
  ela, substituindo o túnel `gh webhook forward` usado só para
  desenvolvimento/teste local.
