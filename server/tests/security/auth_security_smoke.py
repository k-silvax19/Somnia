"""HTTPS/SQL security regression checks. Requires a Release build, Docker SQL and a .NET development certificate."""
import base64
import concurrent.futures
import json
import os
import secrets
import socket
import ssl
import subprocess
import tempfile
import time
import urllib.error
import urllib.request
import uuid
from http.cookies import SimpleCookie
from pathlib import Path

api = Path(__file__).resolve().parents[2] / 'src' / 'Api'
connection = json.loads((api / 'appsettings.Development.json').read_text(encoding='utf-8-sig'))['ConnectionStrings']['SqlServer']
parts = dict(part.split('=', 1) for part in connection.split(';') if '=' in part)
database = 'SomniaAuthSecurity_' + uuid.uuid4().hex
connection = connection.replace('Database=' + parts['Database'], 'Database=' + database)
with socket.socket() as sock:
    sock.bind(('127.0.0.1', 0))
    port = sock.getsockname()[1]
with socket.socket() as sock:
    sock.bind(('127.0.0.1', 0))
    http_port = sock.getsockname()[1]
env = os.environ.copy()
env.update(ASPNETCORE_ENVIRONMENT='Development', ASPNETCORE_URLS=f'https://localhost:{port};http://localhost:{http_port}',
           ConnectionStrings__SqlServer=connection, Jwt__SigningKey=secrets.token_hex(32), Jwt__ExpiresInMinutes='15')
# Only the isolated loopback test client bypasses trust of the development certificate.
opener = urllib.request.build_opener(urllib.request.HTTPSHandler(context=ssl._create_unverified_context()))
log = tempfile.TemporaryFile(mode='w+b')
process = subprocess.Popen(['dotnet', str(api / 'bin/Release/net10.0/Somnia.Api.dll')],
                           cwd=api, env=env, stdout=log, stderr=log)


def sql(query):
    sql_env = os.environ.copy()
    sql_env['SQLCMDPASSWORD'] = parts['Password']
    result = subprocess.run(['docker', 'exec', '-e', 'SQLCMDPASSWORD', 'notekeeper-sqlserver-1',
                             '/opt/mssql-tools18/bin/sqlcmd', '-S', 'localhost', '-U', parts['User Id'],
                             '-C', '-b', '-Q', query], env=sql_env, capture_output=True, text=True)
    if result.returncode:
        raise RuntimeError('Isolated database operation failed: ' + result.stdout + result.stderr)
    return result.stdout


def request(method, path, body=None, session=None, csrf=True, origin=None, client_header=True):
    headers = {'Content-Type': 'application/json', 'Origin': origin or f'https://localhost:{port}'}
    if client_header:
        headers['X-Somnia-Client'] = 'web'
    if session:
        headers['Cookie'] = session.get('cookies', '')
        if session.get('accessToken'):
            headers['Authorization'] = 'Bearer ' + session['accessToken']
        if csrf:
            headers['X-CSRF-Token'] = session.get('csrfToken', '')
    req = urllib.request.Request(f'https://localhost:{port}' + path,
                                 data=json.dumps(body).encode() if body is not None else None,
                                 headers=headers, method=method)
    try:
        response = opener.open(req, timeout=20)
    except urllib.error.HTTPError as error:
        response = error
    content = response.read()
    payload = json.loads(content) if content and 'json' in response.headers.get('Content-Type', '') else {}
    if response.status == 200 and path in ('/api/auth/login', '/api/auth/refresh'):
        assert 'refreshToken' not in payload, 'Refresh token leaked in JSON'
        cookies = SimpleCookie()
        for header in response.headers.get_all('Set-Cookie', []):
            cookies.load(header)
        refresh = cookies['__Host-somnia-refresh']
        assert refresh['secure'] and refresh['httponly'] and refresh['samesite'].lower() == 'strict'
        assert response.headers.get('Cache-Control') and 'no-store' in response.headers['Cache-Control']
        payload['cookies'] = '; '.join(f'{name}={morsel.value}' for name, morsel in cookies.items())
    return response.status, payload


def login():
    status, session = request('POST', '/api/auth/login', {'email': 'security@example.com', 'password': 'Security123!@'})
    assert status == 200, f'Login failed: {status}'
    return session


try:
    for attempt in range(160):
        if process.poll() is not None:
            log.seek(0)
            raise RuntimeError('API startup failed: ' + log.read().decode(errors='replace')[-3000:])
        try:
            if request('GET', '/openapi/v1.json')[0] == 200:
                break
        except OSError:
            pass
        time.sleep(0.25)
    else:
        raise RuntimeError('API did not start')

    class NoRedirect(urllib.request.HTTPRedirectHandler):
        def redirect_request(self, req, fp, code, msg, headers, newurl):
            return None

    http_client = urllib.request.build_opener(NoRedirect())
    try:
        http_response = http_client.open(f'http://localhost:{http_port}/swagger/index.html', timeout=10)
    except urllib.error.HTTPError as error:
        http_response = error
    assert http_response.status == 307, 'Swagger is served over HTTP instead of redirecting to HTTPS'
    assert http_response.headers['Location'] == f'https://localhost:{port}/swagger/index.html'

    registration = {'email': 'security@example.com', 'username': 'security', 'password': 'Security123!@', 'displayName': 'Security'}
    assert request('POST', '/api/auth/register', registration)[0] == 201
    original = login()
    assert request('GET', '/api/me', session=original)[0] == 200
    mine = request('GET', '/api/me', session=original)[1]
    assert mine['displayName'] == 'Security'
    assert mine['defaultScoreVisibility'] == mine['defaultReviewVisibility'] == 'private'
    assert request('GET', '/api/users/security')[0] == 401
    assert request('PATCH', '/api/me/profile', {'bio': 'anonymous'})[0] == 401
    bob_registration = dict(registration, email='bob@example.com', username='bob', displayName='Bob')
    assert request('POST', '/api/auth/register', bob_registration)[0] == 201
    bob_status, bob = request('POST', '/api/auth/login', {'email': 'bob@example.com', 'password': registration['password']})
    assert bob_status == 200
    status, updated = request('PATCH', '/api/me/profile', {'bio': 'Minha bio', 'defaultScoreVisibility': 'public'}, session=original)
    assert status == 200 and updated['bio'] == 'Minha bio'
    assert updated['defaultScoreVisibility'] == 'public' and updated['defaultReviewVisibility'] == 'private'
    status, public = request('GET', '/api/users/SECURITY', session=bob)
    assert status == 200 and public['bio'] == 'Minha bio'
    assert set(public) == {'username', 'displayName', 'bio', 'avatarUrl'}, 'Private profile fields leaked'
    assert request('PATCH', '/api/me/profile', {'displayName': 'Bob atualizado'}, session=bob)[0] == 200
    assert request('GET', '/api/me', session=original)[1]['displayName'] == 'Security', 'Another user changed the owner'
    assert request('PATCH', '/api/me/profile', {'usuarioId': mine['id'], 'displayName': 'Attack'}, session=bob)[0] == 400
    assert request('PATCH', '/api/users/security/profile', {'displayName': 'Attack'}, session=bob)[0] == 404
    assert request('PATCH', '/api/me/profile', {'bio': 'a' * 301}, session=original)[0] == 400
    assert request('PATCH', '/api/me/profile', {'defaultReviewVisibility': 'friends'}, session=original)[0] == 400
    assert request('PATCH', '/api/me/profile', {'defaultReviewVisibility': None}, session=original)[0] == 400
    assert request('PATCH', '/api/me/profile', {'displayName': None}, session=original)[0] == 400
    assert request('PATCH', '/api/me/profile', {}, session=original)[0] == 400
    status, cleared = request('PATCH', '/api/me/profile', {'bio': None}, session=original)
    assert status == 200 and cleared['bio'] is None and cleared['defaultScoreVisibility'] == 'public'
    assert request('GET', '/api/users/security', session=bob)[1]['bio'] is None
    assert request('GET', '/api/users/missing', session=bob)[0] == 404
    assert request('POST', '/api/auth/refresh', session=original, csrf=False)[0] == 403
    assert request('POST', '/api/auth/refresh', session=original, origin='https://evil.example')[0] == 403
    assert request('POST', '/api/auth/login', {}, client_header=False)[0] == 403
    forged = dict(original, csrfToken='A' * 64 + '.' + 'B' * 64)
    cookies = SimpleCookie(original['cookies'])
    cookies['__Host-somnia-csrf'] = forged['csrfToken']
    forged['cookies'] = '; '.join(f'{name}={value.value}' for name, value in cookies.items())
    assert request('POST', '/api/auth/refresh', session=forged)[0] == 403
    assert request('GET', '/api/me', session=original)[0] == 200, 'CSRF failure revoked a valid session'

    independent = login()
    status, stolen_successor = request('POST', '/api/auth/refresh', session=original)
    assert status == 200
    assert request('GET', '/api/me', session=stolen_successor)[0] == 200
    assert request('POST', '/api/auth/refresh', session=original)[0] == 401, 'Replayed refresh accepted'
    assert request('GET', '/api/me', session=stolen_successor)[0] == 401, 'Attacker access survived replay detection'
    assert request('POST', '/api/auth/refresh', session=stolen_successor)[0] == 401, 'Attacker refresh survived replay detection'
    assert request('GET', '/api/me', session=independent)[0] == 200, 'Unrelated session was revoked'

    concurrent_session = login()
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
        responses = list(pool.map(lambda _: request('POST', '/api/auth/refresh', session=concurrent_session), range(2)))
    assert sorted(status for status, _ in responses) == [200, 401], 'Concurrent refresh accepted twice'
    winner = next(body for status, body in responses if status == 200)
    assert request('GET', '/api/me', session=winner)[0] == 401, 'Concurrent replay did not revoke the session'

    logout_session = login()
    assert request('POST', '/api/auth/logout', session=logout_session, csrf=False)[0] == 403
    assert request('GET', '/api/me', session=logout_session)[0] == 200
    assert request('POST', '/api/auth/logout', session=logout_session)[0] == 204
    assert request('GET', '/api/me', session=logout_session)[0] == 401
    assert request('GET', '/api/me', session=independent)[0] == 401, 'Other JWT survived global logout'
    assert request('POST', '/api/auth/refresh', session=independent)[0] == 401

    expiring = login()
    sql(f'USE [{database}]; UPDATE Sessoes SET ExpiraEmUtc = DATEADD(day, 1, SYSUTCDATETIME()) WHERE RevogadaEmUtc IS NULL;')
    status, clamped = request('POST', '/api/auth/refresh', session=expiring)
    assert status == 200
    from datetime import datetime, timezone
    remaining = (datetime.fromisoformat(clamped['refreshExpiraEmUtc'].replace('Z', '+00:00')) - datetime.now(timezone.utc)).total_seconds()
    assert 0 < remaining <= 86410, 'Rotation extended the absolute session lifetime'
    claims = json.loads(base64.urlsafe_b64decode(clamped['accessToken'].split('.')[1] + '=='))
    assert 0 < claims['exp'] - time.time() <= 901, 'Access token lasts more than 15 minutes'
    sql(f'USE [{database}]; UPDATE Sessoes SET ExpiraEmUtc = DATEADD(second, -1, SYSUTCDATETIME());')
    assert request('GET', '/api/me', session=clamped)[0] == 401
    assert request('POST', '/api/auth/refresh', session=clamped)[0] == 401
    sql(f"USE [{database}]; IF EXISTS (SELECT 1 FROM RefreshTokens WHERE LEN(TokenHash) <> 64) THROW 51000, 'Raw refresh stored', 1;")

    throttled = [request('POST', '/api/auth/login', {'email': 'missing@example.com', 'password': 'invalid'})[0] for _ in range(12)]
    assert 429 in throttled, 'Login rate limit not enforced'
    paths = request('GET', '/openapi/v1.json')[1]['paths']
    assert '/auth/login' not in paths and '/api/v1/auth/login' not in paths
    print('Security checks passed: secure cookies, signed CSRF, origin checks, stolen-token replay, concurrent replay, global logout, absolute expiration, token hashing, rate limiting route cleanup and profile privacy/ownership/partial updates.')
finally:
    process.terminate()
    try:
        process.wait(timeout=10)
    except subprocess.TimeoutExpired:
        process.kill()
        process.wait()
    log.close()
    assert database.startswith('SomniaAuthSecurity_') and database.replace('_', '').isalnum()
    sql(f"IF DB_ID('{database}') IS NOT NULL BEGIN ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}]; END")
