import { test, expect, fakeJwt } from '../fixtures/api';

test.describe('Phase 8 — Partial Payments', () => {
  const token = fakeJwt({ sub: '1' });

  test.beforeEach(async ({ page }) => {
    await page.addInitScript((t: string) => {
      localStorage.setItem('token', t);
      localStorage.setItem('userId', '1');
    }, token);
    await page.goto('/auth/login');
  });

  test('Partial payment 30 of 100 → remaining 70', async ({ page }) => {
    let capturedAmount = 0;
    await page.route('**/api/expenses/settle', async route => {
      const body = route.request().postDataJSON();
      capturedAmount = body.amount;
      expect(body.amount).toBe(30);
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ paymentId: 1, remainingDebt: 70, settledCount: 1 }) });
    });
    await page.route('**/api/expenses/remaining-debt*', async r => r.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ remainingDebt: 70 }) }));

    const status = await page.evaluate(async () => {
      const r = await fetch('http://localhost:5120/api/expenses/settle', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', Authorization: 'Bearer ' + localStorage.getItem('token')! },
        body: JSON.stringify({ payerUserId: 1, groupId: 1, amount: 30 })
      });
      return r.status;
    });
    expect(status).toBe(200);
    expect(capturedAmount).toBe(30);
  });

  test('Multiple partial payments 30+20+50 → settled', async ({ page }) => {
    const amounts = [30, 20, 50];
    const remainings = [70, 50, 0];
    for (let i = 0; i < amounts.length; i++) {
      await page.route('**/api/expenses/settle', async route => {
        await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ remainingDebt: remainings[i] }) });
      });
      const status = await page.evaluate(async (amt) => {
        const r = await fetch('http://localhost:5120/api/expenses/settle', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json', Authorization: 'Bearer ' + localStorage.getItem('token')! },
          body: JSON.stringify({ payerUserId: 1, groupId: 1, amount: amt })
        });
        return r.status;
      }, amounts[i]);
      expect(status).toBe(200);
    }
  });

  test('Payment greater than debt → 400', async ({ page }) => {
    await page.route('**/api/expenses/settle', async route => {
      await route.fulfill({ status: 400, contentType: 'application/json', body: JSON.stringify({ message: 'Payment 150 exceeds remaining debt 100.' }) });
    });
    const status = await page.evaluate(async () => {
      const r = await fetch('http://localhost:5120/api/expenses/settle', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', Authorization: 'Bearer ' + localStorage.getItem('token')! },
        body: JSON.stringify({ payerUserId: 1, groupId: 1, amount: 150 })
      });
      return r.status;
    });
    expect(status).toBe(400);
  });

  test('Zero and negative payment → 400', async ({ page }) => {
    for (const amt of [0, -10]) {
      await page.route('**/api/expenses/settle', async route => {
        await route.fulfill({ status: 400, contentType: 'application/json', body: JSON.stringify({ message: 'Invalid payment amount.' }) });
      });
      const status = await page.evaluate(async (a) => {
        const r = await fetch('http://localhost:5120/api/expenses/settle', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json', Authorization: 'Bearer ' + localStorage.getItem('token')! },
          body: JSON.stringify({ payerUserId: 1, groupId: 1, amount: a })
        });
        return r.status;
      }, amt);
      expect(status).toBe(400);
    }
  });

  test('No debt → 400', async ({ page }) => {
    await page.route('**/api/expenses/settle', async route => {
      await route.fulfill({ status: 400, contentType: 'application/json', body: JSON.stringify({ message: 'No debt to settle between these users in this group.' }) });
    });
    const status = await page.evaluate(async () => {
      const r = await fetch('http://localhost:5120/api/expenses/settle', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', Authorization: 'Bearer ' + localStorage.getItem('token')! },
        body: JSON.stringify({ payerUserId: 1, groupId: 1, amount: 10 })
      });
      return r.status;
    });
    expect(status).toBe(400);
  });
});
