# Frontend GeoMap

Aplicação React com TypeScript e MapLibre GL JS para o visualizador geográfico do GeoMap.

## Pré-requisitos

- Node.js 24;
- npm 11 ou compatível.

## Executar localmente

~~~bash
cd apps/web
cp .env.example .env
npm ci
npm run dev
~~~

Abra http://localhost:5173. A configuração padrão usa o mapa raster OpenStreetMap Standard, não exige chave de acesso e exibe a atribuição no próprio mapa.

## Configurar o mapa base

Todas as opções da fonte ficam centralizadas nas variáveis abaixo:

| Variável | Padrão | Função |
|---|---|---|
| `VITE_BASE_MAP_NAME` | `OpenStreetMap Standard` | nome apresentado na interface |
| `VITE_BASE_MAP_TILE_URL` | URL pública do OpenStreetMap | template XYZ com `{z}`, `{x}` e `{y}` |
| `VITE_BASE_MAP_TILE_SIZE` | `256` | tamanho do tile, aceitando 256 ou 512 |
| `VITE_BASE_MAP_MIN_ZOOM` | `0` | menor zoom disponível na fonte |
| `VITE_BASE_MAP_MAX_ZOOM` | `19` | maior zoom nativo disponível na fonte |
| `VITE_BASE_MAP_ATTRIBUTION_TEXT` | `© OpenStreetMap contributors` | crédito visível obrigatório |
| `VITE_BASE_MAP_ATTRIBUTION_URL` | página de copyright do OSM | destino do crédito |

Para trocar de provedor, copie `.env.example` para `.env` e altere os valores. Uma URL personalizada também exige texto e URL de atribuição próprios. A aplicação rejeita configuração sem os marcadores XYZ, sem atribuição, com zoom inválido ou com HTTP fora do ambiente local.

Somente raster XYZ está implementado nesta etapa. A estrutura reserva IDs exclusivos `basemap-*` para trocar a fonte sem remover futuras camadas `geomap-*`. Adaptadores específicos para WMS, WMTS, ortofoto própria e COG poderão ser acrescentados depois, conforme necessidade e licença da fonte.

## Termos e limites da fonte padrão

O OpenStreetMap Standard é adequado ao protótipo interno de baixo tráfego, mas seu servidor público opera sem SLA e pode bloquear uso que prejudique o serviço. A aplicação deve:

- manter a atribuição visível e legível;
- usar a URL oficial `https://tile.openstreetmap.org/{z}/{x}/{y}.png`;
- respeitar os cabeçalhos de cache enviados pelo servidor;
- não fazer download em massa, varredura, prefetch ou uso offline dos tiles.

Antes de uso público ou de alto tráfego, deve-se contratar um provedor compatível ou hospedar uma fonte própria. Alterar a fonte não autoriza remover os créditos ou ignorar os termos do novo provedor.

## Configurar as camadas de rede

O frontend consulta as redes pertencentes ao projeto e cria uma camada MVT para cada uma. Preencha o projeto no arquivo `.env`; a rede inicial é opcional:

~~~dotenv
VITE_GEOMAP_API_BASE_URL=/api
VITE_GEOMAP_PROJECT_ID=UUID_DO_PROJETO
VITE_GEOMAP_NETWORK_ID=UUID_DA_REDE_INICIAL_OPCIONAL
~~~

No desenvolvimento, `/api` é encaminhado para `http://localhost:8080`. Em produção, o proxy reverso deverá publicar frontend e API na mesma origem ou `VITE_GEOMAP_API_BASE_URL` deverá apontar para a URL HTTPS da API.

Depois de aplicar a migration e carregar um dataset, os identificadores disponíveis podem ser consultados com:

~~~sql
SELECT p.id AS project_id, n.id AS network_id, p.name AS project, n.name AS network
FROM projects p
JOIN networks n ON n.project_id = p.id
ORDER BY p.created_at DESC, n.created_at DESC;
~~~

Sem o identificador do projeto, o mapa base abre e informa que o projeto não foi configurado. Com o projeto definido, o controle lista somente suas redes. Se uma rede inicial válida for informada, ela começa visível e as demais ocultas; sem essa preferência, todas começam visíveis.

Cada rede pode ser ligada ou desligada, e os comandos `Mostrar todas` e `Ocultar todas` ajudam em projetos com várias camadas. A troca de visibilidade altera apenas o estilo do MapLibre, preservando as fontes e o estado durante zoom e pan. O mapa solicita somente os tiles visíveis entre os zooms 14 e 22, diferencia carregamento, região vazia e falha e nunca carrega uma rede inteira.

## Selecionar um elemento

Clique sobre uma linha visível para destacá-la e mostrar seu identificador, rede e tipo. Em uma sobreposição, vence a feição visualmente superior retornada pelo MapLibre. Outro clique troca a seleção; clicar fora ou usar `Limpar seleção` remove o destaque. Ocultar a rede selecionada, trocar o projeto ou recarregar o estilo também limpa a seleção.

A seleção consulta somente as feições já renderizadas no ponto clicado e reutiliza a fonte MVT para o destaque. Em seguida, o painel busca apenas a feição escolhida e apresenta metadados de origem e as propriedades disponíveis. A interface trata carregamento, ausência, propriedades vazias e falha com opção de nova tentativa.

Trocar ou limpar a seleção cancela a consulta anterior. Zoom e pan não editam a geometria nem limpam a seleção. O painel é somente leitura; edição de atributos e ferramentas de desenho continuam fora desta etapa.

## Verificações

~~~bash
npm run lint
npm run test
npm run build
~~~

O frontend oferece visualização, camadas, seleção, zoom e navegação. Não há ferramentas de desenho ou edição.
