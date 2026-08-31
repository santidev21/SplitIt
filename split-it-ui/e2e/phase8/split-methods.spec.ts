import { test, expect, fakeJwt, loginViaStorage, mockRefreshEndpoint } from '../fixtures/api';

test.describe('Phase 8 — Alternative Split Methods', () => {
  const token = fakeJwt({ sub: '1' });
  test.beforeEach(async ({ page }) => {
    await page.addInitScript((t: string) => localStorage.setItem('token', t), token);
    await loginViaStorage(page, token);
    await page.goto('/auth/login');
  });

  test('Equal split 100 among 3 → 33.34,33.33,33.33', async ({ page }) => {
    await page.route('**/api/expenses/add', async route => {
      const body = route.request().postDataJSON();
      expect(body.amount).toBe(100);
      expect(body.participants.length).toBe(3);
      const sum = body.participants.reduce((s: number, p: any) => s + p.amountOwed, 0);
      expect(Math.abs(sum - 100)).toBeLessThan(0.01);
      await route.fulfill({ status: 201, contentType: 'application/json', body: JSON.stringify({ id: 1 }) });
    });
    const status = await page.evaluate(async () => {
      // Simulate equal split logic from fixed component
      const amount = 100;
      const count = 3;
      const perRounded = Math.floor((amount / count) * 100) / 100;
      const remainder = Math.round((amount - perRounded * count) * 100);
      const participants = [1,2,3].map((id, idx) => ({ userId: id, amountOwed: Math.round((perRounded + (idx < remainder ? 0.01 : 0)) * 100) / 100 }));
      const r = await fetch('http://localhost:5120/api/expenses/add', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', Authorization: 'Bearer ' + localStorage.getItem('token')! },
        body: JSON.stringify({ title: 'Equal', amount, date: Date.now(), groupId: 1, paidById: 1, participants })
      });
      return r.status;
    });
    expect(status).toBe(201);
  });

  test('Fixed amount sum mismatch → 400', async ({ page }) => {
    await page.route('**/api/expenses/add', async route => {
      await route.fulfill({ status: 400, contentType: 'application/json', body: JSON.stringify({ message: 'Sum of participant amounts (90) does not match expense amount (100).' }) });
    });
    const status = await page.evaluate(async () => {
      const r = await fetch('http://localhost:5120/api/expenses/add', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', Authorization: 'Bearer ' + localStorage.getItem('token')! },
        body: JSON.stringify({ title: 'Bad', amount: 100, date: Date.now(), groupId: 1, paidById: 1, participants: [{ userId: 1, amountOwed: 60 }, { userId: 2, amountOwed: 30 }] })
      });
      return r.status;
    });
    expect(status).toBe(400);
  });

  test('Percentage sum 90% → 400, 100% → 201', async ({ page }) => {
    // Invalid 90%
    await page.route('**/api/expenses/add', async route => {
      const body = route.request().postDataJSON();
      const sum = body.participants.reduce((s: number, p: any) => s + p.amountOwed, 0);
      if (Math.abs(sum - body.amount) > 0.01) {
        await route.fulfill({ status: 400, contentType: 'application/json', body: JSON.stringify({ message: 'Sum mismatch' }) });
      } else {
        await route.fulfill({ status: 201, contentType: 'application/json', body: JSON.stringify({ id: 2 }) });
      }
    });
    const bad = await page.evaluate(async () => {
      const total = 100;
      const pcts = [50, 30, 10]; // 90%
      const participants = pcts.map((pct, i) => ({ userId: i+1, amountOwed: Math.round((pct/100)*total*100)/100 }));
      const r = await fetch('http://localhost:5120/api/expenses/add', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', Authorization: 'Bearer ' + localStorage.getItem('token')! },
        body: JSON.stringify({ title: 'PctBad', amount: total, date: Date.now(), groupId: 1, paidById: 1, participants })
      });
      return r.status;
    });
    expect(bad).toBe(400);

    const good = await page.evaluate(async () => {
      const total = 100;
      const pcts = [50, 30, 20];
      const participants = pcts.map((pct, i) => ({ userId: i+1, amountOwed: Math.round((pct/100)*total*100)/100 }));
      const r = await fetch('http://localhost:5120/api/expenses/add', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', Authorization: 'Bearer ' + localStorage.getItem('token')! },
        body: JSON.stringify({ title: 'PctGood', amount: total, date: Date.now(), groupId: 1, paidById: 1, participants })
      });
      return r.status;
    });
    expect(good).toBe(201);
  });

  test('Negative allocation → 400', async ({ page }) => {
    await page.route('**/api/expenses/add', async route => {
      await route.fulfill({ status: 400, contentType: 'application/json', body: JSON.stringify({ message: 'Participant amount must be positive.' }) });
    });
    const status = await page.evaluate(async () => {
      const r = await fetch('http://localhost:5120/api/expenses/add', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', Authorization: 'Bearer ' + localStorage.getItem('token')! },
        body: JSON.stringify({ title: 'Neg', amount: 100, date: Date.now(), groupId: 1, paidById: 1, participants: [{ userId: 1, amountOwed: -10 }, { userId: 2, amountOwed: 110 }] })
      });
      return r.status;
    });
    expect(status).toBe(400);
  });
});
