-- ============================================================================
-- SplitIt · Auditoría de integridad de datos (SOLO LECTURA)
-- Motor: SQL Server 2022 (SplitItDb)
-- Uso (host, DB en Docker):
--   docker exec splitit-db /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$DB_PASSWORD" -C \
--     -d SplitItDb -i /dev/stdin < scripts/db-integrity-audit.sql
-- o desde SSMS/Azure Data Studio conectado a 127.0.0.1:1433.
--
-- Cada bloque debe devolver 0 filas. Si alguno devuelve filas, corregir los
-- datos ANTES de aplicar migraciones o desplegar cambios de esquema.
-- ============================================================================

SET NOCOUNT ON;
GO

-- 1. Gasto no-pago con desbalance: suma de shares != monto (tolerancia 0.01)
SELECT '1. Suma de shares != monto' AS check_name, e.[Id], e.[GroupId], e.[Amount],
       SUM(es.[AmountOwed]) AS shares_total
FROM [Expense] e
JOIN [ExpenseShare] es ON es.[ExpenseId] = e.[Id]
WHERE e.[IsPayment] = 0
GROUP BY e.[Id], e.[GroupId], e.[Amount]
HAVING ABS(SUM(es.[AmountOwed]) - e.[Amount]) > 0.01;
GO

-- 2. Gasto no-pago sin participantes (sin shares)
SELECT '2. Gasto sin shares' AS check_name, e.[Id], e.[GroupId], e.[Amount]
FROM [Expense] e
WHERE e.[IsPayment] = 0
  AND NOT EXISTS (SELECT 1 FROM [ExpenseShare] es WHERE es.[ExpenseId] = e.[Id]);
GO

-- 3. Share cuyo usuario NO es miembro del grupo del gasto
SELECT '3. Share fuera del grupo' AS check_name, es.[Id] AS share_id, es.[ExpenseId],
       es.[UserId], e.[GroupId]
FROM [ExpenseShare] es
JOIN [Expense] e ON e.[Id] = es.[ExpenseId]
WHERE NOT EXISTS (
    SELECT 1 FROM [GroupMembers] gm
    WHERE gm.[GroupId] = e.[GroupId] AND gm.[UserId] = es.[UserId]);
GO

-- 4. Gasto creado por un usuario que NO es miembro del grupo
SELECT '4. Creador fuera del grupo' AS check_name, e.[Id], e.[GroupId], e.[CreatedById]
FROM [Expense] e
WHERE NOT EXISTS (
    SELECT 1 FROM [GroupMembers] gm
    WHERE gm.[GroupId] = e.[GroupId] AND gm.[UserId] = e.[CreatedById]);
GO

-- 5. Gasto pagado por un usuario que NO es miembro del grupo
SELECT '5. Pagador fuera del grupo' AS check_name, e.[Id], e.[GroupId], e.[PaidById]
FROM [Expense] e
WHERE NOT EXISTS (
    SELECT 1 FROM [GroupMembers] gm
    WHERE gm.[GroupId] = e.[GroupId] AND gm.[UserId] = e.[PaidById]);
GO

-- 6. Membresía duplicada (GroupId, UserId) — confirma que falte índice único
SELECT '6. Membresía duplicada' AS check_name, [GroupId], [UserId], COUNT(*) AS total
FROM [GroupMembers]
GROUP BY [GroupId], [UserId]
HAVING COUNT(*) > 1;
GO

-- 7. Amistad duplicada en orden inverso (A->B y B->A)
SELECT '7. Amistad duplicada inversa' AS check_name, a.[RequesterId], a.[AddresseeId]
FROM [Friendships] a
JOIN [Friendships] b
  ON b.[RequesterId] = a.[AddresseeId] AND b.[AddresseeId] = a.[RequesterId];
GO

-- 8. Estado de liquidación inconsistente (IsSettled <-> SettledAt)
SELECT '8. Settled inconsistente' AS check_name, [Id], [ExpenseId], [UserId],
       [IsSettled], [SettledAt]
FROM [ExpenseShare]
WHERE ([IsSettled] = 1 AND [SettledAt] IS NULL)
   OR ([IsSettled] = 0 AND [SettledAt] IS NOT NULL);
GO

-- 9. Montos no positivos
SELECT '9. Monto no positivo' AS check_name, [Id], [Amount]
FROM [Expense]
WHERE [Amount] <= 0;
GO

-- 10. Shares con monto no positivo
SELECT '10. Share no positivo' AS check_name, [Id], [ExpenseId], [UserId], [AmountOwed]
FROM [ExpenseShare]
WHERE [AmountOwed] <= 0;
GO

-- 11. Fecha de gasto en el futuro (más de 1 día de tolerancia por zona horaria)
SELECT '11. Fecha futura' AS check_name, [Id], [GroupId], [Date]
FROM [Expense]
WHERE [Date] > DATEADD(day, 1, GETUTCDATE());
GO

-- 12. Emails duplicados (debe ser imposible por índice único)
SELECT '12. Email duplicado' AS check_name, LOWER([Email]) AS email, COUNT(*) AS total
FROM [Users]
GROUP BY LOWER([Email])
HAVING COUNT(*) > 1;
GO

-- 13. Usuario sin rol válido
SELECT '13. Rol inválido' AS check_name, u.[Id], u.[Email], u.[RoleId]
FROM [Users] u
LEFT JOIN [Roles] r ON r.[Id] = u.[RoleId]
WHERE r.[Id] IS NULL;
GO

-- 14. Grupo sin miembros (huérfano lógico)
SELECT '14. Grupo sin miembros' AS check_name, g.[Id], g.[Name]
FROM [Groups] g
WHERE NOT EXISTS (SELECT 1 FROM [GroupMembers] gm WHERE gm.[GroupId] = g.[Id]);
GO

-- 15. Grupo sin moneda válida
SELECT '15. Grupo sin moneda' AS check_name, g.[Id], g.[CurrencyId]
FROM [Groups] g
LEFT JOIN [Currencies] c ON c.[Id] = g.[CurrencyId]
WHERE c.[Id] IS NULL;
GO

-- 16. Shares huérfanos (ExpenseId inexistente) — debe ser imposible por FK
SELECT '16. Share huérfano' AS check_name, es.[Id], es.[ExpenseId]
FROM [ExpenseShare] es
LEFT JOIN [Expense] e ON e.[Id] = es.[ExpenseId]
WHERE e.[Id] IS NULL;
GO

-- 17. Refresh tokens expirados aún presentes (candidatos a limpieza)
SELECT '17. Refresh token expirado' AS check_name, COUNT(*) AS total
FROM [RefreshTokens]
WHERE [ExpiresAt] < GETUTCDATE();
GO

-- 18. Password reset tokens usados o expirados aún presentes (candidatos a limpieza)
SELECT '18. Reset token obsoleto' AS check_name, COUNT(*) AS total
FROM [PasswordResetTokens]
WHERE [Used] = 1 OR [ExpiresAt] < GETUTCDATE();
GO

PRINT 'Auditoría de integridad completada. Cada bloque debe haber devuelto 0 filas.';
GO
