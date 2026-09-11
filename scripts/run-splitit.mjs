import { spawn, spawnSync } from 'node:child_process';
import { copyFileSync, existsSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const mode = (process.argv[2] ?? 'all').toLowerCase();
const extraArgs = process.argv.slice(3);
const scriptDirectory = path.dirname(fileURLToPath(import.meta.url));
const rootDirectory = path.resolve(scriptDirectory, '..');
const frontendDirectory = path.join(rootDirectory, 'split-it-ui');
const backendProjectPath = path.join(rootDirectory, 'SplitIt.API', 'SplitIt.API', 'SplitIt.API.csproj');
const infraProjectPath = path.join(rootDirectory, 'SplitIt.API', 'SplitIt.Infrastructure', 'SplitIt.Infrastructure.csproj');
const envFilePath = path.join(rootDirectory, '.env');
const devConfigPath = path.join(rootDirectory, 'SplitIt.API', 'SplitIt.API', 'appsettings.Development.json');
const devConfigExamplePath = `${devConfigPath}.example`;
const npmCommand = process.platform === 'win32' ? 'npm.cmd' : 'npm';
const dockerCommand = process.platform === 'win32' ? 'docker.exe' : 'docker';

function parseDotEnv(filePath) {
  const values = {};
  for (const rawLine of readFileSync(filePath, 'utf8').split(/\r?\n/)) {
    const line = rawLine.trim();
    if (!line || line.startsWith('#')) continue;
    const eq = line.indexOf('=');
    if (eq < 0) continue;
    const key = line.slice(0, eq).trim();
    let value = line.slice(eq + 1).trim();
    if ((value.startsWith('"') && value.endsWith('"')) || (value.startsWith("'") && value.endsWith("'"))) {
      value = value.slice(1, -1);
    }
    values[key] = value;
  }
  return values;
}

function backendEnv() {
  if (!existsSync(devConfigPath) && existsSync(devConfigExamplePath)) {
    copyFileSync(devConfigExamplePath, devConfigPath);
    console.log('[setup] created SplitIt.API/SplitIt.API/appsettings.Development.json from the .example template.');
  }

  if (!existsSync(envFilePath)) {
    console.error('[setup] missing .env at the repo root. Copy .env.example to .env and fill DB_PASSWORD first.');
    process.exit(1);
  }
  const env = parseDotEnv(envFilePath);
  const saPassword = env.DB_PASSWORD;
  if (!saPassword || saPassword.startsWith('CHANGE_ME')) {
    console.error('[setup] .env has no usable DB_PASSWORD. Fill it in before running the API natively.');
    process.exit(1);
  }
  return {
    ASPNETCORE_ENVIRONMENT: 'Development',
    ConnectionStrings__DefaultConnection: `Server=localhost,1433;Database=SplitIt_Dev;User Id=sa;Password=${saPassword};TrustServerCertificate=True`
  };
}

function ensureDbUp() {
  const result = spawnSync(
    dockerCommand,
    ['compose', '-f', 'docker-compose.yml', '-f', 'docker-compose.local.yml', 'up', '-d', 'sqlserver'],
    { cwd: rootDirectory, stdio: 'inherit', shell: process.platform === 'win32' }
  );

  if (result.error ?? result.status !== 0) {
    console.error('[db] could not start the SQL Server container. Run `npm run db:up` manually.');
    process.exit(1);
  }
}

function runEfUpdate(dotnetEnv) {
  const result = spawnSync(
    'dotnet',
    ['ef', 'database', 'update', '--project', infraProjectPath, '--startup-project', backendProjectPath],
    { cwd: rootDirectory, env: { ...process.env, ...dotnetEnv }, stdio: 'inherit', shell: process.platform === 'win32' }
  );

  if (result.error ?? result.status !== 0) {
    console.error('[db] `dotnet ef database update` failed. Is the dotnet-ef tool installed? (`dotnet tool install -g dotnet-ef`)');
    process.exit(1);
  }
}

function createCommand(command, args, options = {}) {
  return {
    command,
    args,
    cwd: options.cwd ?? rootDirectory,
    env: options.env ?? {},
    label: options.label ?? command
  };
}

function buildCommands(selectedMode, dotnetEnv) {
  if (selectedMode === 'ui') {
    return [
      createCommand(npmCommand, ['--prefix', frontendDirectory, 'run', 'start', ...extraArgs], { label: 'ui' })
    ];
  }

  if (selectedMode === 'api') {
    return [
      createCommand('dotnet', ['run', '--launch-profile', 'http', '--project', backendProjectPath, ...extraArgs], {
        label: 'api',
        env: dotnetEnv
      })
    ];
  }

  if (selectedMode === 'all') {
    return [
      createCommand(npmCommand, ['--prefix', frontendDirectory, 'run', 'start', ...extraArgs], { label: 'ui' }),
      createCommand('dotnet', ['run', '--launch-profile', 'http', '--project', backendProjectPath, ...extraArgs], {
        label: 'api',
        env: dotnetEnv
      })
    ];
  }

  return null;
}

function writeOutput(label, chunk, stream = process.stdout) {
  const text = chunk.toString();
  const prefix = `[${label}] `;
  const formatted = text
    .split(/\r?\n/)
    .map((line, index, lines) => {
      if (!line && index === lines.length - 1) {
        return '';
      }

      return `${prefix}${line}`;
    })
    .join('\n');

  stream.write(formatted);
}

function runSingle(commandConfig) {
  return new Promise((resolve, reject) => {
    const child = spawn(commandConfig.command, commandConfig.args, {
      cwd: commandConfig.cwd,
      env: { ...process.env, ...commandConfig.env },
      stdio: ['inherit', 'pipe', 'pipe'],
      shell: false
    });

    child.stdout.on('data', chunk => writeOutput(commandConfig.label, chunk));
    child.stderr.on('data', chunk => writeOutput(commandConfig.label, chunk, process.stderr));

    child.on('error', reject);
    child.on('exit', (code, signal) => {
      if (signal) {
        reject(new Error(`${commandConfig.label} exited with signal ${signal}`));
        return;
      }

      if (code !== 0) {
        reject(new Error(`${commandConfig.label} exited with code ${code}`));
        return;
      }

      resolve();
    });
  });
}

function runCombined(commands) {
  const children = [];
  let resolved = false;

  const shutdown = signal => {
    for (const child of children) {
      if (!child.killed) {
        child.kill(signal);
      }
    }
  };

  const onSignal = signal => {
    shutdown(signal);
    process.exit(0);
  };

  process.on('SIGINT', onSignal);
  process.on('SIGTERM', onSignal);

  return new Promise((resolve, reject) => {
    let finished = 0;

    const finalize = error => {
      if (resolved) {
        return;
      }

      resolved = true;
      process.off('SIGINT', onSignal);
      process.off('SIGTERM', onSignal);
      shutdown();

      if (error) {
        reject(error);
        return;
      }

      resolve();
    };

    for (const commandConfig of commands) {
      const child = spawn(commandConfig.command, commandConfig.args, {
        cwd: commandConfig.cwd,
        env: { ...process.env, ...commandConfig.env },
        stdio: ['inherit', 'pipe', 'pipe'],
        shell: false
      });

      children.push(child);

      child.stdout.on('data', chunk => writeOutput(commandConfig.label, chunk));
      child.stderr.on('data', chunk => writeOutput(commandConfig.label, chunk, process.stderr));

      child.on('error', error => finalize(error));
      child.on('exit', (code, signal) => {
        if (signal) {
          finalize(new Error(`${commandConfig.label} exited with signal ${signal}`));
          return;
        }

        if (code !== 0) {
          finalize(new Error(`${commandConfig.label} exited with code ${code}`));
          return;
        }

        finished += 1;
        if (finished === commands.length) {
          finalize();
        }
      });
    }
  });
}

if (mode === 'migrate') {
  ensureDbUp();
  runEfUpdate(backendEnv());
  process.exit(0);
}

const commands = buildCommands(mode, {});

if (!commands) {
  console.error('Usage: node scripts/run-splitit.mjs <all|ui|api|migrate> [extra args]');
  process.exit(1);
}

if (mode !== 'ui') {
  const dotnetEnv = backendEnv();
  for (const commandConfig of commands) {
    if (commandConfig.label !== 'ui') {
      commandConfig.env = { ...commandConfig.env, ...dotnetEnv };
    }
  }
  ensureDbUp();
}

if (commands.length === 1) {
  await runSingle(commands[0]);
} else {
  await runCombined(commands);
}
