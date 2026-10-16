# Revestik Projects B04 E — VERIFY

## Alcance
- Corrige `ProjectDocumentEventLink.ProjectId` al insertar un vínculo; antes quedaba 0, incompatible con las FK compuestas.
- Agrega 4 pruebas HTTP focalizadas del ciclo completo de vínculos y sus protecciones.
- No modifica Program.cs, migraciones, reglas de versiones, clientes Blazor, ventas, inventario ni contabilidad.

## Flujo obligatorio
1. Aplicar ZIP sobre la copia local con B04 A+B+C+D.
2. `dotnet build Revestik.sln`
3. `dotnet test tests/Revestik.Api.Tests/Revestik.Api.Tests.csproj --filter "FullyQualifiedName~ProjectDocument" --logger "console;verbosity=minimal"`
4. `dotnet test tests/Revestik.Api.Tests/Revestik.Api.Tests.csproj --filter "FullyQualifiedName~ProjectFileReconciliation" --logger "console;verbosity=minimal"`
5. Si 3 y 4 pasan: `dotnet test Revestik.sln --logger "console;verbosity=minimal"`
6. `git diff --check` y `git status --short`.

## Criterios de cierre
- 9/9 B04 C+D HTTP ya verificados en la copia local anterior.
- 4 nuevas pruebas HTTP de E (vínculo real, idempotencia, desvínculo real, CSRF/roles, evento extranjero y anulado).
- 2 pruebas SQL finales de ciclo de vida deben comprobarse en la regresión focalizada.
- La ejecución de pruebas focalizadas y suite completa debe verificarse en Windows local; este ZIP no afirma resultados de tests aún no ejecutados.
- Reconciliación física manual, sin cargas simultáneas; NO habilitar en producción antes de contar con coordinación excluyente y endurecimiento adicional.
- No ejecutar `dotnet ef database update`, Git commit o Git push automáticamente.
