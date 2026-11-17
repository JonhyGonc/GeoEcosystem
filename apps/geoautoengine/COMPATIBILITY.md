# Compatibilidade

- Civil 3D 2024 (R24.3, .NET Framework 4.8): build validado; smoke CAD pendente porque o produto não está instalado nesta máquina.
- Civil 3D 2025 (R25.0, .NET 8): build e smoke CAD validados.
- Civil 3D 2026 (R25.1, .NET 8): build e smoke CAD validados.
- Civil 3D 2027 (R26.0, .NET 10): host criado no M6.1 (17/11/2026); build e smoke CAD **pendentes** até o usuário instalar o Civil 3D 2027.

Compilar exige o SDK do .NET 10. O `Instalar GeoAutoEngine 3.cmd` instala no 2026 e no 2027 que encontrar. O instalador da equipe (`GeoAutoEngine3_Setup.exe`) ainda não tem o host 2027 (M6.3).

O Setup é único, detecta instalações Civil 3D compatíveis e instala um bundle Autodesk com hosts separados por release. Reexecutar o Setup realiza upgrade/repair. Assemblies Autodesk não são distribuídas.
