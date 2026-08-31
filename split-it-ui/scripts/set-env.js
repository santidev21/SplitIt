const fs = require('fs');
const path = require('path');

function parseEnvFile(file) {
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

const envPath = path.resolve(__dirname, '..', '..', '.env');
const fileEnv = parseEnvFile(envPath);
const googleClientId = process.env.GOOGLE_CLIENT_ID || fileEnv.GOOGLE_CLIENT_ID || '';

function writeEnv(file, apiUrl, production) {
  const content = `export const environment = {
  production: ${production},
  apiUrl: '${apiUrl}',
  googleClientId: '${googleClientId}',
};
`;
  fs.writeFileSync(file, content);
}

const envDir = path.resolve(__dirname, '..', 'src', 'environments');
writeEnv(path.join(envDir, 'environment.ts'), 'http://localhost:5120/api', false);
writeEnv(path.join(envDir, 'environment.prod.ts'), '/api', true);
console.log('environment.ts and environment.prod.ts generated');
