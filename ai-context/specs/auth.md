# Auth

- JWT signed with HMAC-SHA256. Production requires a 64+ char secret.
- BCrypt password hashing, with automatic rehash on login.
- Protected routes on the frontend; admin endpoints require the SuperAdmin role.
- First admin bootstrap (after registering a user):
  ```sql
  UPDATE Users SET RoleId = 1 WHERE Email = 'your@email.com';
  ```
  Or via `POST /api/admin/promote` with a SuperAdmin token.
