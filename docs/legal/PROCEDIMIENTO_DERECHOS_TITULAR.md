# Procedimiento para el ejercicio de los derechos del titular

> Da cumplimiento al Capítulo 25 del Decreto 1074 de 2015 (Decreto 1377 de 2013), arts. 20 a 26.

## 1. Derechos que se atienden

Conocer, actualizar, rectificar, solicitar prueba de la autorización, ser informado del uso, revocar la autorización, solicitar la supresión y presentar quejas ante la SIC.

## 2. Canal

`[correo@dominio]` (asunto: "Derechos de titular — SplitIt"). También desde la aplicación:

- Exportar mis datos → `GET /api/users/me/export`
- Eliminar mi cuenta → `DELETE /api/users/me`

## 3. Consultas

- Plazo máximo: **10 días hábiles** desde la recepción.
- Prórroga: hasta **5 días hábiles** más, informando el motivo.
- Respuesta: por el mismo canal; si no es posible, por correo con acuse.

## 4. Reclamos

- Plazo máximo: **15 días hábiles** desde la recepción.
- Prórroga: hasta **8 días hábiles** más, informando el motivo.
- Si el reclamo está incompleto, se pedirá subsanación dentro de los **5 días** siguientes; si no se subsana, se tendrá por desistido.
- Si se acepta el reclamo, se corregirá, actualizará o suprimirá el dato dentro del término.
- Si no se acepta, se informará al titular y se dejará constancia.

## 5. Supresión de cuenta (derecho de supresión)

Al solicitarla:

1. Se revocan las sesiones activas (tokens de refresco).
2. Se **anonimizan** los datos personales (nombre y correo) en la cuenta.
3. Se conservan los **registros financieros** (grupos, gastos, shares y pagos) de forma anonimizada, para no romper las cuentas de los demás miembros y por las obligaciones legales/contables.
4. No se puede suprimir información cuando exista un deber legal o contractual de conservarla (art. 8, parágrafo, Ley 1581).

## 6. Registro de actuaciones

Toda solicitud y su respuesta se registran en el [Inventario de Bases de Datos](INVENTARIO_BASES_DE_DATOS.md) / sistema de trazabilidad interna, sin exponer datos sensibles.

## 7. Autoridad

Si el titular considera que no se atendió correctamente, puede acudir a la Superintendencia de Industria y Comercio — Delegatura para la Protección de Datos Personales.
