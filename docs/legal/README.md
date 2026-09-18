# SplitIt — Documentos legales (Ley 1581 de 2012, Colombia)

> **Aviso importante:** estas plantillas describen el tratamiento de datos de SplitIt y están redactadas según la Ley 1581 de 2012 y el Decreto 1074 de 2015 (Título 2, Capítulo 25), que compiló el Decreto 1377 de 2013. **No son asesoría jurídica.** Antes de usarse con usuarios reales deben ser revisadas y aprobadas por un abogado colombiano, y deben completarse los datos del Responsable (marcados con `[...]`).

## Índice

| Documento | Propósito |
|---|---|
| [POLITICA_TRATAMIENTO_DATOS_PERSONALES.md](POLITICA_TRATAMIENTO_DATOS_PERSONALES.md) | Política integral de tratamiento; documento público exigido por la ley. |
| [AVISO_PRIVACIDAD.md](AVISO_PRIVACIDAD.md) | Aviso corto que se muestra en el punto de recolección (registro). |
| [TERMINOS_Y_CONDICIONES.md](TERMINOS_Y_CONDICIONES.md) | Condiciones de uso del servicio. |
| [PROCEDIMIENTO_DERECHOS_TITULAR.md](PROCEDIMIENTO_DERECHOS_TITULAR.md) | Cómo se atienden consultas, reclamos, acceso, rectificación y supresión. |
| [PLAN_RESPUESTA_INCIDENTES.md](PLAN_RESPUESTA_INCIDENTES.md) | Detección, contención y notificación de incidentes de seguridad. |
| [INVENTARIO_BASES_DE_DATOS.md](INVENTARIO_BASES_DE_DATOS.md) | Inventario interno de bases de datos y análisis del RNBD. |

## Datos por completar antes de publicar

- Identidad del **Responsable del Tratamiento**: `[NOMBRE / RAZÓN SOCIAL]`, `[NIT / C.C.]`, `[DOMICILIO]`, `[CIUDAD, COLOMBIA]`.
- Canal de atención de derechos: `[correo@dominio]` (recomendado un correo dedicado, p. ej. `datos@...`).
- Ubicación del hosting/VPS y proveedores que tratan datos por cuenta de SplitIt (ver Inventario).
- Fecha de entrada en vigencia y versión de la política.

## Implementación en el producto (trazabilidad)

- Control de autorización en el registro y trazabilidad del tratamiento: ver `docs/AUDIT_2026-09.md` (Fase 4) y la entidad `AuditLog`.
- Derecho de acceso/portabilidad y de supresión: endpoints `GET /api/users/me/export` y `DELETE /api/users/me`.
