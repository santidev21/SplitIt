# SplitIt — Backups & Restore

> Retención: **4 backups**. Frecuencia: **semanal** (domingo 03:00 UTC) + **pre-deploy**. Copia **off-site** opcional recomendada.

## Por qué

Los backups viven en un **volumen Docker separado** (`splitit_sqlserver_backups`) montado en `/var/opt/mssql/backups`, nunca en el volumen de datos (`splitit_sqlserver_data`). Así, si se corrompe o se pierde el volumen de datos, los `.bak` sobreviven. Para protección real ante pérdida del VPS, configura la copia off-site (rclone).

- **RPO** (pérdida máxima de datos): 7 días con cron semanal; 0 con backup pre-deploy. Si necesitas menos, baja la frecuencia del cron (ver más abajo).
- **RTO** (tiempo de recuperación): ~2-5 min (restore de un `.bak` verificado).

## Uso rápido (dev / host con Docker)

```bash
npm run db:backup          # crea + verifica + retención (+ off-site si RCLONE_REMOTE)
npm run db:backup:list     # lista los backups del volumen
npm run db:restore -- /var/opt/mssql/backups/splitit_SplitItDb_YYYYMMDD_HHMMSS.bak
```

En Windows (`npm run db:backup`) se ejecuta `scripts/db-backup.ps1`; en Linux/VPS, `scripts/db-backup.sh` (el dispatcher `scripts/db-backup.mjs` elige).

Variables de entorno (todas opcionales salvo `DB_PASSWORD`, que se lee de `.env`):

| Variable | Default | Descripción |
|---|---|---|
| `DB_PASSWORD` | lee de `.env` | Password de `sa`. |
| `DB_CONTAINER` | `splitit-db` | Contenedor de SQL Server. |
| `DB_NAME` | `SplitItDb` | Base de datos. |
| `BACKUP_DIR` | `/var/opt/mssql/backups` | Ruta **dentro** del contenedor (volumen separado). |
| `RETENTION` | `4` | Backups más recientes a conservar. |
| `RCLONE_REMOTE` | vacío | Ej. `b2:splitit-backups`; si se setea, sube copia off-site. |

## Programar el cron semanal (VPS)

```bash
cd /opt/splitit
sudo -E RCLONE_REMOTE=b2:splitit-backups ./scripts/install-backup-cron.sh
crontab -l   # verifica
tail -f /var/log/splitit-backup.log
```

Por defecto instala `0 3 * * 0` (domingos 03:00 UTC). Personaliza con `BACKUP_CRON`:

```bash
sudo -E BACKUP_CRON="0 3 * * *" ./scripts/install-backup-cron.sh   # diario
```

## Copia off-site (rclone)

Los backups en el mismo VPS no protegen contra la pérdida del VPS. Configura rclone:

```bash
# Instalar: https://rclone.org/install/
rclone config          # crea un remote (b2, s3, gdrive, etc.)
rclone lsd b2:         # verifica acceso
```

Luego exporta `RCLONE_REMOTE=b2:splitit-backups` en el cron (ver arriba) o en el entorno del script. La copia se sube con `--checksum` tras verificar el `.bak` local.

## Restore drill (hazlo una vez y repite trimestralmente)

Ensayar el restore al menos una vez antes de confiar en él:

```bash
# 1. Crea un backup fresco
npm run db:backup
npm run db:backup:list        # copia la ruta del .bak más reciente

# 2. Verifica (no destructivo)
node ./scripts/db-backup.mjs verify /var/opt/mssql/backups/<archivo>.bak

# 3. Restaura (DESTRUCTIVO: sobreescribe SplitItDb)
COMPOSE="docker compose -f docker-compose.yml -f docker-compose.local.yml"
$COMPOSE stop backend
npm run db:restore -- /var/opt/mssql/backups/<archivo>.bak
$COMPOSE start backend
curl -fsS http://localhost:8090/health/ready
```

> `verify` no tiene script npm; se invoca con `node ./scripts/db-backup.mjs verify <archivo>` o `bash scripts/db-backup.sh verify <archivo>`.

Documenta el resultado del drill (fecha, archivo, quién lo hizo) — es evidencia de diligencia ante la SIC y de continuidad de negocio.

## Backups pre-deploy

`scripts/deploy.sh` crea un backup antes de migrar (`splitit_predeploy_*.bak`), lo verifica con `RESTORE VERIFYONLY` y conserva los 4 más recientes en el mismo volumen de backups. Si el backup falla, el deploy continúa porque las migraciones son idempotentes, pero **revisa el log**.

## Seguridad y privacidad

- Los `.bak` contienen datos personales (Ley 1581). Restringe el acceso al VPS y al remote off-site; usa buckets privados y credenciales de solo-escritura para el backup.
- El volumen de backups hereda el aislamiento de Docker; no lo montes en el frontend ni en la red pública.
- Alinea la retención con tu política de tratamiento: 4 backups semanales ≈ 4 semanas de datos. Ajusta `RETENTION` si tu política difiere.
- Nunca subas `.bak` a repositorios ni los incluyas en imágenes.

## Limitaciones conocidas

- **No hay point-in-time recovery**: solo backups full. Para PITR se necesitaría `RESTORE LOG` + recovery model FULL y retención de logs.
- **No hay cifrado del `.bak`** más allá del cifrado en reposo del disco/volumen. Evalúa `WITH ENCRYPTION` si el requisito de privacidad lo exige.
- El cron depende de que el contenedor `splitit-db` esté corriendo; el script falla con error claro si no lo está.

## Troubleshooting

| Síntoma | Causa / fix |
|---|---|
| `DB_PASSWORD not set` | Exporta la variable o define `DB_PASSWORD=` en `.env`. |
| `container 'splitit-db' is not running` | `npm run db:up` (o `docker compose ... up -d sqlserver`). |
| `sqlcmd not found in container` | Imagen de SQL Server sin mssql-tools; usa `mcr.microsoft.com/mssql/server:2022-latest`. |
| `Off-site copy SKIPPED` | `RCLONE_REMOTE` no está configurado. Ver sección off-site. |
| Restore falla por conexiones activas | El script fuerza `SINGLE_USER WITH ROLLBACK IMMEDIATE`; detén `backend` antes. |
