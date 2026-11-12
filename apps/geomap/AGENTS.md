# Instruções permanentes para agentes

Estas regras valem para qualquer IA ou pessoa que altere este repositório.

## Antes de alterar código

1. Leia docs/PRODUCT.md.
2. Leia docs/MVP.md.
3. Leia docs/ARCHITECTURE.md.
4. Leia docs/DECISIONS.md.
5. Leia docs/PERFORMANCE.md quando trabalhar com dados, importação, banco, API ou mapas.
6. Leia a Issue atual por completo.
7. Inspecione a implementação e os testes existentes.
8. Confirme que a tarefa cabe integralmente na Issue.

Se uma informação essencial estiver ausente ou duas regras entrarem em conflito, registre a dúvida e pare antes de tomar uma decisão difícil de reverter.

## Regras de escopo

- Trabalhe em uma única Issue por vez.
- Não implemente funcionalidades fora da Issue atual.
- Não faça refatorações grandes ou não relacionadas.
- Não crie abstrações sem necessidade concreta.
- Não remova comportamento existente sem justificativa.
- Não transforme o GeoMap em um CAD web.
- Não inicie o exportador do Civil 3D antes de o fluxo PostGIS → API → MVT → MapLibre estar comprovado.
- Se encontrar uma melhoria fora do escopo, registre-a como sugestão ou futura Issue. Não a implemente automaticamente.

## Regras de arquitetura

- Não altere as tecnologias aprovadas sem necessidade concreta e registro em docs/DECISIONS.md.
- Preserve o monólito modular simples para o MVP.
- Não introduza microserviços, Kubernetes, mensageria, CQRS, Event Sourcing, Redis, GraphQL ou múltiplos bancos sem decisão aprovada.
- O banco e o frontend trabalham com o modelo normalizado GeoMap, nunca diretamente com tipos internos da Autodesk.
- A geometria nasce no CAD; o navegador apenas visualiza e consulta.
- A fonte do mapa base deve permanecer configurável e desacoplada.
- Endpoints de mapa devem buscar dados pela região e pelo zoom. Nunca envie uma rede grande inteira ao navegador por padrão.
- Consultas espaciais devem considerar índices PostGIS e ser verificadas com EXPLAIN quando aplicável.
- Importações grandes devem favorecer lotes ou streaming e limitar o uso de memória.

## Qualidade e verificação

Antes de considerar a tarefa concluída:

1. Execute o build do backend.
2. Execute o build do frontend.
3. Execute os testes existentes.
4. Adicione ou atualize testes relacionados à alteração.
5. Use o dataset LARGE quando a Issue alterar mapa, consultas, tiles ou importação e isso for aplicável.
6. Revise o diff e remova mudanças acidentais.
7. Atualize a documentação quando contratos, arquitetura ou decisões mudarem.
8. Confirme todos os critérios de aceitação da Issue.

Se algum comando não existir ainda, registre “não aplicável — projeto ainda não inicializado”, sem fingir sucesso.

## GitHub

- Prefira branches vinculadas à Issue, como feat/002-postgis.
- Mantenha a main funcional.
- Faça commits pequenos, coerentes e com mensagem clara.
- O Pull Request deve referenciar a Issue e informar build, testes, impacto em performance, decisões e pendências.
- Não misture várias Issues grandes no mesmo Pull Request.

## Relatório de conclusão

Informe:

- Issue;
- resultado;
- build: PASS, FAIL ou não aplicável;
- testes: quantidade aprovada ou não aplicável;
- performance;
- arquivos criados e alterados;
- arquitetura alterada: sim ou não;
- novas decisões;
- pendências.
