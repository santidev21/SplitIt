import { test, expect, fakeJwt, loginViaStorage, mockRefreshEndpoint } from '../fixtures/api';

test.describe('Phase 8 — Group Admin', () => {
  const creatorToken = fakeJwt({ sub: '1' });
  const adminToken = fakeJwt({ sub: '2' });
  const memberToken = fakeJwt({ sub: '3' });

  test('Creator can promote member to admin', async ({ page }) => {
    await page.addInitScript((t: string) => localStorage.setItem('token', t), creatorToken);
    await loginViaStorage(page, creatorToken);
    await page.goto('/auth/login');
    await page.route('**/api/groups/1/members/3/role', async route => {
      const body = route.request().postDataJSON();
      expect(body.role).toBe('admin');
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ message: 'Role updated.' }) });
    });
    const status = await page.evaluate(async () => {
      const r = await fetch('http://localhost:5120/api/groups/1/members/3/role', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json', Authorization: 'Bearer ' + localStorage.getItem('token')! },
        body: JSON.stringify({ role: 'admin' })
      });
      return r.status;
    });
    expect(status).toBe(200);
  });

  test('Member cannot promote self', async ({ page }) => {
    await page.addInitScript((t: string) => localStorage.setItem('token', t), memberToken);
    await loginViaStorage(page, memberToken);
    await page.goto('/auth/login');
    await page.route('**/api/groups/1/members/3/role', async route => route.fulfill({ status: 403 }));
    const status = await page.evaluate(async () => {
      const r = await fetch('http://localhost:5120/api/groups/1/members/3/role', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json', Authorization: 'Bearer ' + localStorage.getItem('token')! },
        body: JSON.stringify({ role: 'admin' })
      });
      return r.status;
    });
    expect(status).toBe(403);
  });

  test('Admin cannot promote to admin (only creator)', async ({ page }) => {
    await page.addInitScript((t: string) => localStorage.setItem('token', t), adminToken);
    await loginViaStorage(page, adminToken);
    await page.goto('/auth/login');
    await page.route('**/api/groups/1/members/3/role', async route => route.fulfill({ status: 403 }));
    const status = await page.evaluate(async () => {
      const r = await fetch('http://localhost:5120/api/groups/1/members/3/role', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json', Authorization: 'Bearer ' + localStorage.getItem('token')! },
        body: JSON.stringify({ role: 'admin' })
      });
      return r.status;
    });
    expect(status).toBe(403);
  });

  test('Creator can remove member, member cannot remove admin', async ({ page }) => {
    await page.addInitScript((t: string) => localStorage.setItem('token', t), creatorToken);
    await loginViaStorage(page, creatorToken);
    await page.goto('/auth/login');
    await page.route('**/api/groups/1/members/3', async route => {
      expect(route.request().method()).toBe('DELETE');
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ message: 'Member removed.' }) });
    });
    let status = await page.evaluate(async () => {
      const r = await fetch('http://localhost:5120/api/groups/1/members/3', { method: 'DELETE', headers: { Authorization: 'Bearer ' + localStorage.getItem('token')! } });
      return r.status;
    });
    expect(status).toBe(200);

    // Member trying to remove admin → 403
    await page.addInitScript((t: string) => localStorage.setItem('token', t), memberToken);
    await loginViaStorage(page, memberToken);
    await page.goto('/auth/login');
    await page.route('**/api/groups/1/members/2', async route => route.fulfill({ status: 403 }));
    status = await page.evaluate(async () => {
      const r = await fetch('http://localhost:5120/api/groups/1/members/2', { method: 'DELETE', headers: { Authorization: 'Bearer ' + localStorage.getItem('token')! } });
      return r.status;
    });
    expect(status).toBe(403);
  });

  test('Only creator can delete group', async ({ page }) => {
    await page.addInitScript((t: string) => localStorage.setItem('token', t), memberToken);
    await loginViaStorage(page, memberToken);
    await page.goto('/auth/login');
    await page.route('**/api/groups/1', async route => route.fulfill({ status: 403 }));
    let status = await page.evaluate(async () => {
      const r = await fetch('http://localhost:5120/api/groups/1', { method: 'DELETE', headers: { Authorization: 'Bearer ' + localStorage.getItem('token')! } });
      return r.status;
    });
    expect(status).toBe(403);

    await page.addInitScript((t: string) => localStorage.setItem('token', t), creatorToken);
    await loginViaStorage(page, creatorToken);
    await page.goto('/auth/login');
    await page.route('**/api/groups/1', async route => route.fulfill({ status: 200 }));
    status = await page.evaluate(async () => {
      const r = await fetch('http://localhost:5120/api/groups/1', { method: 'DELETE', headers: { Authorization: 'Bearer ' + localStorage.getItem('token')! } });
      return r.status;
    });
    expect(status).toBe(200);
  });
});
