import { test as base, Page } from '@playwright/test';

function b64url(obj: any) {
  return Buffer.from(JSON.stringify(obj)).toString('base64').replace(/=/g, '').replace(/\+/g, '-').replace(/\//g, '_');
}
export function fakeJwt(payload: any = {}) {
  const header = { alg: 'HS256', typ: 'JWT' };
  const now = Math.floor(Date.now() / 1000);
  const defaultPayload = { sub: '1', exp: now + 3600, iss: 'https://localhost', aud: 'https://localhost', ...payload };
  return `${b64url(header)}.${b64url(defaultPayload)}.fake-signature`;
}
export function expiredJwt() {
  return fakeJwt({ exp: Math.floor(Date.now() / 1000) - 3600 });
}

export async function mockRefreshEndpoint(page: Page, token: string) {
  await page.route('**/api/auth/refresh', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ token }),
    });
  });
  await page.route('**/api/auth/logout', async route => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ message: 'Logged out.' }) });
  });
}

export async function loginViaStorage(page: Page, token: string = fakeJwt()) {
  await page.addInitScript((t: string) => {
    localStorage.setItem('userName', 'Test User');
    localStorage.setItem('userId', '1');
  }, token);
  await mockRefreshEndpoint(page, token);
}

export const test = base;
export const expect = base.expect;
