# Agentes

> **Desde 16/11/2026 vale a [Emenda 2](PROTOCOLO.md#emenda-2--agente-único-decisão-do-usuário-em-16112026): um único agente.** A tabela antiga (Chats 00–03) fica abaixo como histórico.

| Agente | Papel | Escreve em |
|---|---|---|
| **Claude** (chat no Project "GeoEcosystem") | Coordena e implementa em todo o repositório | `JonhyGonc/GeoEcosystem`, por PR |
| **ChatGPT** (só quando os créditos do Claude acabarem) | O mesmo papel, com o mesmo prompt | Idem |
| **Usuário** | Requisitos de negócio, aprovação do roadmap, merges, tags, Releases, instalação nas máquinas | Todos |

Prompt para iniciar qualquer sessão: [`PROMPT_AGENTE_UNICO.md`](PROMPT_AGENTE_UNICO.md). Estado para retomar: [`status/ESTADO_ATUAL.md`](../status/ESTADO_ATUAL.md).

<details><summary>Histórico: quatro agentes (10–13/11/2026)</summary>

| Agente | Escopo |
|---|---|
| Chat 00 — coordenador | Prioridades, contratos, decisões, roadmap |
| Chat 01 — GeoMap | `apps/geomap` |
| Chat 02 — GeoAutoEngine | `apps/geoautoengine` |
| Chat 03 — GeoAutoDocs | `apps/geoautodocs` |

</details>

## Regras de comunicação
- Os chats não se comunicam automaticamente: o usuário encaminha mensagens, e Issues, PRs e arquivos deste repositório são a trilha oficial.
- Toda entrega usa o relatório padrão de 10 itens ([`PROTOCOLO.md`](PROTOCOLO.md#relatório-padrão-obrigatório-para-entregas-e-auditorias)).
- Mudança que afeta outro projeto (contrato, formato, identificador, protocolo) é proposta ao Chat 00 antes de ser executada.

## Prompts vigentes
- Protocolo comum: [`PROTOCOLO.md`](PROTOCOLO.md).
- Os prompts individuais dos chats ficam no Project "GeoEcosystem" do Claude. Este repositório guarda a versão oficial do protocolo.

## Limitações conhecidas do ambiente (10/11/2026)
- Todos os agentes usam a conta GitHub `JonhyGonc`; por isso as aprovações seguem [`PROCESSO_PR.md`](PROCESSO_PR.md).
- As sessões não têm .NET SDK nem Civil 3D: build e testes .NET dependem da CI ou da máquina do usuário.
- A política das sessões negou acesso de escrita/API a `GeoAutoEngine` e `GeoAutoDocs`; os Chats 02/03 podem precisar de autorização explícita do usuário para abrir PRs.
