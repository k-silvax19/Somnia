"""HTTPS/SQL security regression checks. Requires a Release build, Docker SQL and a .NET development certificate."""
import base64
import concurrent.futures
import json
import os
import secrets
import socket
import ssl
import struct
import subprocess
import tempfile
import time
import urllib.error
import urllib.request
import uuid
import zlib
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


def request(method, path, body=None, session=None, csrf=True, origin=None, client_header=True, upload=None):
    headers = {'Content-Type': 'application/json', 'Origin': origin or f'https://localhost:{port}'}
    data = json.dumps(body).encode() if body is not None else None
    if upload is not None:
        boundary = 'SomniaBoundary' + uuid.uuid4().hex
        headers['Content-Type'] = 'multipart/form-data; boundary=' + boundary
        data = (f'--{boundary}\r\nContent-Disposition: form-data; name="file"; filename="avatar.png"\r\n'
                'Content-Type: image/png\r\n\r\n').encode() + upload + f'\r\n--{boundary}--\r\n'.encode()
    if client_header:
        headers['X-Somnia-Client'] = 'web'
    if session:
        headers['Cookie'] = session.get('cookies', '')
        if session.get('accessToken'):
            headers['Authorization'] = 'Bearer ' + session['accessToken']
        if csrf:
            headers['X-CSRF-Token'] = session.get('csrfToken', '')
    req = urllib.request.Request(f'https://localhost:{port}' + path,
                                 data=data,
                                 headers=headers, method=method)
    try:
        response = opener.open(req, timeout=20)
    except urllib.error.HTTPError as error:
        response = error
    content = response.read()
    payload = json.loads(content) if content and 'json' in response.headers.get('Content-Type', '') else content or {}
    if content and response.headers.get('Content-Type') == 'image/png':
        assert 'no-store' in response.headers.get('Cache-Control', '')
        assert response.headers.get('X-Content-Type-Options') == 'nosniff'
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


def png(animated=False):
    """Small real PNG/APNG fixtures, including metadata that must not be published."""
    def chunk(kind, content):
        return struct.pack('>I', len(content)) + kind + content + struct.pack('>I', zlib.crc32(kind + content))
    header = b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', 1, 1, 8, 6, 0, 0, 0))
    pixels = zlib.compress(b'\x00\xff\x00\x00\xff')
    header += chunk(b'tEXt', b'Comment\x00private-avatar-metadata')
    if animated:
        header += chunk(b'acTL', struct.pack('>II', 2, 0))
        header += chunk(b'fcTL', struct.pack('>IIIIIHHBB', 0, 1, 1, 0, 0, 1, 10, 0, 0))
    header += chunk(b'IDAT', pixels)
    if animated:
        header += chunk(b'fcTL', struct.pack('>IIIIIHHBB', 1, 1, 1, 0, 0, 1, 10, 0, 0))
        header += chunk(b'fdAT', struct.pack('>I', 2) + pixels)
    return header + chunk(b'IEND', b'')


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

    avatar_path = '/api/users/security/avatar'
    assert request('GET', avatar_path, session=bob)[0] == 404
    assert request('PUT', '/api/me/avatar', upload=png())[0] == 401
    assert request('PUT', '/api/me/avatar', session=original, upload=png(), client_header=False)[0] == 403
    assert request('PUT', '/api/me/avatar', session=original, upload=png(), origin='https://evil.example')[0] == 403
    assert request('PUT', '/api/me/avatar', session=original, upload=b'<script>not an image</script>')[0] == 400
    assert request('PUT', '/api/me/avatar', session=original, upload=png(animated=True))[0] == 400
    assert request('PUT', '/api/me/avatar', session=original, upload=b'A' * (2 * 1024 * 1024 + 1))[0] == 400
    status, uploaded = request('PUT', '/api/me/avatar', session=original, upload=png())
    assert status == 200 and uploaded['avatarUrl'] == avatar_path, (status, uploaded)
    assert request('GET', '/api/me', session=original)[1]['avatarUrl'] == avatar_path
    assert request('GET', '/api/users/security', session=bob)[1]['avatarUrl'] == avatar_path
    assert request('GET', avatar_path)[0] == 401
    status, avatar = request('GET', '/api/users/SECURITY/avatar', session=bob)
    assert status == 200 and avatar.startswith(b'\x89PNG\r\n\x1a\n')
    assert b'private-avatar-metadata' not in avatar, 'Avatar metadata leaked'
    assert request('GET', '/api/me', session=bob)[1]['avatarUrl'] is None
    # A failed replacement must preserve the previous image.
    assert request('PUT', '/api/me/avatar', session=original, upload=b'broken')[0] == 400
    assert request('GET', avatar_path, session=bob)[1] == avatar
    assert request('PUT', '/api/me/avatar', session=original, upload=png())[0] == 200
    assert request('DELETE', '/api/me/avatar', session=bob)[0] == 204
    assert request('GET', avatar_path, session=bob)[0] == 200, 'Another user removed the avatar'
    # Replacements and deletion of one avatar are serialized in the same transaction.
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
        writes = list(pool.map(lambda _: request('PUT', '/api/me/avatar', session=original, upload=png())[0], range(2)))
    assert writes == [200, 200], writes
    assert request('DELETE', '/api/me/avatar', session=original)[0] == 204
    assert request('GET', avatar_path, session=bob)[0] == 404
    assert request('GET', '/api/me', session=original)[1]['avatarUrl'] is None
    assert request('DELETE', '/api/me/avatar', session=original)[0] == 204
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
    print('Security checks passed: secure cookies, signed CSRF, origin checks, stolen-token replay, concurrent replay, global logout, absolute expiration, token hashing, rate limiting, profile privacy/ownership/partial updates and static avatar upload/replacement/deletion/validation/concurrency.')
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
