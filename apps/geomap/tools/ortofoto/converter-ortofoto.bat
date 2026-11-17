@echo off
setlocal EnableExtensions
rem ============================================================================
rem  GERADOR DE MBTILES - ortofoto para o GeoMap (DEC-035)
rem  De dois cliques (abre uma janela para escolher o arquivo) ou arraste a
rem  ortofoto (TIF, ECW ou JP2) para cima deste arquivo.
rem  Usa o GDAL que vem com o QGIS. A transparencia vem da banda 4 (alfa).
rem ============================================================================

rem Reabre numa janela que nao fecha sozinha, mesmo se der erro.
if not "%~2"=="__janela" (
  start "Gerador de MBTiles - GeoMap" cmd /k ""%~f0" "%~1" __janela"
  exit /b
)

for /f %%a in ('echo prompt $E^| cmd') do set "ESC=%%a"
set "OK=%ESC%[92m[OK]%ESC%[0m"
set "AVISO=%ESC%[93m[AVISO]%ESC%[0m"
set "ERRO=%ESC%[91m[ERRO]%ESC%[0m"
set "PASTA=C:\GeoMap\ortofotos"

cls
echo %ESC%[96m==============================================================%ESC%[0m
echo %ESC%[96m   GERADOR DE MBTILES  -  ortofoto para o GeoMap%ESC%[0m
echo %ESC%[96m==============================================================%ESC%[0m
echo   Destino: %PASTA%
echo.

rem ---------------------------------------------------------------- 1
echo %ESC%[1m[1/7] Procurando o QGIS...%ESC%[0m
set "QGISROOT="
for /d %%D in ("%ProgramFiles%\QGIS*") do if exist "%%D\bin\o4w_env.bat" set "QGISROOT=%%D"
if not defined QGISROOT (
  set "MOTIVO=Nao encontrei o QGIS na pasta Arquivos de Programas. Instale o QGIS e tente de novo."
  goto falha
)
rem O ambiente do QGIS troca o PATH; o Docker e o PowerShell continuam no fim dele.
set "PATHORIGINAL=%PATH%"
call "%QGISROOT%\bin\o4w_env.bat"
@echo off
set "PATH=%PATH%;%PATHORIGINAL%"
where gdal_translate >nul 2>nul
if errorlevel 1 (
  set "MOTIVO=Achei o QGIS, mas nao o GDAL dentro dele."
  goto falha
)
set GDAL_FILENAME_IS_UTF8=NO
echo       %OK% %QGISROOT%
echo.

rem ---------------------------------------------------------------- 2
echo %ESC%[1m[2/7] Escolhendo a ortofoto...%ESC%[0m
set "ENTRADA=%~1"
if defined ENTRADA goto temarquivo
echo       Abrindo a janela para escolher o arquivo...
for /f "usebackq delims=" %%F in (`powershell -NoProfile -STA -Command "Add-Type -AssemblyName System.Windows.Forms; $d = New-Object System.Windows.Forms.OpenFileDialog; $d.Title = 'Escolha a ortofoto (TIF, ECW ou JP2)'; $d.Filter = 'Ortofoto|*.tif;*.tiff;*.ecw;*.jp2|Todos os arquivos|*.*'; if ($d.ShowDialog() -eq 'OK') { $d.FileName }"`) do set "ENTRADA=%%F"
if not defined ENTRADA (
  set "MOTIVO=Nenhum arquivo foi escolhido."
  goto falha
)
:temarquivo
if not exist "%ENTRADA%" (
  set "MOTIVO=Arquivo nao encontrado."
  goto falha
)
set "GB=?"
for /f %%S in ('powershell -NoProfile -Command "[math]::Round((Get-Item -LiteralPath $env:ENTRADA).Length / 1GB, 2)"') do set "GB=%%S"
echo       %OK% %ENTRADA%  (%GB% GB)
echo.

rem ---------------------------------------------------------------- 3
echo %ESC%[1m[3/7] Lendo a ortofoto...%ESC%[0m
set "INFO=%TEMP%\geomap-ortofoto-info.txt"
gdalinfo "%ENTRADA%" > "%INFO%" 2>&1
if errorlevel 1 (
  set "MOTIVO=O GDAL nao conseguiu abrir a ortofoto. Confira se o QGIS abre esse arquivo."
  goto falha
)
set "BANDAS=-b 1 -b 2 -b 3"
find "Band 4" "%INFO%" >nul
if errorlevel 1 goto tresbandas
set "BANDAS=-b 1 -b 2 -b 3 -b 4 -colorinterp red,green,blue,alpha"
echo       %OK% 4 bandas: a banda 4 vira transparencia, sem borda branca.
goto lida
:tresbandas
echo       %AVISO% So 3 bandas, sem transparencia: o fundo do arquivo vai aparecer em volta da foto.
:lida
echo.

rem ---------------------------------------------------------------- 4
echo %ESC%[1m[4/7] Nome do projeto%ESC%[0m
echo       Digite o nome do projeto EXATAMENTE como aparece no site.
echo       Exemplo: GF-CV-01-MAP-1723-548.26-001
set "PROJETO="
set /p "PROJETO=      Nome: "
if not defined PROJETO (
  set "MOTIVO=Nenhum nome foi digitado."
  goto falha
)
if not exist "%PASTA%" mkdir "%PASTA%"
set "SAIDA=%PASTA%\%PROJETO%.mbtiles"
echo       %OK% Vai gerar: %SAIDA%
echo.

rem ---------------------------------------------------------------- 5
echo %ESC%[1m[5/7] Parando a API do GeoMap para trocar o arquivo...%ESC%[0m
set "GEOMAP="
if exist "%~dp0..\..\docker-compose.yml" set "GEOMAP=%~dp0..\.."
if not defined GEOMAP if exist "%USERPROFILE%\Desktop\GeoEcosystem\apps\geomap\docker-compose.yml" set "GEOMAP=%USERPROFILE%\Desktop\GeoEcosystem\apps\geomap"
if not defined GEOMAP goto semgeomap
pushd "%GEOMAP%"
docker compose stop api >nul 2>nul
if errorlevel 1 (
  echo       %AVISO% Nao consegui parar a API. O Docker Desktop esta aberto?
) else (
  echo       %OK% API parada.
)
popd
goto apagar
:semgeomap
echo       %AVISO% Nao achei a pasta do GeoMap. Pare a API voce mesmo: docker compose stop api
:apagar
if exist "%SAIDA%" del "%SAIDA%" >nul 2>nul
if exist "%SAIDA%" (
  set "MOTIVO=O arquivo antigo esta em uso. Feche o QGIS ou o Global Mapper, se ele estiver aberto la, e tente de novo."
  goto falha
)
echo.

rem ---------------------------------------------------------------- 6
echo %ESC%[1m[6/7] Gerando as imagens no zoom maximo...%ESC%[0m
echo       Pode levar varios minutos. O progresso aparece abaixo: 0...10...20... ate 100.
echo       Inicio: %time:~0,8%
gdal_translate -of MBTILES -co TILE_FORMAT=PNG %BANDAS% "%ENTRADA%" "%SAIDA%"
if errorlevel 1 (
  set "MOTIVO=A geracao das imagens falhou. Veja a mensagem acima."
  goto falha
)
echo       %OK% Imagens geradas. Fim: %time:~0,8%
echo.

rem ---------------------------------------------------------------- 7
echo %ESC%[1m[7/7] Gerando os zooms menores, para a foto aparecer de longe...%ESC%[0m
gdaladdo -r average "%SAIDA%" 2 4 8 16 32 64 128 256
if errorlevel 1 (
  set "MOTIVO=A geracao dos zooms menores falhou. Veja a mensagem acima."
  goto falha
)
echo       %OK% Zooms gerados. Fim: %time:~0,8%
echo.

if not defined GEOMAP goto pronto
pushd "%GEOMAP%"
docker compose start api >nul 2>nul
if errorlevel 1 (
  echo %AVISO% Nao consegui ligar a API. Rode: docker compose start api
) else (
  echo %OK% API ligada de novo.
)
popd

:pronto
set "GB=?"
for /f %%S in ('powershell -NoProfile -Command "[math]::Round((Get-Item -LiteralPath $env:SAIDA).Length / 1GB, 2)"') do set "GB=%%S"
echo.
echo %ESC%[92m==============================================================%ESC%[0m
echo %ESC%[92m   TUDO PRONTO%ESC%[0m
echo %ESC%[92m==============================================================%ESC%[0m
echo   Arquivo: %SAIDA%  (%GB% GB)
echo   Abra o site (npm run dev rodando) e escolha o projeto %PROJETO%.
echo.
choice /c SN /n /m "  Abrir o site agora? [S/N] "
if errorlevel 2 goto fim
start "" "http://localhost:5173"
:fim
echo.
echo   Pode fechar esta janela.
exit /b 0

:falha
echo.
echo %ESC%[91m==============================================================%ESC%[0m
echo   %ERRO% %MOTIVO%
echo %ESC%[91m==============================================================%ESC%[0m
echo   Nada foi apagado alem do arquivo antigo da ortofoto deste projeto.
echo   Se precisar de ajuda, mande um print desta janela.
exit /b 1
