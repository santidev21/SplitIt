const fs = require('fs');
const path = require('path');

const envPath = path.resolve(__dirname, '..', '..', '.env');
const envFile = path.resolve(__dirname, '..', 'src', 'environments', 'environment.ts');

function parseEnv(file) {
  if (!fs.existsSync(file)) return {};
  const lines = fs.readFileSync(file, 'utf-8').split('\n');
  const env = {};
  for (const line of lines) {
    const trimmed = line.trim();
    if (!trimmed || trimmed.startsWith('#')) continue;
    const idx = trimmed.indexOf('=');
    if (idx === -1) continue;
    const key = trimmed.substring(0, idx).trim();
    let value = trimmed.substring(idx + 1).trim();
    if ((value.startsWith('"') && value.endsWith('"')) || (value.startsWith("'") && value.endsWith("'"))) {
      value = value.slice(1, -1);
    }
    env[key] = value;
  }
  return env;
}

const env = parseEnv(envPath);

const content = `export const environment = {
  production: false,
  apiUrl: 'http://localhost:5120/api',
  googleClientId: '${env.GOOGLE_CLIENT_ID || ''}',
};
`;

fs.writeFileSync(envFile, content);
console.log('environment.ts generated from .env');
