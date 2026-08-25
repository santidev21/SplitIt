#!/bin/bash
set -e

# SplitIt — init dedicated DB users with least privilege (Phase 10)
# Runs as one-shot container db-init after sqlserver is healthy
# Uses SA to create LOGIN/USER if not exists
# - splitit_app: runtime API (reader + writer, NO DDL)
# - splitit_migrator: migration job (reader + writer + ddladmin)

SA_PWD="${MSSQL_SA_PASSWORD}"
APP_USER="${MSSQL_APP_USER:-splitit_app}"
APP_PWD="${MSSQL_APP_PASSWORD}"
MIG_USER="${MSSQL_MIGRATOR_USER:-splitit_migrator}"
MIG_PWD="${MSSQL_MIGRATOR_PASSWORD}"

if [ -z "$APP_PWD" ]; then echo "ERROR: MSSQL_APP_PASSWORD not set"; exit 1; fi
if [ -z "$MIG_PWD" ]; then echo "ERROR: MSSQL_MIGRATOR_PASSWORD not set"; exit 1; fi
if [ -z "$SA_PWD" ]; then echo "ERROR: MSSQL_SA_PASSWORD not set"; exit 1; fi

echo "Waiting for SQL Server at sqlserver:1433..."
for i in {1..30}; do
  if /opt/mssql-tools18/bin/sqlcmd -S sqlserver -U sa -P "$SA_PWD" -C -Q "SELECT 1" >/dev/null 2>&1 || /opt/mssql-tools/bin/sqlcmd -S sqlserver -U sa -P "$SA_PWD" -Q "SELECT 1" >/dev/null 2>&1; then
    echo "SQL Server reachable"
    break
  fi
  echo "  attempt $i/30..."
  sleep 2
done

create_login() {
  local USER=$1
  local PWD=$2
  echo "Ensuring LOGIN [$USER] exists..."
  /opt/mssql-tools18/bin/sqlcmd -S sqlserver -U sa -P "$SA_PWD" -C -Q "
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'$USER')
  CREATE LOGIN [$USER] WITH PASSWORD = N'$PWD', CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;
ELSE
  ALTER LOGIN [$USER] WITH PASSWORD = N'$PWD';
" 2>&1 || /opt/mssql-tools/bin/sqlcmd -S sqlserver -U sa -P "$SA_PWD" -Q "
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'$USER') CREATE LOGIN [$USER] WITH PASSWORD = N'$PWD';
" 2>&1
}

create_login "$APP_USER" "$APP_PWD"
create_login "$MIG_USER" "$MIG_PWD"

echo "Ensuring DATABASE SplitItDb exists..."
/opt/mssql-tools18/bin/sqlcmd -S sqlserver -U sa -P "$SA_PWD" -C -Q "
IF DB_ID(N'SplitItDb') IS NULL CREATE DATABASE [SplitItDb];
" 2>&1 || /opt/mssql-tools/bin/sqlcmd -S sqlserver -U sa -P "$SA_PWD" -Q "IF DB_ID(N'SplitItDb') IS NULL CREATE DATABASE [SplitItDb];" 2>&1

ensure_user() {
  local USER=$1
  local ROLES_SQL=$2
  echo "Ensuring USER [$USER] in SplitItDb with roles..."
  /opt/mssql-tools18/bin/sqlcmd -S sqlserver -U sa -P "$SA_PWD" -C -d SplitItDb -Q "
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'$USER')
  CREATE USER [$USER] FOR LOGIN [$USER];
$ROLES_SQL
IF IS_ROLEMEMBER('db_owner', '$USER') = 1 PRINT 'WARNING: $USER is db_owner - should be avoided';
" 2>&1 || /opt/mssql-tools/bin/sqlcmd -S sqlserver -U sa -P "$SA_PWD" -d SplitItDb -Q "
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'$USER') CREATE USER [$USER] FOR LOGIN [$USER];
$ROLES_SQL
" 2>&1
}

# splitit_app: runtime least privilege — NO DDL
ensure_user "$APP_USER" "
IF IS_ROLEMEMBER('db_datareader', '$APP_USER') = 0 ALTER ROLE db_datareader ADD MEMBER [$APP_USER];
IF IS_ROLEMEMBER('db_datawriter', '$APP_USER') = 0 ALTER ROLE db_datawriter ADD MEMBER [$APP_USER];
IF IS_ROLEMEMBER('db_ddladmin', '$APP_USER') = 1 ALTER ROLE db_ddladmin DROP MEMBER [$APP_USER];
IF IS_ROLEMEMBER('db_owner', '$APP_USER') = 1 ALTER ROLE db_owner DROP MEMBER [$APP_USER];
PRINT 'Roles for $APP_USER: db_datareader, db_datawriter (no ddl, no owner)';
"

# splitit_migrator: migration job — needs DDL
ensure_user "$MIG_USER" "
IF IS_ROLEMEMBER('db_datareader', '$MIG_USER') = 0 ALTER ROLE db_datareader ADD MEMBER [$MIG_USER];
IF IS_ROLEMEMBER('db_datawriter', '$MIG_USER') = 0 ALTER ROLE db_datawriter ADD MEMBER [$MIG_USER];
IF IS_ROLEMEMBER('db_ddladmin', '$MIG_USER') = 0 ALTER ROLE db_ddladmin ADD MEMBER [$MIG_USER];
IF IS_ROLEMEMBER('db_owner', '$MIG_USER') = 1 ALTER ROLE db_owner DROP MEMBER [$MIG_USER];
PRINT 'Roles for $MIG_USER: db_datareader, db_datawriter, db_ddladmin';
"

echo "DB users init completed: $APP_USER (runtime) and $MIG_USER (migrator)"
