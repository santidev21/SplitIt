import { test, expect } from '@playwright/test';

test.describe.serial('Real Docker Full-Stack E2E through Nginx HTTPS (No Mocks)', () => {
  const timestamp = Date.now();
  const userA = { name: `UserA_${timestamp}`, email: `usera_${timestamp}@e2e.local`, password: 'Password123!' };
  const userB = { name: `UserB_${timestamp}`, email: `userb_${timestamp}@e2e.local`, password: 'Password123!' };

  let tokenA: string = '';
  let userAId: number = 0;
  let tokenB: string = '';
  let userBId: number = 0;
  let groupId: number = 0;
  let expenseId: number = 0;

  const baseUrl = process.env.E2E_BASE_URL || 'https://localhost';

  test.use({ ignoreHTTPSErrors: true });

  test('1. Protected Route — Unauthenticated redirect', async ({ page }) => {
    await page.goto(`${baseUrl}/dashboard/home`);
    await expect(page).toHaveURL(/\/auth\/login/);
  });

  test('2. Real User A Registration via UI', async ({ page }) => {
    await page.goto(`${baseUrl}/auth/register`);
    await page.getByPlaceholder('Enter your name').fill(userA.name);
    await page.getByPlaceholder('example@example.com').fill(userA.email);
    await page.getByPlaceholder('Enter your password').fill(userA.password);
    await page.getByRole('button', { name: 'Register' }).click();

    await expect(page).toHaveURL(/\/dashboard\/home/, { timeout: 10000 });
    
    tokenA = (await page.evaluate(() => localStorage.getItem('token'))) || '';
    userAId = parseInt((await page.evaluate(() => localStorage.getItem('userId'))) || '0', 10);
    expect(tokenA).toBeTruthy();
    expect(userAId).toBeGreaterThan(0);
  });

  test('3. Real User B Registration via API & Login via UI', async ({ request, page }) => {
    const regRes = await request.post(`${baseUrl}/api/auth/register`, {
      data: { name: userB.name, email: userB.email, password: userB.password }
    });
    expect(regRes.ok()).toBeTruthy();
    const regBody = await regRes.json();
    tokenB = regBody.token;
    userBId = regBody.userId;
    expect(tokenB).toBeTruthy();
    expect(userBId).toBeGreaterThan(0);

    await page.goto(`${baseUrl}/auth/login`);
    await page.evaluate(() => localStorage.clear());
    await page.reload();
    await page.getByPlaceholder('example@example.com').fill(userB.email);
    await page.getByPlaceholder('Enter your password').fill(userB.password);
    await page.getByRole('button', { name: 'Login now' }).click();

    await expect(page).toHaveURL(/\/dashboard\/home/, { timeout: 10000 });
  });

  test('4. Real Group Creation & Member Addition', async ({ request }) => {
    const grpRes = await request.post(`${baseUrl}/api/groups/create`, {
      headers: { Authorization: `Bearer ${tokenA}` },
      data: {
        name: `DockerGroup_${timestamp}`,
        description: 'Fullstack E2E Group',
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

  test('5. Real Expense Creation & Debt Calculation', async ({ request }) => {
    const expRes = await request.post(`${baseUrl}/api/expenses/add`, {
      headers: { Authorization: `Bearer ${tokenA}` },
      data: {
        groupId: groupId,
        title: 'Docker Team Dinner',
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

    // Verify debt summary endpoint
    const debtRes = await request.get(`${baseUrl}/api/expenses/debt-summary?groupId=${groupId}`, {
      headers: { Authorization: `Bearer ${tokenA}` }
    });
    expect(debtRes.ok()).toBeTruthy();
  });

  test('6. Real Partial Payment ($30 paid by User B to User A)', async ({ request }) => {
    const payRes = await request.post(`${baseUrl}/api/expenses/settle`, {
      headers: { Authorization: `Bearer ${tokenA}` },
      data: {
        payerUserId: userBId,
        groupId: groupId,
        amount: 30.0
      }
    });
    expect(payRes.ok()).toBeTruthy();
    const payBody = await payRes.json();
    expect(payBody.remainingDebt).toBe(20.0);
  });

  test('7. Real Settlement (Full liquidation of remaining $20)', async ({ request }) => {
    const setRes = await request.post(`${baseUrl}/api/expenses/settle`, {
      headers: { Authorization: `Bearer ${tokenA}` },
      data: {
        payerUserId: userBId,
        groupId: groupId,
        amount: 20.0
      }
    });
    expect(setRes.ok()).toBeTruthy();
    const setBody = await setRes.json();
    expect(setBody.remainingDebt).toBe(0.0);
  });

  test('8. Real BOLA / Authorization Control', async ({ request }) => {
    const isoGroupRes = await request.post(`${baseUrl}/api/groups/create`, {
      headers: { Authorization: `Bearer ${tokenA}` },
      data: {
        name: `SecretGroup_${timestamp}`,
        description: 'Private Isolated Group',
        currencyId: 1,
        members: []
      }
    });
    expect(isoGroupRes.ok()).toBeTruthy();
    const isoGroup = await isoGroupRes.json();

    // User B attempts to access debt summary of User A's private group (should be rejected 403)
    const bolaRes = await request.get(`${baseUrl}/api/expenses/debt-summary?groupId=${isoGroup.groupId}`, {
      headers: { Authorization: `Bearer ${tokenB}` }
    });
    expect(bolaRes.status()).toBe(403);
  });

  test('9. Real Logout via UI', async ({ page }) => {
    await page.goto(`${baseUrl}/dashboard/home`);
    
    await page.evaluate(() => {
      localStorage.removeItem('token');
      localStorage.removeItem('userName');
      localStorage.removeItem('userId');
    });

    await page.goto(`${baseUrl}/dashboard/home`);
    await expect(page).toHaveURL(/\/auth\/login/);
  });
});
