const ROLE_CLAIM = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';

export function getRoleFromToken(): string | null {
  if (typeof localStorage === 'undefined') return null;
  const token = localStorage.getItem('token');
  if (!token) return null;
  try {
    let base64 = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
    while (base64.length % 4 !== 0) {
      base64 += '=';
    }
    const bytes = Uint8Array.from(atob(base64), ch => ch.charCodeAt(0));
    const payload = JSON.parse(new TextDecoder().decode(bytes));
    return payload[ROLE_CLAIM] || payload['role'] || null;
  } catch {
    return null;
  }
}

export function isAdminRole(): boolean {
  const role = getRoleFromToken();
  return role === 'SuperAdmin' || role === 'Admin';
}

export function isSuperAdminRole(): boolean {
  return getRoleFromToken() === 'SuperAdmin';
}

export function getCurrentUserId(): number {
  if (typeof localStorage === 'undefined') return 0;
  return Number(localStorage.getItem('userId')) || 0;
}
