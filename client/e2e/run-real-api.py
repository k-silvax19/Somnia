"""Run browser auth against HTTPS + a disposable SQL database. Never use SomniaDb."""
import json
import os
from pathlib import Path
import secrets
import socket
import ssl
import subprocess
import tempfile
import time
import urllib.error
import urllib.request
import uuid

client = Path(__file__).resolve().parents[1]
api = client.parent / 'server' / 'src' / 'Api'
dll = api / 'bin' / 'Release' / 'net10.0' / 'Somnia.Api.dll'
if not dll.exists():
    raise SystemExit('Compile a API primeiro: dotnet build server/Somnia.slnx -c Release')

# Fail rather than send test registrations to an existing application.
for port in (7301, 4300):
    with socket.socket() as probe:
        try:
            probe.bind(('127.0.0.1', port))
        except OSError:
            raise SystemExit(f'Porta de teste {port} ocupada. Encerre o teste anterior.')

settings = json.loads((api / 'appsettings.Development.json').read_text(encoding='utf-8-sig'))
connection = settings['ConnectionStrings']['SqlServer']
parts = dict(part.split('=', 1) for part in connection.split(';') if '=' in part)
database = 'SomniaFrontendAuth_' + uuid.uuid4().hex
connection = connection.replace('Database=' + parts['Database'], 'Database=' + database)
env = os.environ.copy()
env.update({
    'ASPNETCORE_ENVIRONMENT': 'Development', 'DOTNET_ENVIRONMENT': 'Development',
    'ASPNETCORE_URLS': 'https://localhost:7301',
    'ConnectionStrings__SqlServer': connection,
    'Jwt__SigningKey': secrets.token_hex(32), 'MusicCatalog__Provider': 'Simulated',
    'Cors__AllowedOrigins__0': 'https://localhost:4300',
    'SOMNIA_REAL_API': '1',
})
opener = urllib.request.build_opener(urllib.request.HTTPSHandler(context=ssl._create_unverified_context()))
exit_code = 1
with tempfile.TemporaryFile(mode='w+b') as log:
    process = subprocess.Popen(['dotnet', str(dll)], cwd=api, env=env, stdout=log, stderr=log)
    try:
        for _ in range(120):
            if process.poll() is not None:
                raise RuntimeError('A API isolada encerrou antes da inicialização. Confira SQL e certificado .NET.')
            try:
                opener.open('https://localhost:7301/api/me', timeout=1)
            except urllib.error.HTTPError as response:
                if response.code == 401:
                    break
            except (OSError, urllib.error.URLError):
                pass
            time.sleep(.5)
        else:
            raise RuntimeError('Tempo de inicialização da API isolada excedido.')
        print('API HTTPS iniciada com banco descartável; executando navegador.', flush=True)
        command = ['node', str(client / 'node_modules' / '@playwright' / 'test' / 'cli.js'), 'test']
        exit_code = subprocess.run(command, cwd=client, env=env).returncode
    finally:
        process.terminate()
        try:
            process.wait(timeout=15)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait()
        # Only this generated database is eligible for deletion, never the configured database.
        assert database.startswith('SomniaFrontendAuth_') and len(database) == 51
        sql_env = os.environ.copy()
        sql_env['SQLCMDPASSWORD'] = parts['Password']
        query = f"IF DB_ID('{database}') IS NOT NULL BEGIN ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}]; END"
        cleanup = subprocess.run([
            'docker', 'exec', '-e', 'SQLCMDPASSWORD', 'notekeeper-sqlserver-1',
            '/opt/mssql-tools18/bin/sqlcmd', '-S', 'localhost', '-U', parts['User Id'],
            '-C', '-b', '-Q', query,
        ], env=sql_env, capture_output=True)
        if cleanup.returncode:
            raise RuntimeError(f'Não foi possível remover o banco temporário {database}.')
        print('API isolada encerrada e banco temporário removido.', flush=True)
raise SystemExit(exit_code)
