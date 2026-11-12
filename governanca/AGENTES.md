# Agentes

| Agente | Escopo | Escreve em | Não faz |
|---|---|---|---|
| **Chat 00** — coordenador | Prioridades, dependências, contratos, decisões entre projetos, roadmap | `GeoEcosystem` (por PR) | Implementar funcionalidades nos três aplicativos |
| **Chat 01** — GeoMap | `JonhyGonc/Geomap` | `Geomap` (respeitando o `AGENTS.md` dele) | Alterar outros repositórios sem autorização do Chat 00 |
| **Chat 02** — GeoAutoEngine | `JonhyGonc/GeoAutoEngine` | `GeoAutoEngine` | Idem |
| **Chat 03** — GeoAutoDocs | `JonhyGonc/GeoAutoDocs` | `GeoAutoDocs` | Idem |
| **Usuário** | Requisitos de negócio, aprovação do roadmap, merges, operações em produção | Todos | — |

> **A partir do M2.3 (monorepo):** cada especialista escreve só em `apps/<sua aplicação>/` do `GeoEcosystem` ([Emenda 1](PROTOCOLO.md#emendas)). Os repositórios antigos ficam arquivados.

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
