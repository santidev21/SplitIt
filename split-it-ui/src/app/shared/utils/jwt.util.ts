const ROLE_CLAIM = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';

export function decodeRoleFromToken(token: string): string | null {
  try {
    let base64 = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
    while (base64.length % 4 !== 0) base64 += '=';
    const bytes = Uint8Array.from(atob(base64), ch => ch.charCodeAt(0));
    const payload = JSON.parse(new TextDecoder().decode(bytes));
    return payload[ROLE_CLAIM] || payload['role'] || null;
  } catch {
    return null;
  }
}

export function isTokenExpired(token: string): boolean {
  try {
    const payload = JSON.parse(atob(token.split('.')[1]));
    if (!payload.exp) return true;
    const now = Math.floor(Date.now() / 1000);
    return payload.exp < now - 30;
  } catch {
    return true;
  }
}
