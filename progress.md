# progress.md — Estado de la sesión actual

> Se reinicia al cerrar cada sesión: el resumen se mueve a `history.md` (ver AGENTS.md §5).
> Si este archivo tiene contenido al empezar, léelo primero — describe dónde quedó la última sesión.

## Feature en curso

- #5 — Permitir AWS S3 como proveedor de almacenamiento de facturas.

## Plan

- Inspeccionar el puerto de almacenamiento y el adaptador MinIO actual. Completado.
- Determinar los cambios mínimos para conmutar entre MinIO y AWS S3. Completado.
- Evaluar el límite de reutilización para Azure Blob Storage y documentar la decisión. Completado.
- Acordar las decisiones de despliegue/credenciales antes de implementar. Completado.

## Notas de la sesión

- La feature se prioriza explícitamente por indicación del usuario, aunque #3 siga pendiente.
- Decisiones acordadas: MinIO en desarrollo, AWS S3 en producción con access keys configuradas por
  entorno, y bucket AWS aprovisionado por IaC. Azure Blob Storage queda fuera de alcance.
- Implementado `S3StorageService`, configuración tipada por proveedor y health check neutral. MinIO
  conserva el autocreado de bucket; AWS S3 lo desactiva y usa región configurada.
- Validación estática: JSON válido, no hay referencias de Application/Domain a Infrastructure y no
  quedan referencias al adapter o health check exclusivos de MinIO.

## Bloqueos

- No se ejecutó `verify.ps1` por indicación explícita del usuario; el entorno tampoco tiene SDK de
  .NET disponible. Falta una comprobación real contra un bucket S3 preaprovisionado.
