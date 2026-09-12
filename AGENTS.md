# AGENTS.md – Mapa de navegación para agentes de IA

> Este archivo es el **punto de entrada** para cualquier agente que trabaje en este repositorio
> (Invoicing — .NET 10, Clean Architecture + DDD + CQRS). NO es una biblia de reglas: es un
> **mapa**. Lee solo lo que necesites cuando lo necesites (divulgación progresiva).

---

## 1. Antes de empezar (obligatorio)

1. Ejecuta `./verify.ps1` y verifica que termina en 0. Hoy corre `dotnet restore` + `dotnet build`
   (todavía no hay proyecto de tests — ver `docs/verification.md`). Si falla, **para** y resuelve
   el entorno antes de tocar código.
2. Lee `progress.md` para entender en qué estado quedó la última sesión.
3. Lee `feature_list.json` y elige **una** tarea con estado `pending`. No trabajes en más de una a
   la vez (`rules.one_feature_at_a_time`).

## 2. Mapa del repositorio

| Archivo / carpeta | Qué contiene | Cuándo leerlo |
| :--- | :--- | :--- |
| `feature_list.json` | Lista de features con estado (`pending` / `in_progress` / `done` / `blocked`) | Siempre, al empezar |
| `progress.md` | Estado de la sesión actual | Siempre, al empezar |
| `history.md` | Bitácora append-only de sesiones anteriores | Si necesitas contexto histórico |
| `docs/ARCHITECTURE.md` | Capas, CQRS/MediatR, dominio, eventos, config, Docker | Antes de implementar |
| `docs/CONVENTIONS.md` | Nombres, idioma del dominio, simplicidad, comentarios | Antes de escribir código |
| `docs/verification.md` | Qué significa "verde" hoy (solo build) y verificación manual | Antes de declarar una feature `done` |
| `CHECKPOINTS.md` | Criterios objetivos (C1–C8) de "estado final correcto" | Para auto-evaluarte o revisar a otro |
| `verify.ps1` | Script de verificación estándar | Al empezar y antes de cerrar sesión |
| `.claude/agents/` | Subagentes: `leader` (orquesta), `implementer` (implementa una feature), `reviewer` (aprueba/rechaza) | Si orquestas trabajo con varios agentes |
| `LiquiLabs.Vankoo.Invoicing/CLAUDE.md` | Puntero corto a este archivo (Claude Code lo autocarga si trabajas dentro de esa carpeta) | — |

---

## 3. Reglas duras (no negociables)

- **Una sola feature a la vez.** No mezcles cambios de varias features de `feature_list.json` en la misma sesión.
- **No declares una feature `done` sin `./verify.ps1` en verde.** Si la feature requiere verificación manual (Docker, integraciones externas), documenta esa verificación en `progress.md` — no basta con "compila".
- **Documenta lo que haces** en `progress.md` mientras trabajas, no al final.
- **Respeta la regla de capas** de `docs/ARCHITECTURE.md`: Domain y Application nunca importan Infrastructure.
- **Deja el repositorio limpio** antes de cerrar la sesión (ver §5).
- **Si no sabes algo, busca en `docs/`** antes de inventarlo.

---

## 4. Cómo elegir una tarea

1. Abre `feature_list.json`.
2. Filtra por `status == "pending"`.
3. Coge la de menor `id`.
4. Cambia su `status` a `"in_progress"` y guarda.
5. Anota en `progress.md`: feature, plan breve (3-5 bullets).

---

## 5. Cierre de sesión (lifecycle)

Antes de terminar:

1. Ejecuta `./verify.ps1` — tiene que terminar en 0.
2. Si la feature está acabada y revisada (`reviewer` con veredicto `APPROVED`): marca `"status": "done"` en `feature_list.json`.
3. Mueve el resumen de `progress.md` al final de `history.md` (formato indicado en ese archivo).
4. Vacía `progress.md` dejando solo la plantilla.
5. No dejes archivos temporales, `Console.WriteLine` de debug, ni TODOs sin contexto.

---

## 6. Si te bloqueas

- Relee la sección relevante de `docs/`.
- Si la herramienta no hace lo que esperas, **no inventes un workaround**: documenta el bloqueo en
  `progress.md` con `status: blocked` y para la sesión.

---

## 7. Orquestación con subagentes

Para tareas que requieren dividir trabajo, usa el patrón definido en `.claude/agents/`:

- **`leader`** — descompone la tarea, lanza `implementer`/`reviewer` (o `Explore` para investigar
  primero), nunca escribe código.
- **`implementer`** — implementa exactamente una feature de `feature_list.json`, de punta a punta,
  y se autoverifica con `./verify.ps1`.
- **`reviewer`** — aprueba o rechaza comparando contra `docs/ARCHITECTURE.md`, `docs/CONVENTIONS.md`
  y `CHECKPOINTS.md`. Nunca edita código.

Los tres se comunican por archivo (`progress.md`, `feature_list.json`), no por texto largo en el
chat — evita el efecto teléfono descompuesto entre agentes.
