# Política de Tratamiento de Datos Personales — SplitIt

> Versión: `1.0` · Vigente desde: `[FECHA]`
> Documento público exigido por el artículo 13 del Decreto 1377 de 2013, compilado en el Decreto 1074 de 2015.

## 1. Identificación del Responsable del Tratamiento

- **Responsable:** `[NOMBRE / RAZÓN SOCIAL]`
- **Identificación:** `[NIT / C.C.]`
- **Domicilio:** `[DIRECCIÓN]`, `[CIUDAD]`, Colombia
- **Correo de privacidad:** `[correo@dominio]`
- **Sitio/aplicación:** SplitIt (`[https://...]`)

## 2. Marco normativo

Esta política se rige por la Constitución Política (art. 15), la **Ley 1581 de 2012**, el **Decreto 1074 de 2015** (Decreto Único Reglamentario del Sector Comercio, Industria y Turismo, Título 2, Capítulo 25), la **Ley 527 de 1999** (mensajes de datos y firma electrónica) y las instrucciones de la **Superintendencia de Industria y Comercio (SIC)**. La Ley 1266 de 2008 (habeas data financiero) no aplica a SplitIt porque no trata información financiera, crediticia o comercial de titulares.

## 3. Definiciones

- **Dato personal:** información vinculada o que pueda asociarse a una o varias personas naturales determinadas o determinables.
- **Titular:** persona natural cuyos datos personales son tratados.
- **Tratamiento:** cualquier operación sobre datos personales (recolección, almacenamiento, uso, circulación, supresión, etc.).
- **Responsable:** quien decide sobre la base de datos y el tratamiento (SplitIt).
- **Encargado:** quien trata datos por cuenta del Responsable (p. ej. proveedor de hosting).
- **Autorización:** consentimiento previo, expreso e informado del titular.
- **Dato sensible:** afecta la intimidad o genera discriminación (art. 5 Ley 1581). SplitIt **no** recolecta datos sensibles.

## 4. Principios

Se aplican los principios de legalidad, finalidad, libertad, veracidad, transparencia, acceso y circulación restringida, seguridad y confidencialidad (art. 4 Ley 1581).

## 5. Datos tratados y finalidades

SplitIt recolecta únicamente los datos necesarios para operar una aplicación de gastos compartidos entre amigos:

| Categoría | Datos | Finalidad | Base |
|---|---|---|---|
| Identificación y cuenta | Nombre, correo electrónico | Crear la cuenta, autenticar, identificar al usuario ante su grupo | Ejecución del servicio / autorización |
| Autenticación | Hash de contraseña (PBKDF2), identificador de Google (si aplica) | Iniciar sesión de forma segura | Ejecución del servicio |
| Autorización | Fecha, versión de la política y IP de la autorización | Probar que existe autorización (art. 9 Decreto 1377) | Obligación legal |
| Actividad financiera | Grupos, gastos, montos, participantes, estado de liquidación | Calcular quién debe a quién y registrar pagos | Ejecución del servicio |
| Operación y seguridad | Registro de auditoría (quién hizo qué y cuándo), IP | Trazabilidad, seguridad y atención de requerimientos | Interés legítimo / obligación legal |
| Relaciones | Solicitudes y vínculos de amistad | Permitir compartir gastos | Ejecución del servicio |

**No** se recolectan datos sensibles, datos de menores, datos biométricos ni información financiera (tarjetas, cuentas bancarias). Los pagos se registran como acuerdos entre usuarios; SplitIt **no** procesa dinero.

## 6. Autorización

Antes de recolectar los datos, SplitIt obtiene la **autorización previa, expresa e informada** del titular. En el registro se otorga mediante una casilla **no premarcada**; en el primer inicio de sesión con Google se otorga al continuar, tras leer el aviso de autorización mostrado sobre el botón de Google, junto con el enlace a esta política y al aviso de privacidad. La autorización puede ser revocada en cualquier momento, salvo cuando exista un deber legal de conservar el dato.

## 7. Derechos del titular

El titular puede (art. 8 Ley 1581):

1. Conocer, actualizar y rectificar sus datos.
2. Solicitar prueba de la autorización otorgada.
3. Ser informado sobre el uso dado a sus datos.
4. Presentar quejas ante la SIC por infracciones.
5. Revocar la autorización y/o solicitar la supresión, cuando no exista deber legal de conservar el dato.
6. Acceder de forma gratuita a sus datos.

En el producto: **exportar** (`GET /api/users/me/export`) y **suprimir la cuenta** (`DELETE /api/users/me`) están disponibles desde la aplicación.

## 8. Canales y procedimiento

- **Canal:** `[correo@dominio]`.
- **Consultas:** se atienden en máximo **10 días hábiles** (prorrogables 5 más).
- **Reclamos:** se atienden en máximo **15 días hábiles** (prorrogables 8 más).
- El detalle está en [PROCEDIMIENTO_DERECHOS_TITULAR.md](PROCEDIMIENTO_DERECHOS_TITULAR.md).

## 9. Encargados, transferencia y transmisión internacional

SplitIt se apoya en proveedores de infraestructura (hosting/VPS, base de datos, correo) que pueden estar ubicados **fuera de Colombia** y actúan como **encargados**. Con ellos se suscriben acuerdos de tratamiento y confidencialidad. Cuando el país de destino no ofrezca un nivel adecuado de protección según la SIC, se informará al titular y se obtendrá su autorización cuando corresponda. Detalle en [INVENTARIO_BASES_DE_DATOS.md](INVENTARIO_BASES_DE_DATOS.md).

## 10. Medidas de seguridad

- Cifrado en tránsito (HTTPS/HSTS) y contraseñas con PBKDF2 (hash + salt).
- Principio de mínimo privilegio en la base de datos (usuarios separados para runtime y migraciones).
- Registro de auditoría (append-only) de las operaciones sobre datos.
- Copias de seguridad periódicas con retención acotada y verificación de restauración (ver `docs/BACKUPS.md`).
- Control de acceso, revisión de dependencias y escaneo de vulnerabilidades en CI.
- Procedimiento de respuesta a incidentes y notificación a la SIC en los casos que la ley exija.

## 11. Retención y vigencia

- Datos de cuenta: mientras la cuenta esté activa.
- Registros financieros (gastos, shares, pagos): se conservan **anonimizados** tras la supresión de la cuenta, para preservar la integridad de las cuentas entre los demás usuarios y por las obligaciones legales/contables.
- Registro de auditoría: `[X]` meses, o el término que exija la ley.
- Backups: ver política de retención en `docs/BACKUPS.md`.

## 12. Menores de edad

El servicio está dirigido a **mayores de 18 años**. No se recolectan datos de menores. Si se detecta una cuenta de un menor, se procederá a su supresión. El tratamiento de datos de menores está proscrito salvo las excepciones del art. 7 de la Ley 1581 y la autorización del representante legal.

## 13. Cookies y almacenamiento local

Se usan cookies **estrictamente necesarias** para la sesión (token de refresco `HttpOnly`) y almacenamiento local del navegador para preferencias (idioma, nombre de usuario). No se usan cookies publicitarias ni de seguimiento de terceros. Si en el futuro se incorporan cookies no esenciales, se pedirá consentimiento previo.

## 14. Vigencia y cambios

Esta política rige desde `[FECHA]`. Los cambios sustanciales se informarán por la aplicación o por correo y, cuando impliquen una nueva finalidad, se solicitará una nueva autorización. El historial de versiones se mantiene en este repositorio.

---

**Contacto:** `[correo@dominio]` · **Autoridad:** Superintendencia de Industria y Comercio — Delegatura para la Protección de Datos Personales.
