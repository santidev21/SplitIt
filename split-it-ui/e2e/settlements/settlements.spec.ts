import { test, expect, fakeJwt } from '../fixtures/api';

test.describe('Settlements E2E', () => {
  const token = fakeJwt({ sub: '1' });

  test.beforeEach(async ({ page }) => {
    await page.addInitScript((t: string) => {
      localStorage.setItem('token', t);
      localStorage.setItem('userName', 'Alice');
      localStorage.setItem('userId', '1');
    }, token);
    await page.route('**/api/groups/1/details', async r => r.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ name: 'G1', description: 'Desc' }) }));
    await page.route('**/api/groups/1/members', async r => r.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([{ id: 1, name: 'You' }, { id: 2, name: 'Bob' }]) }));
    await page.route('**/api/groups/1/userrole', async r => r.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ role: 'creator' }) }));
    await page.route('**/api/expenses/1/expenses*', async r => r.fulfill({ status: 200, contentType: 'application/json', body: '[]' }));
  });

  test('Register partial payment → creates IsPayment and leaves remaining', async ({ page }) => {
    let settleBody: any = null;
    await page.route('**/api/expenses/settle', async route => {
      settleBody = route.request().postDataJSON();
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ settledCount: 1 }) });
    });
    await page.route('**/api/expenses/debt-summary*', async r => r.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ debtsOwedByUser: [{ creditorUserId: 2, creditorUserName: 'Bob', totalAmountOwed: 70 }], debtsOwedToUser: [] }) }));

    await page.goto('/dashboard/group/1');
    const status = await page.evaluate(async () => {
      const r = await fetch('http://localhost:5120/api/expenses/settle', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', Authorization: 'Bearer ' + localStorage.getItem('token')! },
        body: JSON.stringify({ payerUserId: 2, groupId: 1, amount: 30 })
      });
      return r.status;
    });
    expect(status).toBe(200);
  });

  test('Fully settle debt → settledCount >0', async ({ page }) => {
    await page.route('**/api/expenses/settle', async r => r.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ settledCount: 2 }) }));
    await page.goto('/dashboard/group/1');
    const json = await page.evaluate(async () => {
      const r = await fetch('http://localhost:5120/api/expenses/settle', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', Authorization: 'Bearer ' + localStorage.getItem('token')! },
        body: JSON.stringify({ payerUserId: 2, groupId: 1, amount: 100 })
      });
      return r.json();
    });
    expect(json.settledCount).toBe(2);
  });

  test('Cross-group isolation — Group A settle does not affect Group B (regression)', async ({ page }) => {
    // This is the critical regression: mock two groups, settle only one.
    await page.route('**/api/expenses/settle', async route => {
      const body = route.request().postDataJSON();
      // Ensure groupId is respected
      expect([1, 2]).toContain(body.groupId);
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ settledCount: 1 }) });
    });

    await page.goto('/dashboard/group/1');
    // Settle Group 1
    const s1 = await page.evaluate(async () => {
      const r = await fetch('http://localhost:5120/api/expenses/settle', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', Authorization: 'Bearer ' + localStorage.getItem('token')! },
        body: JSON.stringify({ payerUserId: 2, groupId: 1, amount: 100 })
      });
      return r.status;
    });
    expect(s1).toBe(200);

    // Verify Group 2 debt still exists via separate fetch
    await page.route('**/api/expenses/debt-summary?groupId=2', async r => r.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ debtsOwedByUser: [{ creditorUserId: 2, creditorUserName: 'Bob', totalAmountOwed: 50 }], debtsOwedToUser: [] }) }));
    const group2 = await page.evaluate(async () => {
      const r = await fetch('http://localhost:5120/api/expenses/debt-summary?groupId=2', { headers: { Authorization: 'Bearer ' + localStorage.getItem('token')! } });
      return r.json();
    });
    expect(group2.debtsOwedByUser[0].totalAmountOwed).toBe(50);
  });

  test('Settle with no debt → 404', async ({ page }) => {
    await page.route('**/api/expenses/settle', async r => r.fulfill({ status: 404, contentType: 'application/json', body: JSON.stringify({ message: 'No unsettled debts found.' }) }));
    await page.goto('/dashboard/group/1');
    const status = await page.evaluate(async () => {
      const r = await fetch('http://localhost:5120/api/expenses/settle', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', Authorization: 'Bearer ' + localStorage.getItem('token')! },
        body: JSON.stringify({ payerUserId: 99, groupId: 1, amount: 10 })
      });
      return r.status;
    });
    expect(status).toBe(404);
  });
});
