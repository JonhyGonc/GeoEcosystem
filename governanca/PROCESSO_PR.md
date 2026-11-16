# Processo de PR do GeoEcosystem

Base: regra 7 do [protocolo](PROTOCOLO.md) e [Emenda 2](PROTOCOLO.md#emenda-2--agente-único-decisão-do-usuário-em-16112026) (agente único, 16/11/2026). Substitui a versão anterior, que exigia o comentário `APROVADO-CHAT-00` de um coordenador separado.

## Por que existe
O agente e o usuário usam a mesma conta GitHub (`JonhyGonc`). O GitHub não deixa o autor aprovar o próprio PR, e desde a Emenda 2 não há um segundo agente para revisar. A qualidade é garantida por três compensações obrigatórias, e o merge fica com o usuário. Recomenda-se ativar em `main` a regra "exigir PR antes do merge", sem exigir aprovações, para impedir pushes diretos por engano.

## Fluxo
1. O agente cria uma branch a partir da `main` atualizada, com um destes prefixos:
   - `docs/<nnn>-<tema>` para documentação;
   - `contrato/…` para contratos;
   - `decisao/…` para decisões;
   - `feat/…` ou `fix/…` para código.
2. Abre o PR com o [template](../.github/PULL_REQUEST_TEMPLATE.md) preenchido (relatório padrão).
3. **Compensações da Emenda 2** (todas obrigatórias):
   - CI verde em todas as aplicações afetadas, conferida pelo agente nos logs;
   - autorrevisão escrita no PR (checklist do template);
   - roteiro de teste manual (até 10 passos) quando o PR muda o comportamento no Civil 3D ou no GeoAutoDocs.
4. **Merge:** só o usuário faz o merge, depois de ler a autorrevisão e, quando houver, executar o roteiro.
5. Mudanças em `contratos/` exigem, além disso, a seção de compatibilidade preenchida (produtor, consumidor, versão, migração, testes, rollback) e uma Issue com o template `mudanca-de-contrato`.

## O que não pode
- Commit direto na `main` (exceção única: o commit inicial que criou a `main` vazia, registrado no PR #1).
- Merge feito pelo agente.
- Alegar teste, aprovação ou merge que não aconteceu (regra 1).
