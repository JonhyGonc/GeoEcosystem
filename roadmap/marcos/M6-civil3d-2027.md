# M6 — GeoAutoEngine no Civil 3D 2027

- **Estado:** M6.1 em revisão (PR #33).
- **Origem:** pedido do usuário em 17/11/2026: a empresa vai migrar para o Civil 3D 2027.
- **Dono:** agente único (Emenda 2); compilação, teste no Civil 3D 2027, merge e instalação: usuário.
- **Objetivo:** o mesmo GeoAutoEngine funciona no Civil 3D 2026 e no 2027, com um instalador só, durante toda a migração da equipe.
- **Ordem:** vem **antes do M5** (decisão do usuário, 17/11/2026).

## Por que é preciso
- O Civil 3D 2027 (série R26.0) só carrega plugins **.NET 10**, compilados contra as DLLs do próprio 2027. O 2025 e o 2026 usam .NET 8.
- O GAE usa só as DLLs básicas do AutoCAD (`AcCoreMgd`, `AcDbMgd`, `AcMgd`, `AcWindows`, `AdWindows`), não as do Civil 3D. O teste `NativeFoundation_ContainsNoCivilSpecificReference` garante isso.

## Passos
| Passo | Entrega | Verificação | Estado |
|---|---|---|---|
| M6.1 | Host 2027: `AutodeskAutoCADVersion=2027` → `net10.0-windows`; `PackageContents.xml` com R26.0 em `Contents/2027`; `Instalar GeoAutoEngine 3.cmd` instala em todo Civil 3D 2026 e 2027 encontrado; SDK do .NET 10 para compilar (o host 2026 continua .NET 8) | CI verde (testes no SDK 10); roteiro no Civil 3D 2026 (nada mudou) e no 2027 | 🔄 |
| M6.2 | GAD e biblioteca do contrato no .NET 10 (o .NET 8 perdeu o suporte em 10/11/2026) | CI da biblioteca e do GAD; roteiro de correções GAE ↔ GAD | ⏳ depois do M6.1 (decisão do usuário) |
| M6.3 | Instalador da equipe (`build-release.ps1`, `GeoAutoEngine3_Setup.exe`) com o host 2027 | Setup instala no 2026 e no 2027 | ⏳ junto com a instalação na equipe (fim do projeto) |

## Decisões tomadas neste marco
| Data | Decisão | Quem |
|---|---|---|
| 17/11/2026 | 2026 e 2027 juntos, num instalador só, durante a migração | Usuário |
| 17/11/2026 | Fazer agora, antes do M5 | Usuário |
| 17/11/2026 | GAD e biblioteca no .NET 10 depois, num PR separado | Usuário |

## Pontos de atenção
- A CI não compila o plugin (exige as DLLs da Autodesk). O host 2027 só é compilado de verdade na máquina do usuário, depois de instalar o Civil 3D 2027 e o SDK do .NET 10.
- O compilador agora é o SDK do .NET 10 também para o host 2026. Para não mudar nada nele, o projeto fixa C# 12, as regras de análise do .NET 8 e a auditoria NuGet só dos pacotes diretos (iguais ao SDK 8).
- Instalar o Civil 3D 2027 com o **Update 1**: a versão inicial travava ao arrastar itens de paletas personalizadas para o desenho.
- O `cadVersion` do aperto de mão do GADX passa a ser `"2027"` no host novo. É texto livre no contrato (`SPEC.md`), então não muda o GADX.
