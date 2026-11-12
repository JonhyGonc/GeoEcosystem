# DEC-ECO-0001 — Repositório único (monorepo) no GeoEcosystem

- **Estado:** aprovada
- **Data:** 11/11/2026, decisão do usuário, confirmada na aprovação do roadmap ("Roadmap aprovado", 11/11/2026 08:34)
- **Origem:** pergunta do usuário em 11/11/2026; análise do Chat 00; ROADMAP M2 (D-08)

## Contexto
- Um desenvolvedor, uma conta GitHub, quatro repositórios.
- A auditoria mostrou problemas causados pela separação:
  - o contrato GADX está duplicado em GAE e GAD, e as cópias já divergiram (GAD-12b);
  - a fixture do GeoMap é copiada à mão do GAE (GM-10);
  - não há nenhum teste que atravesse projetos (F-11).
- Os marcos M3–M5 atravessam projetos.
- A política das sessões de agente bloqueou a escrita no GAE e no GAD; um repositório único exige uma autorização só.

## Alternativas
| Alternativa | Avaliação |
|---|---|
| (a) Manter 3 repositórios + fixtures e testes cruzados | A duplicação continua; cada mudança de contrato exige PRs coordenados em 2 repositórios |
| (b) Juntar só GAE + GAD | Resolve o GADX, mas deixa o GeoMap de fora do M4/M5 |
| **(c) Juntar os três dentro do `GeoEcosystem`** | **Escolhida.** Tudo testado junto; contrato compartilhado vira código em `libs/` |

**Visibilidade:** o repositório é público, e o usuário autorizou o código do GeoMap ficar público (11/11/2026). Os itens de privacidade da varredura do histórico (M22-01…04) foram aceitos pelo usuário em 12/11/2026.

## Decisão
1. **Estrutura:**
   - `apps/geomap/`, `apps/geoautoengine/`, `apps/geoautodocs/`: cada um com o histórico completo (`git subtree add`, sem squash);
   - `libs/`: código compartilhado (nasce no M3);
   - documentação central na raiz, como hoje.
2. **CI:** um workflow por aplicação em `.github/workflows/`, disparado só por mudanças na pasta dela (`paths`), com `working-directory` apontando para a aplicação.
3. **Versões:** cada aplicação mantém a sua versão. As tags passam a ter prefixo: `geoautoengine/v3.2.0`, `geoautodocs/v1.1.0`. As tags antigas continuam nos repositórios arquivados.
4. **Repositórios antigos:** arquivados (somente leitura), nunca apagados, com aviso no README. O `Geomap` antigo continua privado, com as suas Issues e PRs #1–#65.
5. **Governança:** a regra 6 do protocolo passa a valer por pasta ([Emenda 1](../governanca/PROTOCOLO.md#emendas)).

## Impacto
- **Chats 01–03:** passam a trabalhar em `apps/<app>` do `GeoEcosystem`; os `AGENTS.md` de cada aplicação continuam valendo dentro da pasta dela.
- **Usuário:** um repositório para clonar, autorizar e acompanhar; os builds e instaladores saem das pastas novas.
- **Contratos:** nenhum muda nesta decisão.

## Execução
Marco M2 ([`roadmap/marcos/M2-monorepo.md`](../roadmap/marcos/M2-monorepo.md)).
