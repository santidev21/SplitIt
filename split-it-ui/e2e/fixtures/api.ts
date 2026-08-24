import { test as base } from '@playwright/test';

// Helper to generate a fake JWT (alg HS256, no signature verification in mocked E2E)
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

// Storage helpers
export async function loginViaStorage(page: any, token: string = fakeJwt()) {
  await page.addInitScript((t: string) => {
    localStorage.setItem('token', t);
    localStorage.setItem('userName', 'Test User');
    localStorage.setItem('userId', '1');
  }, token);
}

export const test = base;
export const expect = base.expect;
