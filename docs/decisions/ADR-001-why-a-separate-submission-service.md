# ADR-001: Por que um serviço separado (Content Submission Service)?

## Status

Aceito

## Contexto

O blog (`sancruzblog-nextjs`) é hoje um site estático: posts MDX versionados
no próprio repositório, sem backend, sem banco de dados. Queremos evoluir
para uma plataforma de publicação colaborativa, onde terceiros submetem
artigos que passam por validação automática, curadoria humana (via Pipefy) e
publicação automatizada (via Pull Request no GitHub).

Esse workflow tem características bem diferentes do blog em si:

- tem estado próprio (o ciclo de vida de uma submissão), que o blog nunca
  precisou ter;
- integra com sistemas externos (Pipefy, GitHub) que o blog nunca precisou
  chamar;
- processa conteúdo não confiável (MDX enviado por terceiros), que exige
  validação e isolamento que não fazem sentido dentro do runtime do Next.js
  que serve o site publicado.

## Decisão

Criar um serviço de domínio separado (`content-submission-service`), em
repositório próprio, em vez de:

1. Adicionar API routes ao próprio Next.js, ou
2. Fragmentar o workflow em múltiplos microsserviços (Upload Service,
   Validation Service, Pipefy Service, GitHub Service, etc.).

**Alternativa mais simples que consideramos:** API routes dentro do próprio
Next.js. Teria custo de infraestrutura zero (mesmo deploy) e é a opção mais
simples possível.

**Por que não escolhemos a alternativa mais simples:** o domínio de
submissão/curadoria é conceitualmente distinto do domínio de apresentação de
conteúdo, com seu próprio ciclo de vida, persistência e integrações
externas — misturá-lo no Next.js acopla dois sistemas que evoluem por
razões diferentes. Além disso, este projeto é também um projeto de estudo
declarado (ver instruções da Fase 1): ASP.NET Core/C# é uma escolha
deliberada de aprendizado, não uma exigência técnica do domínio. Isso é uma
justificativa legítima desde que reconhecida explicitamente como tal, e não
confundida com "o projeto exige um backend em C#".

**Por que não fragmentar em microsserviços por responsabilidade:** validação
de conteúdo, integração com Pipefy e integração com GitHub não têm hoje
volume, times ou ciclos de deploy independentes que justifiquem processos
separados. Fragmentar agora seria "microservices theater" — complexidade
operacional (múltiplos deploys, múltiplas filas, múltiplos pontos de falha)
sem benefício real. Essas responsabilidades vivem como módulos dentro deste
único serviço; só serão extraídas se uma necessidade concreta aparecer
(ex: um consumidor específico precisando escalar de forma independente).

## Consequências

- O serviço tem seu próprio ciclo de deploy, versionamento e stack,
  independente do blog.
- O Next.js chama este serviço via HTTP simples (sem proxy interno) —
  CORS precisa ser configurado explicitamente quando o frontend passar a
  consumir a API (ainda não acontece na Fase 2).
- Persistência é própria deste serviço, não compartilhada com o blog
  (que hoje nem tem banco de dados).
