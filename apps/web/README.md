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

## Verificações

~~~bash
npm run lint
npm run test
npm run build
~~~

O frontend oferece apenas visualização, zoom e navegação. Não há ferramentas de desenho ou edição.
