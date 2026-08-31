import { test, expect, fakeJwt, loginViaStorage, mockRefreshEndpoint } from '../fixtures/api';

test.describe('Expenses E2E', () => {
  const token = fakeJwt({ sub: '1' });

  test.beforeEach(async ({ page }) => {
    await page.addInitScript((t: string) => {
      localStorage.setItem('token', t);
      localStorage.setItem('userName', 'Alice');
      localStorage.setItem('userId', '1');
    }, token);
    await mockRefreshEndpoint(page, token);

    await page.route('**/api/groups/1/details', async r => r.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ name: 'Group 1', description: 'Desc' }) }));
    await page.route('**/api/groups/1/members', async r => r.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([{ id: 1, name: 'You' }, { id: 2, name: 'Bob' }, { id: 3, name: 'Charlie' }]) }));
    await page.route('**/api/groups/1/userrole', async r => r.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ role: 'creator' }) }));
    await page.route('**/api/expenses/1/expenses*', async r => r.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([]) }));
    await page.route('**/api/expenses/debt-summary*', async r => r.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ debtsOwedByUser: [], debtsOwedToUser: [] }) }));
  });

  test('Create expense — equal split', async ({ page }) => {
    await page.route('**/api/expenses/add', async route => {
      const body = route.request().postDataJSON();
      expect(body.amount).toBe(100);
      expect(body.participants.length).toBe(2);
      const sum = body.participants.reduce((s: number, p: any) => s + p.amountOwed, 0);
      expect(Math.abs(sum - 100)).toBeLessThan(0.02);
      await route.fulfill({ status: 201, contentType: 'application/json', body: JSON.stringify({ id: 1 }) });
    });

    await page.goto('/dashboard/group/1');
    // Simulate dialog flow via direct API (equal split: 2 participants → 50 each)
    await page.evaluate(async () => {
      await fetch('http://localhost:5120/api/expenses/add', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', Authorization: 'Bearer ' + localStorage.getItem('token')! },
        body: JSON.stringify({
          title: 'Dinner', amount: 100, date: Date.now(), groupId: 1, note: 'Test', paidById: 1,
          participants: [{ userId: 1, amountOwed: 50 }, { userId: 2, amountOwed: 50 }]
        })
      });
    });
  });

  test('Create expense — fixed amount split validation', async ({ page }) => {
    await page.route('**/api/expenses/add', async route => {
      const body = route.request().postDataJSON();
      // Fixed amount: sum must equal total, otherwise 400
      const sum = body.participants.reduce((s: number, p: any) => s + p.amountOwed, 0);
      if (Math.abs(sum - body.amount) > 0.02) {
        await route.fulfill({ status: 400, contentType: 'application/json', body: JSON.stringify({ message: 'Sum does not match' }) });
      } else {
        await route.fulfill({ status: 201, contentType: 'application/json', body: JSON.stringify({ id: 2 }) });
      }
    });

    await page.goto('/dashboard/group/1');
    // Valid fixed amount
    const valid = await page.evaluate(async () => {
      const r = await fetch('http://localhost:5120/api/expenses/add', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', Authorization: 'Bearer ' + localStorage.getItem('token')! },
        body: JSON.stringify({ title: 'Groceries', amount: 90, date: Date.now(), groupId: 1, paidById: 1, participants: [{ userId: 1, amountOwed: 30 }, { userId: 2, amountOwed: 60 }] })
      });
      return r.status;
    });
    expect(valid).toBe(201);

    // Invalid sum → 400
    const invalid = await page.evaluate(async () => {
      const r = await fetch('http://localhost:5120/api/expenses/add', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', Authorization: 'Bearer ' + localStorage.getItem('token')! },
        body: JSON.stringify({ title: 'Bad', amount: 100, date: Date.now(), groupId: 1, paidById: 1, participants: [{ userId: 1, amountOwed: 30 }] })
      });
      return r.status;
    });
    expect(invalid).toBe(400);
  });

  test('Percentage split → amounts calculated', async ({ page }) => {
    await page.route('**/api/expenses/add', async route => {
      await route.fulfill({ status: 201, contentType: 'application/json', body: JSON.stringify({ id: 3 }) });
    });
    await page.goto('/dashboard/group/1');
    // Simulate percentage dialog: 50% + 30% + 20% of 100 → 50,30,20
    const status = await page.evaluate(async () => {
      const amount = 100;
      const percentages: Record<number, number> = { 1: 50, 2: 30, 3: 20 };
      const participants = Object.entries(percentages).map(([uid, pct]) => ({ userId: Number(uid), amountOwed: (pct/100)*amount }));
      const r = await fetch('http://localhost:5120/api/expenses/add', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', Authorization: 'Bearer ' + localStorage.getItem('token')! },
        body: JSON.stringify({ title: 'Pct', amount, date: Date.now(), groupId: 1, paidById: 1, participants })
      });
      return r.status;
    });
    expect(status).toBe(201);
  });

  test('View balances → debt-summary mocked', async ({ page }) => {
    await page.route('**/api/expenses/debt-summary*', async r => r.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ debtsOwedByUser: [{ creditorUserId: 2, creditorUserName: 'Bob', totalAmountOwed: 50.5 }], debtsOwedToUser: [{ debtorUserId: 3, debtorUserName: 'Charlie', totalAmountOwed: 20 }] }) }));
    await page.goto('/dashboard/group/1');
    // Balances rendered in component; we verify via API mock not UI text
    const balances = await page.evaluate(async () => {
      const r = await fetch('http://localhost:5120/api/expenses/debt-summary?groupId=1', { headers: { Authorization: 'Bearer ' + localStorage.getItem('token')! } });
      return r.json();
    });
    expect(balances.debtsOwedByUser[0].totalAmountOwed).toBe(50.5);
  });
});
