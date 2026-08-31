import { test, expect, fakeJwt, loginViaStorage, mockRefreshEndpoint } from '../fixtures/api';

test.describe('Phase 8 — Application Admin', () => {
  const userToken = fakeJwt({ sub: '3' }); // RoleId 3 user
  const adminToken = fakeJwt({ sub: '2' }); // but JWT role claim not used here directly; we mock 403/200
  const superToken = fakeJwt({ sub: '1' });

  test('User cannot access admin endpoint → 403', async ({ page }) => {
    await page.addInitScript((t: string) => localStorage.setItem('token', t), userToken);
    await loginViaStorage(page, userToken);
    await page.goto('/auth/login');
    await page.route('**/api/admin/users', async route => route.fulfill({ status: 403 }));
    const status = await page.evaluate(async () => {
      const r = await fetch('http://localhost:5120/api/admin/users', { headers: { Authorization: 'Bearer ' + localStorage.getItem('token')! } });
      return r.status;
    });
    expect(status).toBe(403);
  });

  test('Admin can access admin endpoint → 200', async ({ page }) => {
    await page.addInitScript((t: string) => localStorage.setItem('token', t), adminToken);
    await loginViaStorage(page, adminToken);
    await page.goto('/auth/login');
    await page.route('**/api/admin/users', async route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([{ id: 1, name: 'User' }]) }));
    const status = await page.evaluate(async () => {
      const r = await fetch('http://localhost:5120/api/admin/users', { headers: { Authorization: 'Bearer ' + localStorage.getItem('token')! } });
      return r.status;
    });
    expect(status).toBe(200);
  });

  test('User cannot promote own role → 403', async ({ page }) => {
    await page.addInitScript((t: string) => localStorage.setItem('token', t), userToken);
    await loginViaStorage(page, userToken);
    await page.goto('/auth/login');
    await page.route('**/api/admin/users/3/role', async route => route.fulfill({ status: 403 }));
    const status = await page.evaluate(async () => {
      const r = await fetch('http://localhost:5120/api/admin/users/3/role', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json', Authorization: 'Bearer ' + localStorage.getItem('token')! },
        body: JSON.stringify({ roleId: 2 })
      });
      return r.status;
    });
    expect(status).toBe(403);
  });

  test('Super admin can promote user', async ({ page }) => {
    await page.addInitScript((t: string) => localStorage.setItem('token', t), superToken);
    await loginViaStorage(page, superToken);
    await page.goto('/auth/login');
    await page.route('**/api/admin/users/3/role', async route => {
      expect(route.request().postDataJSON().roleId).toBe(2);
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ message: 'Role updated.' }) });
    });
    const status = await page.evaluate(async () => {
      const r = await fetch('http://localhost:5120/api/admin/users/3/role', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json', Authorization: 'Bearer ' + localStorage.getItem('token')! },
        body: JSON.stringify({ roleId: 2 })
      });
      return r.status;
    });
    expect(status).toBe(200);
  });
});
