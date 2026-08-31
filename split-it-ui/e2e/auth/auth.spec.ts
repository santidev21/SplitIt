import { test, expect, fakeJwt, expiredJwt, mockRefreshEndpoint, loginViaStorage } from '../fixtures/api';

test.describe('Auth E2E', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/auth/login');
  });

  test('should display login form', async ({ page }) => {
    await expect(page.getByText('Login to your account')).toBeVisible();
    await expect(page.getByPlaceholder('example@example.com')).toBeVisible();
    await expect(page.getByPlaceholder('Enter your password')).toBeVisible();
  });

  test('Register → should redirect to dashboard', async ({ page }) => {
    const token = fakeJwt({ sub: '42', exp: Math.floor(Date.now()/1000)+3600 });
    await page.route('**/api/auth/register', async route => {
      const json = { token, userName: 'Alice', userId: 42 };
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(json) });
    });
    await mockRefreshEndpoint(page, token);
    await page.goto('/auth/register');
    await page.getByPlaceholder('Enter your name').fill('Alice');
    await page.getByPlaceholder('example@example.com').fill('alice@test.com');
    await page.getByPlaceholder('Enter your password').fill('StrongPass123!');
    await page.getByRole('button', { name: 'Register' }).click();
    await expect(page).toHaveURL(/\/dashboard/, { timeout: 5000 });
  });

  test('Login with valid credentials → redirects to dashboard', async ({ page }) => {
    const token = fakeJwt({ sub: '1' });
    await page.route('**/api/auth/login', async route => {
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ token, userName: 'Bob', userId: 1 }) });
    });
    await mockRefreshEndpoint(page, token);
    await page.getByPlaceholder('example@example.com').fill('bob@test.com');
    await page.getByPlaceholder('Enter your password').fill('ValidPass123!');
    await page.getByRole('button', { name: 'Login now' }).click();
    await expect(page).toHaveURL(/\/dashboard/, { timeout: 5000 });
  });

  test('Login with invalid credentials → stays on login, no token', async ({ page }) => {
    await page.route('**/api/auth/login', async route => {
      await route.fulfill({ status: 401, contentType: 'application/json', body: JSON.stringify({ error: 'Invalid credentials' }) });
    });
    await page.getByPlaceholder('example@example.com').fill('evil@test.com');
    await page.getByPlaceholder('Enter your password').fill('wrong');
    await page.getByRole('button', { name: 'Login now' }).click();
    await page.waitForTimeout(500);
    await expect(page).toHaveURL(/\/auth\/login/);
  });

  test('Logout → clears session and redirects to login', async ({ page }) => {
    const token = fakeJwt();
    await loginViaStorage(page, token);
    await page.route('**/api/groups/user/*', async route => route.fulfill({ status: 200, contentType: 'application/json', body: '[]' }));
    await page.route('**/api/users*', async route => route.fulfill({ status: 200, contentType: 'application/json', body: '[]' }));
    await page.goto('/dashboard/home');
    await page.waitForTimeout(500);
    await page.evaluate(() => {
      localStorage.removeItem('userName');
      localStorage.removeItem('userId');
    });
    await page.goto('/auth/login');
    await expect(page).toHaveURL(/\/auth\/login/);
  });

  test('Expired session → redirect to /auth/login', async ({ page }) => {
    const expired = expiredJwt();
    await mockRefreshEndpoint(page, expired);
    await page.goto('/dashboard/home');
    await expect(page).toHaveURL(/\/auth\/login/, { timeout: 5000 });
  });

  test('Unauthorized route → /dashboard/home without token redirects to login', async ({ page }) => {
    await page.goto('/dashboard/home');
    await expect(page).toHaveURL(/\/auth\/login/);
  });

  test('Unauthorized route with returnUrl preserved', async ({ page }) => {
    await page.goto('/dashboard/group/123');
    await expect(page).toHaveURL(/\/auth\/login\?returnUrl=/);
  });
});
