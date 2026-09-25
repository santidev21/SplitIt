# Auth

- JWT signed with HMAC-SHA256. Production requires a 64+ char secret.
- PBKDF2 password hashing via ASP.NET `PasswordHasher<User>` (with legacy SHA256 rehash on login).
- Protected routes on the frontend; admin endpoints require the SuperAdmin role.
- First admin bootstrap (after registering a user):
  ```sql
  UPDATE Users SET RoleId = 1 WHERE Email = 'your@email.com';
  ```
  Or via `POST /api/admin/promote` with a SuperAdmin token.
