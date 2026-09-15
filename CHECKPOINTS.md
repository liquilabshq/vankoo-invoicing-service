# CHECKPOINTS.md — Criterios objetivos de "estado final correcto"

> Usado por el subagente `reviewer` (`.claude/agents/reviewer.md`) para aprobar o rechazar trabajo.
> Cada checkpoint debe poder verificarse mirando el repo, no "a ojo".

- **C1 — Build verde.** `./verify.ps1` termina con código de salida 0.
- **C2 — Dependencias de capas respetadas.** `Domain/` y `Application/` no contienen ningún `using LiquiLabs.Vankoo.Invoicing.Infrastructure`. Verificable con:
  ```
  grep -r "using LiquiLabs.Vankoo.Invoicing.Infrastructure" LiquiLabs.Vankoo.Invoicing/Domain LiquiLabs.Vankoo.Invoicing/Application
  ```
  No debe haber resultados.
- **C3 — Controladores delgados.** Ningún método de `Interfaces/Rest/Controllers/*.cs` contiene lógica de negocio: solo arma el Command/Query y llama `mediator.Send(...)`.
- **C4 — Convención de nombres respetada.** Si la feature agrega un caso de uso nuevo, sigue el patrón `[Acción][Recurso]Command/Query` + `Handler` + `Validator` descrito en `docs/CONVENTIONS.md`.
- **C5 — Un solo estado de trabajo.** En `feature_list.json`, a lo sumo una feature tiene `status: "in_progress"` al terminar la sesión.
- **C6 — Progreso documentado.** `progress.md` describe qué se hizo en la sesión (no queda vacío con cambios de código pendientes de explicar).
- **C7 — Tests (cuando aplique).** Si `feature_list.json.rules.require_tests_to_close` es `true` y la feature lo requiere, existen tests para los criterios de `acceptance` y `verify.ps1` los corre en verde. Hoy esta regla está desactivada (ver `docs/verification.md`) — no bloquea el veredicto.
- **C8 — Sin secretos ni hosts hardcodeados.** No hay connection strings, API keys, ni hostnames de Docker (`invoicing-db-mongo`, `kafka-broker`, `minio`) escritos directamente en código; todo pasa por `IOptions<T>` y `appsettings.*`.

## Formato de veredicto

El `reviewer` marca cada checkpoint `[x]` o `[ ]` y, si algo falla, cita archivo y línea concreta —
nunca feedback genérico como "revisar convenciones".
