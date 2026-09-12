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

- Integrado `origin/develop` en `refactor/ocr-implementation-improvements` respetando el nuevo
  proveedor `S3StorageService`, la selección MinIO/AWS, los health checks y el worker OCR.
- Adaptado el OCR mejorado para leer el documento mediante `IStorageService`, validar consistencia
  y publicar `InvoiceEligibleForFundingIntegrationEvent` solo cuando la factura es elegible.
- Eliminados `upload-local` y el adapter MinIO anterior para no reintroducir almacenamiento paralelo.
- El proyecto de pruebas OCR fue incorporado a `verify.ps1`.
- La feature se prioriza explícitamente por indicación del usuario, aunque #3 siga pendiente.
- Decisiones acordadas: MinIO en desarrollo, AWS S3 en producción con access keys configuradas por
  entorno, y bucket AWS aprovisionado por IaC. Azure Blob Storage queda fuera de alcance.
- Implementado `S3StorageService`, configuración tipada por proveedor y health check neutral. MinIO
  conserva el autocreado de bucket; AWS S3 lo desactiva y usa región configurada.
- Validación estática: JSON válido, no hay referencias de Application/Domain a Infrastructure y no
  quedan referencias al adapter o health check exclusivos de MinIO.

## Bloqueos

- `verify.ps1` terminó correctamente con build y 12/12 pruebas; no quedan paquetes NuGet vulnerables.
- La verificación end-to-end contra MinIO/AWS S3 no pudo ejecutarse en esta sesión porque Docker
  Desktop no está iniciado y no hay credenciales de Azure OCR configuradas en User Secrets.
