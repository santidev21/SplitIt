# Plan de respuesta a incidentes de seguridad — SplitIt

> Obligación de seguridad (art. 4, lit. g, Ley 1581) y deber de cuidado del Responsable.

## 1. Objetivo

Detectar, contener, erradicar y notificar incidentes que afecten la confidencialidad, integridad o disponibilidad de los datos personales.

## 2. Clasificación

| Nivel | Ejemplo | Acción |
|---|---|---|
| **Crítico** | Fuga de base de datos, compromiso de credenciales de administrador, acceso a datos de PII | Respuesta inmediata + análisis de notificación |
| **Alto** | Acceso no autorizado a un grupo, borrado masivo de datos, ransomware | Contención en horas |
| **Medio** | Fallo de backup, error de configuración de CORS/headers | Corregir en el ciclo normal |
| **Bajo** | Intento de login fallido aislado, dependencia con CVE menor | Seguimiento |

## 3. Fases

1. **Detección:** alertas de CI/Trivy/secret scanning, logs de auditoría (`AuditLog`), reportes de usuarios, monitoreo del VPS.
2. **Contención:** aislar el servicio afectado, rotar secretos (`JWT_SECRET`, `DB_PASSWORD`), revocar sesiones.
3. **Evaluación:** determinar qué datos y cuántos titulares se vieron afectados.
4. **Erradicación y recuperación:** aplicar el parche, restaurar desde backup verificado (`docs/BACKUPS.md`) si aplica.
5. **Notificación:** si el incidente afecta datos personales, informar a los titulares afectados y evaluar la notificación a la **SIC** según la normativa aplicable.
6. **Post-mortem:** documentar causa raíz y acciones para evitar recurrencia.

## 4. Contactos

- Responsable de seguridad: `[nombre / correo]`
- Delegado de protección de datos: `[correo@dominio]`
- Autoridad: Superintendencia de Industria y Comercio.

## 5. Preparación mínima esperada

- Secretos fuera del repositorio y rotables.
- Backups verificados y con copia off-site.
- Registro de auditoría activo (entidad `AuditLog`).
- Acceso al VPS restringido (SSH con llave, UFW, usuarios de BD de mínimo privilegio).
