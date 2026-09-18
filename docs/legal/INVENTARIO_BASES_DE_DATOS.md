# Inventario de Bases de Datos Personales — SplitIt

> Documento interno de trabajo. Base para el Registro Nacional de Bases de Datos (RNBD) si llega a ser obligatorio.

## 1. Bases de datos y conjuntos de datos

| Base / conjunto | Contenido | Titulares | Finalidad | Ubicación | Encargado | Retención |
|---|---|---|---|---|---|---|
| **SplitItDb.Users** | Nombre, email, hash de contraseña, rol, estado, consentimiento (fecha/versión/IP) | Usuarios registrados | Cuenta y autenticación | SQL Server (VPS `[ubicación]`) | Proveedor de VPS `[nombre]` | Vigencia de la cuenta |
| **SplitItDb.Groups / GroupMembers / Expense / ExpenseShare / Friendships** | Actividad de gastos y relaciones | Usuarios | Operar gastos compartidos | SQL Server | Proveedor de VPS | Anonimizado tras supresión |
| **SplitItDb.AuditLog** | Actor, acción, entidad, fecha, IP | Usuarios/administradores | Trazabilidad y seguridad | SQL Server | Proveedor de VPS | `[X]` meses |
| **SplitItDb.RefreshTokens / PasswordResetTokens** | Hashes de tokens | Usuarios | Sesión y recuperación | SQL Server | Proveedor de VPS | Corto plazo / rotación |
| **Backups** (`splitit_sqlserver_backups` + off-site) | Copia completa de lo anterior | Usuarios | Continuidad | Volumen VPS + `[remote rclone]` | Proveedor de VPS / `[proveedor storage]` | 4 copias semanales (ver `docs/BACKUPS.md`) |
| **Google (OAuth)** | Identificador y correo si el usuario inicia con Google | Usuarios que usan Google | Autenticación | Google LLC (EE. UU.) | Google | Según Google |

## 2. Flujo de datos

```
Titular (navegador) ──HTTPS──> Nginx (VPS) ──> API .NET ──> SQL Server (Docker en el VPS)
                                                   │
                                                   └─> Backups (volumen separado + off-site)
                                                   └─> Google (solo si usa login con Google)
```

## 3. Transferencia / transmisión internacional

- El VPS y el proveedor de backups pueden estar **fuera de Colombia** → **transmisión** a encargados. Se deben suscribir acuerdos de tratamiento y confidencialidad, e informar al titular (hecho en la Política, sección 9).
- **Google OAuth** transfiere datos a Google LLC (EE. UU.) cuando el usuario elige ese método.
- Si algún proveedor se ubica en un país sin nivel adecuado según la SIC, se obtendrá autorización cuando la ley lo exija.

## 4. Análisis del Registro Nacional de Bases de Datos (RNBD)

- La inscripción en el RNBD es obligatoria, conforme al Decreto 090 de 2018, para **sociedades con activos superiores a 100.000 UVT** y **entidades públicas**.
- SplitIt `[es / no es]` una sociedad que supere ese umbral → `[obligado / no obligado]` a inscribirse. **Mantener actualizado este análisis** y, si cambia, registrar las bases ante la SIC.
- Aun sin obligación de RNBD, sí aplican obligaciones de política, autorización, atención de derechos y seguridad.

## 5. Medidas de seguridad por base

- Acceso con usuarios de BD de mínimo privilegio (`splitit_app` sin DDL; `splitit_migrator` con DDL).
- Cifrado en tránsito (TLS/HSTS).
- Contraseñas con PBKDF2; tokens almacenados como hash.
- Auditoría append-only y backups verificados.
