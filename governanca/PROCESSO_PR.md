# Processo de PR do GeoEcosystem

Base: regra 7 do [protocolo](PROTOCOLO.md) e decisão E3-2 da [proposta da Etapa 3](../auditorias/2026-11-etapa1/ETAPA_3_PROPOSTA_GEOECOSYSTEM.md).

## Por que existe
Todos os agentes usam a mesma conta GitHub (`JonhyGonc`). O GitHub não permite que o autor aprove o próprio PR e não distingue qual agente agiu. Mesmo com proteção de branch (disponível porque o repositório é público), a exigência de aprovação não funciona com uma conta só. Então a aprovação é registrada **por convenção**, e o merge fica com o usuário. Recomenda-se ativar em `main` a regra "exigir PR antes do merge", sem exigir aprovações, para impedir pushes diretos por engano.

## Fluxo
1. O agente cria uma branch `docs/<nnn>-<tema>` (ou `contrato/…`, `decisao/…`) a partir da `main` atualizada.
2. Abre o PR com o [template](../.github/PULL_REQUEST_TEMPLATE.md) preenchido (relatório padrão).
3. **Revisão do Chat 00:** o Chat 00 comenta no PR, começando a primeira linha com:
   - `APROVADO-CHAT-00` — pode ser mesclado;
   - `MUDANÇAS-CHAT-00` — precisa de ajuste (com a lista).
   Quando o próprio Chat 00 é o autor, ele registra uma autorrevisão e o usuário revisa.
4. **Merge:** só o usuário faz merge, depois de `APROVADO-CHAT-00`.
5. Mudanças em `contratos/` exigem, além disso, a seção de compatibilidade preenchida (produtor, consumidor, versão, migração, testes, rollback).

## O que não pode
- Commit direto na `main` (exceção única: o commit inicial que criou a `main` vazia, registrado no PR #1).
- Merge sem `APROVADO-CHAT-00`.
- Alegar aprovação ou merge que não aconteceu (regra 1).
