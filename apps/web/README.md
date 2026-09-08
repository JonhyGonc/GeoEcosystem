# Frontend GeoMap

Aplicação React com TypeScript e MapLibre GL JS para o visualizador geográfico do GeoMap.

## Pré-requisitos

- Node.js 24;
- npm 11 ou compatível.

## Executar localmente

~~~bash
cd apps/web
npm ci
npm run dev
~~~

Abra http://localhost:5173. O mapa inicial usa temporariamente o estilo público de demonstração do MapLibre e não exige chave de acesso. A fonte configurável e sua política de atribuição serão isoladas na Issue #9.

## Verificações

~~~bash
npm run lint
npm run test
npm run build
~~~

O frontend desta etapa oferece apenas visualização, zoom e navegação. Não há ferramentas de desenho ou edição.
