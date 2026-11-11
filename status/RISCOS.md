# Riscos

Origem: [Etapa 2](../auditorias/2026-11-etapa1/ETAPA_2_CONSOLIDACAO.md#7-riscos-priorizados), reavaliados com as respostas do usuário em 10/11/2026.

| # | Risco | Severidade | Estado | Observação |
|---|---|---|---|---|
| R-01 | Dados em repositórios públicos (GAE-15, GAD-18) | Baixa | Aceito | O usuário confirmou que **não são dados de cliente**. Pendência menor: caminhos de usuário Windows em `GeoAutoEngine/docs/evidence`. |
| R-02 | Perda silenciosa de alterações no GAD compartilhado (H-01, GAD-10) | Baixa | Defeito conhecido | A equipe não usa nem pretende usar a pasta compartilhada. Volta a ser alto se isso mudar. |
| R-03 | GADX em duas variantes + espera síncrona de 5 min no GAE (F-04, GAE-05) | **Alta** | Aberto | Na máquina do usuário as pontas são compatíveis, mas o Civil 3D provavelmente trava durante a revisão. Outras máquinas: desconhecido. |
| R-04 | Builds não rastreáveis e diferentes entre desenhistas (F-05, GAE-01, GAD-06) | **Alta** | Aberto | Instalações em épocas diferentes, versão fixa, sem commit registrado. |
| R-05 | Ponte DXF "descartável" em produção, com script privado (F-02, GM-09) | Média | Aberto | Depende de D-03. |
| R-06 | Esquemas sem controle de versão aplicada (GM-03, GAD-07) | Média | Aberto | |
| R-07 | Duplicação de projetos no GeoMap por `sourceSystem` (GM-05) | Média | Latente | Aparece quando o exportador do GAE for usado com dados reais. |
| R-08 | Nenhum teste cruzado entre repositórios (F-11) | Média | Aberto | Depende de D-06. |
| R-09 | Semântica divergente do payload de correções (GAD-14, H-03) | Média | Aberto | Em uso na máquina do usuário (GAE com 6/7 + GAD fix). |
