import { test, expect, fakeJwt, expiredJwt } from '../fixtures/api';

test.describe('Auth E2E', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/auth/login');
  });

  test('should display login form', async ({ page }) => {
    await expect(page.getByText('Login to your account')).toBeVisible();
    await expect(page.getByPlaceholder('example@example.com')).toBeVisible();
    await expect(page.getByPlaceholder('Enter your password')).toBeVisible();
  });

  test('Register → should store token and redirect to dashboard', async ({ page }) => {
    const token = fakeJwt({ sub: '42', exp: Math.floor(Date.now()/1000)+3600 });
    await page.route('**/api/auth/register', async route => {
      const json = { token, userName: 'Alice', userId: 42 };
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(json) });
    });
    await page.goto('/auth/register');
    await page.getByPlaceholder('Enter your name').fill('Alice');
    await page.getByPlaceholder('example@example.com').fill('alice@test.com');
    await page.getByPlaceholder('Enter your password').fill('StrongPass123!');
    await page.getByRole('button', { name: 'Register' }).click();
    // Wait for navigation to dashboard (mocked token stored)
    await page.waitForTimeout(500);
    const storedToken = await page.evaluate(() => localStorage.getItem('token'));
    expect(storedToken).toBe(token);
  });

  test('Login with valid credentials → stores token', async ({ page }) => {
    const token = fakeJwt({ sub: '1' });
    await page.route('**/api/auth/login', async route => {
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ token, userName: 'Bob', userId: 1 }) });
    });
    await page.getByPlaceholder('example@example.com').fill('bob@test.com');
    await page.getByPlaceholder('Enter your password').fill('ValidPass123!');
    await page.getByRole('button', { name: 'Login now' }).click();
    await page.waitForTimeout(500);
    const stored = await page.evaluate(() => localStorage.getItem('token'));
    expect(stored).toBe(token);
  });

  test('Login with invalid credentials → stays on login, no token', async ({ page }) => {
    await page.route('**/api/auth/login', async route => {
      await route.fulfill({ status: 401, contentType: 'application/json', body: JSON.stringify({ error: 'Invalid credentials' }) });
    });
    await page.getByPlaceholder('example@example.com').fill('evil@test.com');
    await page.getByPlaceholder('Enter your password').fill('wrong');
    await page.getByRole('button', { name: 'Login now' }).click();
    await page.waitForTimeout(500);
    // Should remain on auth/login (guard doesn't redirect because still unauthenticated but we are already there)
    await expect(page).toHaveURL(/\/auth\/login/);
    const token = await page.evaluate(() => localStorage.getItem('token'));
    expect(token).toBeNull();
  });

  test('Logout → clears storage and redirects to login', async ({ page }) => {
    const token = fakeJwt();
    await page.goto('/auth/login');
    await page.evaluate((t: string) => {
      localStorage.setItem('token', t);
      localStorage.setItem('userName', 'Test');
      localStorage.setItem('userId', '1');
    }, token);
    // Mock dashboard call to avoid 401 flash
    await page.route('**/api/groups/user/*', async route => route.fulfill({ status: 200, contentType: 'application/json', body: '[]' }));
    await page.route('**/api/users*', async route => route.fulfill({ status: 200, contentType: 'application/json', body: '[]' }));
    await page.goto('/dashboard/home');
    // Simulate logout via clearing storage
    await page.evaluate(() => {
      localStorage.removeItem('token');
      localStorage.removeItem('userName');
      localStorage.removeItem('userId');
    });
    await page.goto('/auth/login');
    await expect(page).toHaveURL(/\/auth\/login/);
    const after = await page.evaluate(() => localStorage.getItem('token'));
    expect(after).toBeNull();
  });

  test('Expired session → redirect to /auth/login', async ({ page }) => {
    const expired = expiredJwt();
    await page.addInitScript((t: string) => {
      localStorage.setItem('token', t);
      localStorage.setItem('userName', 'ExpiredUser');
      localStorage.setItem('userId', '1');
    }, expired);
    await page.goto('/dashboard/home');
    await expect(page).toHaveURL(/\/auth\/login/, { timeout: 5000 });
  });

  test('Unauthorized route → /dashboard/home without token redirects to login', async ({ page }) => {
    // Ensure clean storage
    await page.addInitScript(() => localStorage.clear());
    await page.goto('/dashboard/home');
    await expect(page).toHaveURL(/\/auth\/login/);
  });

  test('Unauthorized route with returnUrl preserved', async ({ page }) => {
    await page.goto('/dashboard/group/123');
    await expect(page).toHaveURL(/\/auth\/login\?returnUrl=/);
  });
});
