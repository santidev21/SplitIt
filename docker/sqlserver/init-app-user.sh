#!/bin/bash
set -e

# SplitIt — init dedicated app user with least privilege
# Runs as one-shot container db-init after sqlserver is healthy
# Uses SA to create LOGIN/USER if not exists

SA_PWD="${MSSQL_SA_PASSWORD}"
APP_USER="${MSSQL_APP_USER:-splitit_app}"
APP_PWD="${MSSQL_APP_PASSWORD}"

if [ -z "$APP_PWD" ]; then
  echo "ERROR: MSSQL_APP_PASSWORD not set. Set DB_APP_PASSWORD in .env"
  exit 1
fi
if [ -z "$SA_PWD" ]; then
  echo "ERROR: MSSQL_SA_PASSWORD not set"
  exit 1
fi

echo "Waiting for SQL Server at sqlserver:1433..."
for i in {1..30}; do
  if /opt/mssql-tools18/bin/sqlcmd -S sqlserver -U sa -P "$SA_PWD" -C -Q "SELECT 1" >/dev/null 2>&1 || /opt/mssql-tools/bin/sqlcmd -S sqlserver -U sa -P "$SA_PWD" -Q "SELECT 1" >/dev/null 2>&1; then
    echo "SQL Server reachable"
    break
  fi
  echo "  attempt $i/30..."
  sleep 2
done

echo "Ensuring LOGIN [$APP_USER] exists..."
/opt/mssql-tools18/bin/sqlcmd -S sqlserver -U sa -P "$SA_PWD" -C -Q "
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'$APP_USER')
BEGIN
  CREATE LOGIN [$APP_USER] WITH PASSWORD = N'$APP_PWD', CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;
  PRINT 'LOGIN $APP_USER created';
END
ELSE
BEGIN
  PRINT 'LOGIN $APP_USER already exists';
  ALTER LOGIN [$APP_USER] WITH PASSWORD = N'$APP_PWD';
END
" 2>&1 || /opt/mssql-tools/bin/sqlcmd -S sqlserver -U sa -P "$SA_PWD" -Q "
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'$APP_USER')
  CREATE LOGIN [$APP_USER] WITH PASSWORD = N'$APP_PWD';
" 2>&1

echo "Ensuring DATABASE SplitItDb exists..."
/opt/mssql-tools18/bin/sqlcmd -S sqlserver -U sa -P "$SA_PWD" -C -Q "
IF DB_ID(N'SplitItDb') IS NULL
BEGIN
  CREATE DATABASE [SplitItDb];
  PRINT 'DATABASE SplitItDb created';
END
" 2>&1 || /opt/mssql-tools/bin/sqlcmd -S sqlserver -U sa -P "$SA_PWD" -Q "IF DB_ID(N'SplitItDb') IS NULL CREATE DATABASE [SplitItDb];" 2>&1

echo "Ensuring USER [$APP_USER] in SplitItDb with least privilege..."
/opt/mssql-tools18/bin/sqlcmd -S sqlserver -U sa -P "$SA_PWD" -C -d SplitItDb -Q "
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'$APP_USER')
BEGIN
  CREATE USER [$APP_USER] FOR LOGIN [$APP_USER];
  PRINT 'USER $APP_USER created in SplitItDb';
END
-- Least privilege: data reader/writer + ddl for EF migrations, NOT db_owner
IF IS_ROLEMEMBER('db_datareader', '$APP_USER') = 0 ALTER ROLE db_datareader ADD MEMBER [$APP_USER];
IF IS_ROLEMEMBER('db_datawriter', '$APP_USER') = 0 ALTER ROLE db_datawriter ADD MEMBER [$APP_USER];
IF IS_ROLEMEMBER('db_ddladmin', '$APP_USER') = 0 ALTER ROLE db_ddladmin ADD MEMBER [$APP_USER];
PRINT 'Roles granted: db_datareader, db_datawriter, db_ddladmin';
-- Verify not db_owner
IF IS_ROLEMEMBER('db_owner', '$APP_USER') = 1 PRINT 'WARNING: $APP_USER is db_owner - should be avoided';
" 2>&1 || /opt/mssql-tools/bin/sqlcmd -S sqlserver -U sa -P "$SA_PWD" -d SplitItDb -Q "
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'$APP_USER') CREATE USER [$APP_USER] FOR LOGIN [$APP_USER];
IF IS_ROLEMEMBER('db_datareader', '$APP_USER') = 0 ALTER ROLE db_datareader ADD MEMBER [$APP_USER];
IF IS_ROLEMEMBER('db_datawriter', '$APP_USER') = 0 ALTER ROLE db_datawriter ADD MEMBER [$APP_USER];
IF IS_ROLEMEMBER('db_ddladmin', '$APP_USER') = 0 ALTER ROLE db_ddladmin ADD MEMBER [$APP_USER];
" 2>&1

echo "App user init completed successfully for $APP_USER"
