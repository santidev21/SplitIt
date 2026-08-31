import { test, expect } from '@playwright/test';

/**
 * Real Docker full-stack E2E over HTTPS through the Nginx reverse proxy.
 * These tests exercise the production request path:
 *   Browser -> HTTPS -> Nginx -> Angular / API -> SQL Server
 * No API mocking. Self-signed certificates are accepted via ignoreHTTPSErrors.
 */
test.describe.serial('Docker HTTPS Full-Stack E2E (through Nginx)', () => {
  const timestamp = Date.now();
  const userA = { name: `HttpsUserA_${timestamp}`, email: `https_usera_${timestamp}@e2e.local`, password: 'Password123!' };
  const userB = { name: `HttpsUserB_${timestamp}`, email: `https_userb_${timestamp}@e2e.local`, password: 'Password123!' };

  let tokenA = '';
  let userAId = 0;
  let tokenB = '';
  let userBId = 0;
  let groupId = 0;
  let expenseId = 0;

  const baseUrl = process.env.E2E_HTTPS_BASE_URL || 'https://localhost';
  const httpBaseUrl = baseUrl.replace(/^https:\/\//, 'http://');

  test.use({ ignoreHTTPSErrors: true });

  test('1. HTTP redirects normal traffic to HTTPS', async ({ request }) => {
    const resp = await request.get(`${httpBaseUrl}/`, { maxRedirects: 0 });
    expect(resp.status()).toBe(301);
    expect(resp.headers()['location']).toMatch(/^https:\/\//);
  });

  test('2. ACME challenge path stays on HTTP (no redirect)', async ({ request }) => {
    const resp = await request.get(`${httpBaseUrl}/.well-known/acme-challenge/test-token`, { maxRedirects: 0 });
    expect(resp.status()).toBe(404);
  });

  test('3. HTTPS Angular SPA loads and has security headers', async ({ page }) => {
    await page.goto(`${baseUrl}/`);
    await expect(page).toHaveURL(/\/(auth\/login)?$/);
  });

  test('4. Health endpoints return text/plain (not index.html)', async ({ request }) => {
    const live = await request.get(`${baseUrl}/health/live`);
    expect(live.status()).toBe(200);
    expect(await live.text()).toBe('Healthy');
    expect(live.headers()['content-type']).toMatch(/text\/plain/);

    const ready = await request.get(`${baseUrl}/health/ready`);
    expect(ready.status()).toBe(200);
    expect(await ready.text()).toBe('Healthy');

    const aggregate = await request.get(`${baseUrl}/health`);
    expect(aggregate.status()).toBe(200);
    expect(await aggregate.text()).toBe('Healthy');
  });

  test('5. Security headers are present on HTTPS responses', async ({ request }) => {
    const resp = await request.get(`${baseUrl}/`);
    const headers = resp.headers();
    expect(headers['strict-transport-security']).toContain('max-age=31536000');
    expect(headers['x-content-type-options']).toBe('nosniff');
    expect(headers['x-frame-options']).toBe('SAMEORIGIN');
    expect(headers['referrer-policy']).toBe('strict-origin-when-cross-origin');
    expect(headers['permissions-policy']).toBeTruthy();
    expect(headers['cross-origin-opener-policy']).toBe('same-origin');
    expect(headers['cross-origin-resource-policy']).toBe('same-origin');
    expect(headers['content-security-policy']).toContain("default-src 'self'");
  });

  test('6. User A registration via UI', async ({ page }) => {
    await page.goto(`${baseUrl}/auth/register`);

    const respPromise = page.waitForResponse(r =>
      r.url().includes('/api/auth/register') && r.request().method() === 'POST'
    );

    await page.getByPlaceholder('Enter your name').fill(userA.name);
    await page.getByPlaceholder('example@example.com').fill(userA.email);
    await page.getByPlaceholder('Enter your password').fill(userA.password);
    await page.getByRole('button', { name: 'Register' }).click();

    const resp = await respPromise;
    const body = await resp.json();
    tokenA = body.token;
    userAId = body.userId;

    await expect(page).toHaveURL(/\/dashboard\/home/, { timeout: 10000 });
    expect(tokenA).toBeTruthy();
    expect(userAId).toBeGreaterThan(0);
  });

  test('7. User B registration via API', async ({ request }) => {
    const regRes = await request.post(`${baseUrl}/api/auth/register`, {
      data: { name: userB.name, email: userB.email, password: userB.password }
    });
    expect(regRes.ok()).toBeTruthy();
    const regBody = await regRes.json();
    tokenB = regBody.token;
    userBId = regBody.userId;
    expect(tokenB).toBeTruthy();
    expect(userBId).toBeGreaterThan(0);
  });

  test('8. Authenticated API request through Nginx', async ({ request }) => {
    const groupsRes = await request.get(`${baseUrl}/api/groups/user/${userAId}`, {
      headers: { Authorization: `Bearer ${tokenA}` }
    });
    expect(groupsRes.ok()).toBeTruthy();
    const groups = await groupsRes.json();
    expect(Array.isArray(groups)).toBeTruthy();
  });

  test('9. Protected route requires authentication', async ({ page }) => {
    await page.goto(`${baseUrl}/auth/login`);
    await page.evaluate(() => localStorage.clear());
    await page.goto(`${baseUrl}/dashboard/home`);
    await expect(page).toHaveURL(/\/auth\/login/);
  });

  test('10. Group creation', async ({ request }) => {
    const grpRes = await request.post(`${baseUrl}/api/groups/create`, {
      headers: { Authorization: `Bearer ${tokenA}` },
      data: {
        name: `HttpsGroup_${timestamp}`,
        description: 'HTTPS E2E Group',
        currencyId: 1,
        members: [userBId],
        allowToDeleteExpenses: true
      }
    });
    expect(grpRes.ok()).toBeTruthy();
    const grpBody = await grpRes.json();
    groupId = grpBody.groupId;
    expect(groupId).toBeGreaterThan(0);
  });

  test('11. Expense creation', async ({ request }) => {
    const expRes = await request.post(`${baseUrl}/api/expenses/add`, {
      headers: { Authorization: `Bearer ${tokenA}` },
      data: {
        groupId: groupId,
        title: 'HTTPS Team Dinner',
        amount: 100.0,
        date: new Date().toISOString(),
        paidById: userAId,
        participants: [
          { userId: userAId, amountOwed: 50.0 },
          { userId: userBId, amountOwed: 50.0 }
        ]
      }
    });
    expect(expRes.ok()).toBeTruthy();
    const expBody = await expRes.json();
    expenseId = expBody.id;
    expect(expenseId).toBeGreaterThan(0);
  });

  test('12. Settlement', async ({ request }) => {
    const payRes = await request.post(`${baseUrl}/api/expenses/settle`, {
      headers: { Authorization: `Bearer ${tokenA}` },
      data: { payerUserId: userBId, groupId: groupId, amount: 30.0 }
    });
    expect(payRes.ok()).toBeTruthy();
    expect((await payRes.json()).remainingDebt).toBe(20.0);

    const finalRes = await request.post(`${baseUrl}/api/expenses/settle`, {
      headers: { Authorization: `Bearer ${tokenA}` },
      data: { payerUserId: userBId, groupId: groupId, amount: 20.0 }
    });
    expect(finalRes.ok()).toBeTruthy();
    expect((await finalRes.json()).remainingDebt).toBe(0.0);
  });

  test('13. BOLA protection', async ({ request }) => {
    const privateRes = await request.post(`${baseUrl}/api/groups/create`, {
      headers: { Authorization: `Bearer ${tokenA}` },
      data: {
        name: `HttpsPrivate_${timestamp}`,
        description: 'Private',
        currencyId: 1,
        members: []
      }
    });
    expect(privateRes.ok()).toBeTruthy();
    const privateGroup = await privateRes.json();

    const bolaRes = await request.get(`${baseUrl}/api/expenses/debt-summary?groupId=${privateGroup.groupId}`, {
      headers: { Authorization: `Bearer ${tokenB}` }
    });
    expect(bolaRes.status()).toBe(403);
  });

  test('14. Logout via UI clears session', async ({ page }) => {
    await page.goto(`${baseUrl}/dashboard/home`);
    await expect(page.getByRole('banner')).toBeVisible({ timeout: 5000 });

    await page.getByRole('button', { name: 'Logout' }).click();
    await expect(page).toHaveURL(/\/auth\/login/, { timeout: 5000 });

    await page.goto(`${baseUrl}/dashboard/home`);
    await expect(page).toHaveURL(/\/auth\/login/);
  });
});
