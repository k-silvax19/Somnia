import { spawnSync } from 'node:child_process';
import { chmodSync, mkdirSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

const certificateDirectory = fileURLToPath(new URL('../.certs/', import.meta.url));
const certificatePath = fileURLToPath(new URL('../.certs/localhost.pem', import.meta.url));
const keyPath = fileURLToPath(new URL('../.certs/localhost.key', import.meta.url));

mkdirSync(certificateDirectory, { recursive: true, mode: 0o700 });
const result = spawnSync(
  'dotnet',
  [
    'dev-certs',
    'https',
    '--trust',
    '--export-path',
    certificatePath,
    '--format',
    'Pem',
    '--no-password',
  ],
  { stdio: 'inherit', windowsHide: true },
);

if (result.error || result.status !== 0) {
  console.error(
    'Não foi possível preparar o HTTPS. Verifique o SDK .NET e a confiança do certificado local.',
  );
  process.exit(1);
}

// The private key stays on this machine; .certs is excluded from Git.
chmodSync(keyPath, 0o600);
console.log('HTTPS local preparado para https://localhost:4200.');
