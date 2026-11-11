"""HTTPS/SQL/RabbitMQ regression checks. Requires a Release solution build, Docker SQL and a .NET dev certificate."""
import base64
import concurrent.futures
import http.client
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
from contextlib import closing
from http.cookies import SimpleCookie
from pathlib import Path
from avaliacoes_smoke import validar_avaliacoes
from relacoes_smoke import validar_relacoes

api = Path(__file__).resolve().parents[2] / 'src' / 'Api'
worker_path = api.parent / 'Worker'
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
with socket.socket() as sock:
    sock.bind(('127.0.0.1', 0))
    rabbit_port = sock.getsockname()[1]
with socket.socket() as sock:
    sock.bind(('127.0.0.1', 0))
    management_port = sock.getsockname()[1]
rabbit_name = 'somnia-avatar-test-' + uuid.uuid4().hex
rabbit_password = secrets.token_hex(24)
worker_image = os.environ.get('SOMNIA_TEST_WORKER_IMAGE')
worker_name = rabbit_name + '-worker'
worker = None
rabbit_started = False
env = os.environ.copy()
env.update(ASPNETCORE_ENVIRONMENT='Development', DOTNET_ENVIRONMENT='Development', ASPNETCORE_URLS=f'https://localhost:{port};http://localhost:{http_port}',
           ConnectionStrings__SqlServer=connection, Jwt__SigningKey=secrets.token_hex(32), Jwt__ExpiresInMinutes='15')
env['ConnectionStrings__RabbitMq'] = f'rabbitmq://somnia:{rabbit_password}@localhost:{rabbit_port}/'
env['MusicCatalog__Provider'] = 'Simulated'
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


def assert_problem(response, payload):
    assert response.headers.get('Content-Type', '').startswith('application/problem+json'), (response.status, response.headers.get('Content-Type'))
    assert isinstance(payload, dict) and payload.get('status') == response.status, payload
    assert payload.get('type') == f'urn:somnia:problem:http:{response.status}', payload
    assert all(isinstance(payload.get(field), str) and payload[field] for field in ('title', 'detail', 'traceId')), payload
    assert 'no-store' in response.headers.get('Cache-Control', '')
    if response.status == 429:
        assert int(response.headers.get('Retry-After', '0')) > 0


def oversized_request(session):
    # Send headers first: Kestrel rejects Content-Length before reading the body.
    # A client that sends the whole body first can see a TLS reset instead of the 413.
    with closing(http.client.HTTPSConnection('localhost', port, context=ssl._create_unverified_context(), timeout=20)) as client:
        client.putrequest('PUT', '/api/me/avatar')
        for key, value in {
            'Content-Type': 'multipart/form-data; boundary=SomniaOversize',
            'Content-Length': str(2 * 1024 * 1024 + 65537),
            'Authorization': 'Bearer ' + session['accessToken'],
            'Origin': f'https://localhost:{port}',
            'X-Somnia-Client': 'web'
        }.items():
            client.putheader(key, value)
        client.endheaders()
        response = client.getresponse()
        payload = json.loads(response.read())
        assert response.status in (400, 413), (response.status, payload)
        assert_problem(response, payload)


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
    if response.status >= 400:
        assert_problem(response, payload)
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


def png(animated=False, blue=False):
    """Small real PNG/APNG fixtures, including metadata that must not be published."""
    def chunk(kind, content):
        return struct.pack('>I', len(content)) + kind + content + struct.pack('>I', zlib.crc32(kind + content))
    header = b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', 1, 1, 8, 6, 0, 0, 0))
    pixels = zlib.compress(b'\x00' + (b'\x00\x00\xff\xff' if blue else b'\xff\x00\x00\xff'))
    header += chunk(b'tEXt', b'Comment\x00private-avatar-metadata')
    if animated:
        header += chunk(b'acTL', struct.pack('>II', 2, 0))
        header += chunk(b'fcTL', struct.pack('>IIIIIHHBB', 0, 1, 1, 0, 0, 1, 10, 0, 0))
    header += chunk(b'IDAT', pixels)
    if animated:
        header += chunk(b'fcTL', struct.pack('>IIIIIHHBB', 1, 1, 1, 0, 0, 1, 10, 0, 0))
        header += chunk(b'fdAT', struct.pack('>I', 2) + pixels)
    return header + chunk(b'IEND', b'')


def rabbit_api(method, path, payload=None):
    headers = {'Authorization': 'Basic ' + base64.b64encode(f'somnia:{rabbit_password}'.encode()).decode(),
               'Content-Type': 'application/json'}
    req = urllib.request.Request(f'http://localhost:{management_port}/api/' + path,
                                 data=json.dumps(payload).encode() if payload is not None else None,
                                 headers=headers, method=method)
    with urllib.request.urlopen(req, timeout=10) as response:
        return json.loads(response.read())


def start_rabbit():
    global rabbit_started
    docker_env = os.environ.copy()
    docker_env.update(RABBITMQ_DEFAULT_USER='somnia', RABBITMQ_DEFAULT_PASS=rabbit_password)
    subprocess.run(['docker', 'run', '-d', '--name', rabbit_name, '--memory', '512m', '--cpus', '1',
                    '-p', f'127.0.0.1:{rabbit_port}:5672', '-p', f'127.0.0.1:{management_port}:15672',
                    '-e', 'RABBITMQ_DEFAULT_USER', '-e', 'RABBITMQ_DEFAULT_PASS', 'rabbitmq:4-management'],
                   env=docker_env, check=True, capture_output=True, timeout=60)
    rabbit_started = True
    for _ in range(160):
        try:
            rabbit_api('GET', 'overview')
            return
        except (OSError, ValueError):
            time.sleep(0.25)
    raise AssertionError('RabbitMQ did not start')


def stop_worker():
    global worker
    if worker is not None:
        if worker_image:
            subprocess.run(['docker', 'stop', '-t', '5', worker_name], capture_output=True, timeout=15)
        else:
            worker.terminate()
        try:
            worker.wait(timeout=10)
        except subprocess.TimeoutExpired:
            worker.kill()
            worker.wait()
        worker = None


def start_worker():
    global worker
    if worker_image:
        container_env = env.copy()
        server_key = next(key for key in parts if key.lower() in ('server', 'data source'))
        server = parts[server_key]
        server_host = server.split(',')[0]
        assert server_host.lower() in ('localhost', '127.0.0.1'), 'Container test requires local Docker SQL'
        container_env['ConnectionStrings__SqlServer'] = connection.replace(server, server.replace(server_host, 'host.docker.internal', 1))
        container_env['ConnectionStrings__RabbitMq'] = env['ConnectionStrings__RabbitMq'].replace('@localhost:', '@host.docker.internal:')
        worker = subprocess.Popen(['docker', 'run', '--rm', '--name', worker_name,
                                   '--memory', '512m', '--cpus', '1', '--read-only', '--tmpfs', '/tmp',
                                   '--cap-drop', 'ALL', '--security-opt', 'no-new-privileges',
                                   '-e', 'ConnectionStrings__SqlServer', '-e', 'ConnectionStrings__RabbitMq', worker_image],
                                  env=container_env, stdout=log, stderr=log)
    else:
        worker = subprocess.Popen(['dotnet', str(worker_path / 'bin/Release/net10.0/Somnia.Worker.dll')],
                                  cwd=worker_path, env=env, stdout=log, stderr=log)


def restart_api():
    global process
    process.terminate()
    try:
        process.wait(timeout=10)
    except subprocess.TimeoutExpired:
        process.kill()
        process.wait()
    process = subprocess.Popen(['dotnet', str(api / 'bin/Release/net10.0/Somnia.Api.dll')],
                               cwd=api, env=env, stdout=log, stderr=log)
    for _ in range(160):
        try:
            if request('GET', '/openapi/v1.json')[0] == 200:
                return
        except OSError:
            pass
        time.sleep(0.25)
    raise AssertionError('API did not recover after restart')


def submit_avatar(session, image):
    status, payload = request('PUT', '/api/me/avatar', session=session, upload=image)
    assert status == 202 and payload['status'] == 'pending', (status, payload)
    assert set(payload) == {'id', 'status', 'erro'}, 'Request leaked private source or user data'
    return payload['id']


def wait_avatar(session, request_id, expected):
    for _ in range(160):
        status, payload = request('GET', '/api/me/avatar/requests/' + request_id, session=session)
        assert status == 200, (status, payload)
        if payload['status'] != 'pending':
            assert payload['status'] == expected, payload
            return payload
        if worker is not None and worker.poll() is not None:
            log.seek(0, 2)
            end = log.tell()
            log.seek(max(0, end - 5000))
            details = log.read().decode(errors='replace').replace(rabbit_password, '<REDACTED>').replace(parts['Password'], '<REDACTED>')
            log.seek(0, 2)
            raise AssertionError('Avatar worker exited unexpectedly: ' + details)
        time.sleep(0.25)
    raise AssertionError('Avatar processing did not finish')


def replay_avatar(request_id):
    envelope = {'messageId': str(uuid.uuid4()),
                'messageType': ['urn:message:Somnia.Aplicacao.Modulos.Perfis.Mensageria:ProcessarAvatarMessage'],
                'message': {'solicitacaoId': request_id}, 'headers': {}}
    result = rabbit_api('POST', 'exchanges/%2F/somnia-avatars-solicitados/publish', {
        'properties': {'content_type': 'application/vnd.masstransit+json', 'delivery_mode': 2},
        'routing_key': '', 'payload': json.dumps(envelope), 'payload_encoding': 'string'})
    assert result['routed'], 'Replay did not reach the real queue'


def drain_queue():
    for _ in range(80):
        state = rabbit_api('GET', 'queues/%2F/somnia-avatars-solicitados')
        if state.get('messages', -1) == 0:
            return
        time.sleep(0.25)
    raise AssertionError('RabbitMQ queue did not drain')


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
    assert request('POST', '/api/auth/register', dict(registration, username='ab'))[0] == 400
    assert request('POST', '/api/auth/register', dict(registration, username='a' * 31))[0] == 400
    assert request('POST', '/api/auth/register', registration)[0] == 201
    original = login()
    assert request('GET', '/api/me', session=original)[0] == 200
    mine = request('GET', '/api/me', session=original)[1]
    assert mine['displayName'] == 'Security'
    assert 'defaultScoreVisibility' not in mine and 'defaultReviewVisibility' not in mine
    assert request('GET', '/api/users/security')[0] == 401
    assert request('PATCH', '/api/me/profile', {'bio': 'anonymous'})[0] == 401
    bob_registration = dict(registration, email='bob@example.com', username='bob', displayName='Bob')
    assert request('POST', '/api/auth/register', bob_registration)[0] == 201
    bob_status, bob = request('POST', '/api/auth/login', {'email': 'bob@example.com', 'password': registration['password']})
    assert bob_status == 200
    assert request('POST', '/api/auth/register', dict(registration, email='SECURITY@EXAMPLE.COM', username='another-user'))[0] == 409
    restart_api()  # Fresh rate window for the remaining independent registration check.
    assert request('POST', '/api/auth/register', dict(registration, email='another@example.com', username='SECURITY'))[0] == 409
    sql(f"USE [{database}]; IF EXISTS (SELECT 1 FROM AspNetUsers WHERE EmailConfirmed=1) THROW 51000, 'Pilot fixture must use unconfirmed email', 1;")
    assert request('GET', '/api/does-not-exist', session=original)[0] == 404
    assert request('PUT', '/api/auth/login', {}, session=original)[0] == 405

    # Exercise the actual 500 handler against this disposable database only.
    assert database.startswith('SomniaAuthSecurity_')
    sql(f"USE [{database}]; EXEC sp_rename 'AspNetUsers', 'AspNetUsers_ErrorProbe';")
    try:
        status, internal = request('POST', '/api/auth/login', {'email': 'security@example.com', 'password': registration['password']})
        assert status == 500
        assert 'AspNetUsers' not in json.dumps(internal) and parts['Password'] not in json.dumps(internal)
    finally:
        sql(f"USE [{database}]; EXEC sp_rename 'AspNetUsers_ErrorProbe', 'AspNetUsers';")
    status, updated = request('PATCH', '/api/me/profile', {'bio': 'Minha bio'}, session=original)
    assert status == 200 and updated['bio'] == 'Minha bio'
    assert 'defaultScoreVisibility' not in updated and 'defaultReviewVisibility' not in updated
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
    assert status == 200 and cleared['bio'] is None
    assert request('GET', '/api/users/security', session=bob)[1]['bio'] is None
    assert request('GET', '/api/users/missing', session=bob)[0] == 404

    search_path = '/api/catalog/search?q=Somnia&type=album'
    album_path = '/api/catalog/albums/somnia-demo-album-aurora'
    track_path = '/api/catalog/tracks/somnia-demo-track-primeira-luz'
    for path in (search_path, album_path, track_path):
        assert request('GET', path)[0] == 401
    status, albums = request('GET', search_path, session=original)
    assert status == 200 and albums['total'] == 2 and albums['limit'] == 10 and albums['offset'] == 0
    assert len(albums['items']) == 2 and not albums['hasMore']
    assert all(item['type'] == 'album' and item['provider'] == 'simulated' for item in albums['items'])
    assert all(item['spotifyUrl'] is None and item['coverUrl'] is None for item in albums['items'])
    status, tracks = request('GET', '/api/catalog/search?q=%20somnia%20&type=track&limit=1&offset=1', session=original)
    assert status == 200 and tracks['total'] == 6 and tracks['hasMore']
    assert len(tracks['items']) == 1 and tracks['items'][0]['type'] == 'track'
    status, empty = request('GET', '/api/catalog/search?q=not-found&type=album', session=original)
    assert status == 200 and empty['items'] == [] and empty['total'] == 0 and not empty['hasMore']
    status, end = request('GET', '/api/catalog/search?q=Somnia&type=track&offset=1000', session=original)
    assert status == 200 and not end['items'] and end['total'] == 6 and not end['hasMore']
    status, album = request('GET', album_path, session=original)
    assert status == 200 and album['type'] == 'album'
    assert [track['trackNumber'] for track in album['tracks']] == [1, 2, 3]
    status, track = request('GET', track_path, session=original)
    assert status == 200 and track['type'] == 'track' and track['trackNumber'] == 1
    assert track['album']['externalId'] == album['externalId']
    assert request('GET', '/api/catalog/albums/somnia-demo-track-primeira-luz', session=original)[0] == 404
    assert request('GET', '/api/catalog/tracks/somnia-demo-album-aurora', session=original)[0] == 404
    assert request('GET', '/api/catalog/albums/missing', session=original)[0] == 404
    assert request('GET', '/api/catalog/albums/bad%3Aid', session=original)[0] == 400
    for query in ('type=album', 'q=Somnia', 'q=%20&type=album', 'q=Somnia&type=artist',
                  'q=Somnia&type=album&limit=0', 'q=Somnia&type=album&limit=11',
                  'q=Somnia&type=album&limit=abc', 'q=Somnia&type=album&offset=-1',
                  'q=Somnia&type=album&offset=1001', 'q=' + 'a' * 201 + '&type=album'):
        assert request('GET', '/api/catalog/search?' + query, session=original)[0] == 400, query

    env['MusicCatalog__Provider'] = 'Disabled'
    restart_api()
    for path in (search_path, album_path, track_path):
        assert request('GET', path, session=original)[0] == 503
    assert request('GET', '/api/me', session=original)[0] == 200, 'Catalog outage blocked owner data'
    env['MusicCatalog__Provider'] = 'Simulated'
    restart_api()
    catalog_limits = [request('GET', search_path, session=original)[0] for _ in range(62)]
    assert 429 in catalog_limits, 'Catalog rate limit not enforced'
    assert request('GET', search_path, session=bob)[0] == 200, 'Catalog rate limit leaked to another user'
    assert request('GET', '/api/me', session=original)[0] == 200
    print('Catalog checks passed: authenticated endpoints, type isolation, pagination, album-track links, validation, controlled outage and per-user rate limit.', flush=True)
    validar_avaliacoes(request, sql, database, env, restart_api, original, bob)
    validar_relacoes(request, sql, database, restart_api, original, bob)

    if os.environ.get('SOMNIA_TEST_SPOTIFY_LIVE') == '1':
        env['MusicCatalog__Provider'] = 'Spotify'
        restart_api()
        status, live_search = request('GET', '/api/catalog/search?q=Radiohead&type=album&limit=1', session=original)
        assert status == 200 and live_search['items'], (status, live_search)
        live_item = live_search['items'][0]
        assert set(live_item) == {'provider', 'type', 'externalId', 'title', 'artists', 'coverUrl', 'spotifyUrl'}
        assert live_item['provider'] == 'spotify' and live_item['type'] == 'album'
        assert live_item['spotifyUrl'] == 'https://open.spotify.com/album/' + live_item['externalId']
        status, live_album = request('GET', '/api/catalog/albums/' + live_item['externalId'], session=original)
        assert status == 200 and live_album['tracks'], (status, live_album)
        status, live_track = request('GET', '/api/catalog/tracks/' + live_album['tracks'][0]['externalId'], session=original)
        assert status == 200 and live_track['provider'] == 'spotify' and live_track['type'] == 'track', (status, live_track)
        assert live_track['album']['type'] == 'album' and live_track['album']['externalId']
        status, live_tracks = request('GET', '/api/catalog/search?q=Radiohead&type=track&limit=1', session=original)
        assert status == 200 and live_tracks['items'][0]['type'] == 'track', (status, live_tracks)
        assert request('GET', '/api/me', session=original)[0] == 200
        print('Live Spotify checks passed through Somnia: album/track search, album tracks, track details, attribution and links.', flush=True)
        env['MusicCatalog__Provider'] = 'Simulated'
        restart_api()

    avatar_path = '/api/users/security/avatar'
    assert request('GET', avatar_path, session=bob)[0] == 404
    assert request('PUT', '/api/me/avatar', upload=png())[0] == 401
    assert request('PUT', '/api/me/avatar', session=bob, upload=png(), client_header=False)[0] == 403
    assert request('PUT', '/api/me/avatar', session=bob, upload=png(), origin='https://evil.example')[0] == 403
    assert request('PUT', '/api/me/avatar', session=bob, upload=b'A' * (2 * 1024 * 1024 + 1))[0] == 400
    oversized_request(bob)
    assert request('PUT', '/api/me/avatar', {}, session=original)[0] == 415
    print('HTTP checks passed: registration rules, normalized duplicates, private profiles and ProblemDetails including controlled 500 and oversized requests.', flush=True)
    # Broker and consumer are both OFF: acceptance must be durable, without decoding in the API.
    first_request = submit_avatar(original, png())
    assert request('GET', avatar_path, session=bob)[0] == 404
    assert request('GET', '/api/me/avatar/requests/' + first_request, session=bob)[0] == 404
    assert request('GET', '/api/me/avatar/requests/' + first_request)[0] == 401
    assert request('GET', '/api/me/avatar/requests/' + first_request, session=original)[1]['status'] == 'pending'
    sql(f"USE [{database}]; IF NOT EXISTS (SELECT 1 FROM OutboxMessage) THROW 51000, 'Missing durable outbox', 1;")
    restart_api()
    assert request('GET', '/api/me/avatar/requests/' + first_request, session=original)[1]['status'] == 'pending'
    start_rabbit()
    for _ in range(160):
        try:
            if rabbit_api('GET', 'queues/%2F/somnia-avatars-solicitados').get('messages_ready', 0) > 0:
                break
        except urllib.error.HTTPError:
            pass
        time.sleep(0.25)
    else:
        raise AssertionError('Outbox did not deliver to a queue with no consumer')
    # Start a separate consumer process only after the API accepted the upload.
    start_worker()
    wait_avatar(original, first_request, 'completed')
    assert request('GET', '/api/me', session=original)[1]['avatarUrl'] == avatar_path
    assert request('GET', '/api/users/security', session=bob)[1]['avatarUrl'] == avatar_path
    assert request('GET', avatar_path)[0] == 401
    status, avatar = request('GET', '/api/users/SECURITY/avatar', session=bob)
    assert status == 200 and avatar.startswith(b'\x89PNG\r\n\x1a\n')
    assert b'private-avatar-metadata' not in avatar, 'Avatar metadata leaked'
    assert request('GET', '/api/me', session=bob)[1]['avatarUrl'] is None
    # A failed replacement must preserve the previous image.
    failed = submit_avatar(original, b'broken')
    assert wait_avatar(original, failed, 'rejected')['erro']
    assert request('GET', avatar_path, session=bob)[1] == avatar
    for invalid in (b'<script>not an image</script>', png(animated=True)):
        rejected = submit_avatar(bob, invalid)
        wait_avatar(bob, rejected, 'rejected')
    assert request('DELETE', '/api/me/avatar', session=bob)[0] == 204
    assert request('GET', avatar_path, session=bob)[0] == 200, 'Another user removed the avatar'
    stop_worker()
    # Two simultaneous submissions: the newer one supersedes the older one.
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
        writes = list(pool.map(lambda _: submit_avatar(original, png(blue=True)), range(2)))
    states = [request('GET', '/api/me/avatar/requests/' + item, session=original)[1]['status'] for item in writes]
    assert sorted(states) == ['cancelled', 'pending'], states
    older = writes[states.index('cancelled')]
    newer = writes[states.index('pending')]
    start_worker()
    wait_avatar(original, newer, 'completed')
    blue_avatar = request('GET', avatar_path, session=bob)[1]
    assert blue_avatar != avatar, 'Latest avatar did not replace the old image'
    replay_avatar(first_request)  # Duplicate completed message.
    replay_avatar(older)  # Late message from a superseded upload.
    drain_queue()
    assert request('GET', avatar_path, session=bob)[1] == blue_avatar

    stop_worker()
    pending_delete = submit_avatar(original, png())
    assert request('DELETE', '/api/me/avatar', session=original)[0] == 204
    wait_avatar(original, pending_delete, 'cancelled')
    start_worker()
    replay_avatar(pending_delete)
    drain_queue()
    assert request('GET', avatar_path, session=bob)[0] == 404
    assert request('GET', '/api/me', session=original)[1]['avatarUrl'] is None
    assert request('DELETE', '/api/me/avatar', session=original)[0] == 204

    stop_worker()
    exhausted = submit_avatar(original, png())
    sql(f"USE [{database}]; UPDATE SolicitacoesAvatar SET Tentativas=3 WHERE Id='{exhausted}';")
    start_worker()
    wait_avatar(original, exhausted, 'failed')
    sql(f"USE [{database}]; IF EXISTS (SELECT 1 FROM SolicitacoesAvatar WHERE Status <> 'pending' AND Arquivo IS NOT NULL) THROW 51000, 'Raw image retained', 1;")
    limits = [request('PUT', '/api/me/avatar', session=bob, upload=b'')[0] for _ in range(11)]
    assert 429 in limits, 'Avatar per-user upload rate limit not enforced'
    assert request('GET', '/api/me', session=original)[0] == 200
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
    print('Security checks passed: authentication, profile privacy, reviews, catalog, async RabbitMQ avatar consumer, broker-offline outbox, worker restart, invalid/APNG rejection, metadata stripping, supersession, duplicate delivery, deletion cancellation, attempt limits and rate limiting.')
except Exception:
    log.seek(0)
    diagnostic = log.read().decode(errors='replace')[-10000:]
    for secret in (parts['Password'], env['Jwt__SigningKey'], rabbit_password):
        diagnostic = diagnostic.replace(secret, '[redacted]')
    print(diagnostic, flush=True)
    raise
finally:
    stop_worker()
    process.terminate()
    try:
        process.wait(timeout=10)
    except subprocess.TimeoutExpired:
        process.kill()
        process.wait()
    log.close()
    assert database.startswith('SomniaAuthSecurity_') and database.replace('_', '').isalnum()
    try:
        sql(f"IF DB_ID('{database}') IS NOT NULL BEGIN ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}]; END")
    finally:
        if rabbit_started:
            subprocess.run(['docker', 'rm', '-f', '-v', rabbit_name], check=True, capture_output=True, timeout=30)
