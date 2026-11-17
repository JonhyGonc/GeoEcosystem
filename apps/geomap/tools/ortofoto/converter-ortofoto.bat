@echo off
setlocal
rem ============================================================================
rem  Converte uma ortofoto para o GeoMap (DEC-035): MBTiles com PNG transparente.
rem  Uso: arraste o arquivo da ortofoto (TIF, ECW ou JP2) para cima deste arquivo.
rem  Usa o GDAL que vem com o QGIS (gratuito). A transparencia vem da banda 4
rem  (alfa) do arquivo, sem passar pelo desenho do QGIS.
rem ============================================================================

if "%~1"=="" (
  echo Arraste o arquivo da ortofoto para cima deste arquivo .bat.
  pause
  exit /b 1
)
set "ENTRADA=%~1"

set "QGISROOT="
for /d %%D in ("%ProgramFiles%\QGIS*") do if exist "%%D\bin\o4w_env.bat" set "QGISROOT=%%D"
if not defined QGISROOT (
  echo Nao encontrei o QGIS em "%ProgramFiles%". Instale o QGIS e tente de novo.
  pause
  exit /b 1
)
call "%QGISROOT%\bin\o4w_env.bat"

rem Caminhos com acento (por exemplo, C:\Users\Joao com til) no padrao do Windows.
set GDAL_FILENAME_IS_UTF8=NO

set "PASTA=C:\GeoMap\ortofotos"
if not exist "%PASTA%" mkdir "%PASTA%"

echo.
echo Ortofoto: %ENTRADA%
set "PROJETO="
set /p "PROJETO=Nome do projeto, igual ao do site (ex.: GF-CV-01-MAP-1723-548.26-001): "
if not defined PROJETO (
  echo Nenhum nome digitado.
  pause
  exit /b 1
)
set "SAIDA=%PASTA%\%PROJETO%.mbtiles"

if exist "%SAIDA%" (
  del "%SAIDA%" 2>nul
  if exist "%SAIDA%" (
    echo O arquivo antigo esta em uso. Pare a API do GeoMap: docker compose stop api
    pause
    exit /b 1
  )
)

rem Com 4 bandas, a banda 4 e a transparencia (alfa).
set "BANDAS=-b 1 -b 2 -b 3"
gdalinfo "%ENTRADA%" | find "Band 4" >nul && set "BANDAS=-b 1 -b 2 -b 3 -b 4 -colorinterp red,green,blue,alpha"

echo.
echo [1/2] Gerando as imagens no zoom maximo (pode demorar)...
gdal_translate -of MBTILES -co TILE_FORMAT=PNG %BANDAS% "%ENTRADA%" "%SAIDA%"
if errorlevel 1 goto erro

echo.
echo [2/2] Gerando os zooms menores...
gdaladdo -r average "%SAIDA%" 2 4 8 16 32 64 128 256
if errorlevel 1 goto erro

echo.
echo Pronto: %SAIDA%
echo Agora rode "docker compose start api" (ou "restart api") e abra o site numa janela anonima.
pause
exit /b 0

:erro
echo.
echo A conversao falhou. Mande um print desta janela.
pause
exit /b 1
