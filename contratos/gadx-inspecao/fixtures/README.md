# Fixtures de referência do GADX 1.1 (M3.2)

Exemplos de cada mensagem do protocolo, usados pelos testes do GeoAutoEngine e do GeoAutoDocs. As duas aplicações leem **os mesmos arquivos**: se um lado deixar de entender uma mensagem, a CI dele falha. Especificação: [`../SPEC.md`](../SPEC.md).

| Pasta / arquivo | Conteúdo |
|---|---|
| `json/<nome>.json` | payload da mensagem (JSON UTF-8 sem BOM, LF) |
| `frames/<nome>.bin` | frame completo: cabeçalho de 16 bytes (§3) + o mesmo payload, byte a byte |
| `manifest.json` | lista das fixtures, com tipo de mensagem e `requestId`; os testes percorrem esta lista |
| `.gitattributes` | `-text`: o Git nunca converte as quebras de linha (no Windows isso mudaria os bytes) |

Todos os dados são **fictícios** (GUIDs `0e0c0000-…`).

## Fixtures
| Nome | Tipo | Para que serve |
|---|---|---|
| `hello-1.0` | 1 | cliente 1.0 (GAD 1.1.0), sem `capabilities` |
| `hello-1.1` | 1 | cliente 1.1 com `capabilities: ["corrections"]` |
| `hello-1.1-sem-correcoes` | 1 | cliente 1.1 com `capabilities: []` (regra S1) |
| `hello-ack-1.0` | 2 | servidor 1.0, sem `capabilities` |
| `hello-ack-1.1` | 2 | servidor 1.1 com `capabilities: ["corrections"]` |
| `inspection-push` | 3 | documento v1 com ocorrências `Point` (com anomalia e evidência), `Range`, `MultiPoint` e `EntireSection` |
| `inspection-ack-success` | 4 | inspeção aceita |
| `error-incompatible-version` | 5 | handshake recusado |
| `correction-push` | 6 | correções em estado completo para `Point`, `Range` e `MultiPoint`; o `MultiPoint` traz 2 pontos para uma ocorrência com 3 (inválido de propósito) |
| `correction-ack-partial` | 7 | resposta `Partial`: 2 aplicadas, 1 `Invalid` |

## O que os testes verificam (nos dois lados)
1. Ler `frames/<nome>.bin` com o leitor do protocolo devolve a versão `1`, o tipo e o `requestId` do manifesto, e um payload idêntico a `json/<nome>.json`.
2. Escrever o frame com o escritor do protocolo produz exatamente os bytes de `frames/<nome>.bin`.
3. Cada payload é lido pelos tipos da aplicação. Isso inclui os `Hello`/`HelloAck` 1.1: o código atual ignora `capabilities`, o que prova a compatibilidade da §7.
4. O documento passa pela validação do `InspectionExchangeSerializer` de cada lado.
5. As correções chegam ao GAD com todos os campos presentes (estado completo, §9.2).

Testes: `apps/geoautoengine/tests/GeoAutoEngine.Tests/GadxContractFixtureTests.cs` e `apps/geoautodocs/tests/GeoAutoDocs.Tests/GadxContractFixtureTests.cs`.

## Como mudar
Nunca edite os arquivos à mão. Altere o gerador e rode, a partir da raiz do monorepo:

```
python contratos/gadx-inspecao/ferramentas/gerar_fixtures.py
```

O gerador é determinístico. Uma mudança de fixture é uma mudança de contrato: Issue `mudanca-de-contrato` e as CIs das duas aplicações verdes.
