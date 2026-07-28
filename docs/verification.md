# verification.md — Cómo verificar que tu trabajo funciona

## Estado actual: solo build

Este repo **todavía no tiene proyecto de tests** (no hay ningún `.Tests.csproj` en la solución).
Por eso, hoy, "verde" significa **compila sin errores**, nada más. `feature_list.json` refleja esto
con `"rules.require_tests_to_close": false`.

Verificación estándar:

```powershell
./verify.ps1
```

Esto corre `dotnet restore` + `dotnet build LiquiLabs.Vankoo.Invoicing.sln`. Si termina con código de
salida distinto de 0, la sesión se considera **bloqueada** — no se puede declarar ninguna feature `done`.

## Cuando exista un proyecto de tests

En cuanto se cree `LiquiLabs.Vankoo.Invoicing.Tests` (xUnit recomendado, ya que es el estándar del
ecosistema .NET y se integra directo con `dotnet test`):

1. Descomentar el bloque `dotnet test` en `verify.ps1`.
2. Cambiar `"require_tests_to_close"` a `true` en `feature_list.json`.
3. Añadir el checkpoint correspondiente en `CHECKPOINTS.md`.

## Verificación extendida (manual, no automatizada por verify.ps1)

Algunas features (ej. contenerización, integración con Kafka/MinIO/MongoDB reales) no se pueden
verificar solo con `dotnet build`. Para esas, la verificación es manual:

```powershell
# Levantar el servicio en local
dotnet run --project LiquiLabs.Vankoo.Invoicing

# Health checks
curl http://localhost:8082/health/live
curl http://localhost:8082/health/ready
```

Para la feature de Docker (ver `feature_list.json` id 3), la verificación real requiere el repo
`vankoo/infrastructure`:

```bash
cd vankoo/infrastructure
docker compose up -d --build invoicing-service
curl http://localhost:8082/health/ready
```

Documenta el resultado de esta verificación manual en `progress.md` — `verify.ps1` no la cubre.

## Qué NO es verificación válida

- Que el código "se vea bien" sin compilar.
- Marcar `done` en `feature_list.json` sin haber corrido `verify.ps1` en esa misma sesión.
- Confiar en que Docker funciona sin haber levantado el compose de `vankoo/infrastructure` al menos una vez.
