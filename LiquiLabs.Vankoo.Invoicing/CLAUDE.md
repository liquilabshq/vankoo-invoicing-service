# CLAUDE.md — Guía para agentes IA

Este archivo describe la arquitectura, convenciones y reglas del proyecto para que un agente IA pueda trabajar en él sin romper invariantes ni patrones establecidos.

---

## Arquitectura general

Clean Architecture + DDD + CQRS. Las capas en orden de dependencia (de adentro hacia afuera):

```
Domain → Application → Infrastructure
                    ↗
             Interfaces (REST)
```

- **Domain** no depende de nada externo. Sin NuGet, sin frameworks.
- **Application** depende solo de Domain y de sus propias interfaces (contratos).
- **Infrastructure** implementa los contratos de Application y Domain.
- **Interfaces/Rest** traduce HTTP → comandos/queries de Application.
- **Shared** contiene código transversal (excepciones base, handler global de errores). No tiene lógica de negocio.

**Regla crítica:** nunca importar Infrastructure desde Domain o Application. Si Application necesita algo externo, define una interfaz en `Application/Interfaces/` y deja que Infrastructure la implemente.

---

## CQRS con MediatR

Cada caso de uso vive en su propia carpeta dentro de `Application/Commands/` o `Application/Queries/`. Estructura de una carpeta de caso de uso:

```
UploadInvoice/
├── UploadInvoiceCommand.cs       # IRequest<TResponse> — datos de entrada
├── UploadInvoiceCommandHandler.cs # IRequestHandler<TCommand, TResponse>
└── UploadInvoiceCommandValidator.cs # AbstractValidator<TCommand> (FluentValidation)
```

El `ValidationBehavior<TRequest, TResponse>` en `Application/Behaviors/` intercepta automáticamente todos los requests de MediatR y ejecuta los validadores registrados. No hace falta invocar la validación manualmente.

Para agregar un nuevo caso de uso:
1. Crear carpeta en `Commands/` o `Queries/`
2. Definir el Command/Query, Handler y Validator
3. El controlador llama a `mediator.Send(command)` — nada más

---

## Dominio

El agregado principal es `Invoice` (`Domain/Aggregates/Invoice.cs`). Encapsula toda la lógica de negocio de una factura.

Value objects relevantes:
- `InvoiceId` — wrapper de Guid para evitar primitive obsession
- `MypeId` — identidad de la MYPE propietaria de la factura
- `RucNumber` — RUC peruano con validación de formato
- `FileKey` — referencia al archivo en MinIO (no es una URL, es una clave de objeto S3)
- `Money` + `Currency` — importe con divisa
- `OcrTaskStatus` — enum del estado de una tarea OCR (no modificar sin revisar la máquina de estados)

`OcrTask` es una entidad (tiene identidad propia) que vive en `Domain/Entities/`. Representa una tarea de procesamiento OCR con estado y reintentos.

---

## Configuración (IOptions Pattern)

Todas las configuraciones tienen su clase tipada en `Infrastructure/Configuration/Settings/`. Para agregar una nueva sección de configuración:
1. Crear la clase en `Infrastructure/Configuration/Settings/`
2. Registrarla en `Program.cs` con `builder.Services.Configure<MiSettings>(builder.Configuration.GetSection("MiSettings"))`
3. Inyectarla con `IOptions<MiSettings>` o `IOptionsMonitor<MiSettings>`

La jerarquía de appsettings es: `appsettings.json` → `appsettings.{Environment}.json` → variables de entorno del compose. Las variables de entorno sobreescriben con el formato `Section__Key` (doble guion bajo).

`appsettings.Docker.json` está en disco pero **no está trackeado por git** (es un archivo nuevo no confirmado). Contiene los hostnames internos de Docker (`invoicing-db-mongo`, `kafka-broker`, `minio`). No moverlo ni renombrarlo.

---

## Workers en background

`OcrTaskWorker` (`Infrastructure/Workers/`) es un `BackgroundService` que hace polling a MongoDB buscando tareas OCR pendientes. Su configuración está en `OcrWorkerSettings`. No bloquea el pipeline HTTP.

---

## Infraestructura Docker

- El servicio corre en el puerto **8080 dentro del contenedor**, expuesto en el host en el **8082** por defecto.
- `ASPNETCORE_ENVIRONMENT=Docker` activa `appsettings.Docker.json`.
- El Dockerfile es multi-stage: `base` → `build` → `publish` → `final`.
- `curl` se instala en el stage final exclusivamente para los health checks del compose.
- La reconstrucción se hace con `docker compose up -d --build invoicing-service` desde el repo de infraestructura.

---

## Scalar / OpenAPI

Scalar y el esquema OpenAPI solo se registran cuando `!app.Environment.IsProduction()`. En local (`Development`) y en Docker (`Docker`) están disponibles en `/scalar`.

---

## Convenciones de nomenclatura

- Clases en PascalCase, métodos en PascalCase, variables locales en camelCase.
- Commands: `[Acción][Recurso]Command` — ej: `UploadInvoiceCommand`
- Handlers: `[Acción][Recurso]Handler` — ej: `UploadInvoiceCommandHandler`
- Queries: `[Acción][Recurso]Query` — ej: `DownloadInvoiceFileQuery`
- Validators: `[Nombre del Command/Query]Validator`
- Repositorios: interfaz en `Domain/Repositories/`, implementación en `Infrastructure/Persistence/MongoDB/Repositories/`
- Términos del dominio en español (MypeId, RucNumber, etc.), código en inglés.

---

## Seguridad (pendiente de implementar)

El endpoint `POST /api/v1/invoices` aún recibe `mypeId` como campo del formulario, lo cual es un problema de seguridad. La solución diseñada es:
1. Agregar `[Authorize(Roles = "Mype")]`
2. Extraer `mypeId` desde `User.FindFirstValue("mypeId")` en lugar de recibirlo del cliente

No implementar auth parcial — los dos cambios deben ir juntos.

---

## Qué no hacer

- No poner lógica de negocio en los controladores. El controlador solo traduce HTTP → MediatR.
- No inyectar repositorios directamente en controladores. Todo pasa por Application.
- No acceder a la base de datos desde Domain.
- No crear helpers genéricos sin un caso de uso concreto que lo justifique.
- No agregar `appsettings.Docker.json` al `.gitignore` si no lo está — verificar antes de hacer commit.
