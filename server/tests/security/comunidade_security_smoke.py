"""Adversarial checks using only the disposable test database and signing key."""
import base64
import hashlib
import hmac
import json
import time
import uuid


def validar_seguranca_comunidade(request, sql, database, env, restart_api, alice, bob):
    paths = ('/api/feed', '/api/discover')

    def encode(value):
        return base64.urlsafe_b64encode(json.dumps(value, separators=(',', ':')).encode()).rstrip(b'=').decode()

    def claims(session):
        return json.loads(base64.urlsafe_b64decode(session['accessToken'].split('.')[1] + '=='))

    # This key is random per smoke run, never a user credential or production key.
    test_key = env['Jwt__SigningKey'].encode()

    def sign(payload, algorithm='HS256'):
        content = encode({'alg': algorithm, 'typ': 'JWT'}) + '.' + encode(payload)
        digest = hashlib.sha384 if algorithm == 'HS384' else hashlib.sha256
        signature = base64.urlsafe_b64encode(hmac.new(test_key, content.encode(), digest).digest()).rstrip(b'=').decode()
        return content + '.' + signature

    original = claims(alice)
    other = claims(bob)
    header, _, signature = alice['accessToken'].split('.')
    forged_payload = dict(original, sub=other['sub'])
    invalid_tokens = [
        'not-a-jwt',
        header + '.' + encode(forged_payload) + '.' + signature,
        encode({'alg': 'none', 'typ': 'JWT'}) + '.' + encode(original) + '.',
        sign(dict(original, exp=int(time.time()) - 3600, nbf=int(time.time()) - 7200)),
        sign(dict(original, iss='untrusted-issuer')),
        sign(dict(original, aud='another-application')),
        sign(original, 'HS384'),
        sign(dict(original, sid=other['sid'])),
        sign(dict(original, sid=str(uuid.uuid4()))),
    ]
    for token in invalid_tokens:
        for path in paths:
            status, _ = request('GET', path, session=dict(alice, accessToken=token))
            assert status == 401, f'Invalid JWT accepted at {path}: {status}'
    for path in paths:
        for query in ('limit=1%3BDROP%20TABLE%20Avaliacoes', 'offset=1%20OR%201%3D1'):
            assert request('GET', path + '?' + query, session=alice)[0] == 400

    # Verify jti revocation on these routes, using a token signed by the test key.
    revoked_jti = uuid.uuid4().hex
    audit_token = sign(dict(original, jti=revoked_jti))
    audit_session = dict(alice, accessToken=audit_token)
    assert request('GET', paths[0], session=audit_session)[0] == 200
    sql(f"USE [{database}]; INSERT INTO TokensRevogados(Jti,ExpiraEmUtc) VALUES('{revoked_jti}',DATEADD(hour,1,SYSUTCDATETIME()));")
    for path in paths:
        assert request('GET', path, session=audit_session)[0] == 401
    sql(f"USE [{database}]; DELETE FROM TokensRevogados WHERE Jti='{revoked_jti}';")

    # An ended session must not regain access merely because its JWT is unexpired.
    for field in ('RevogadaEmUtc', 'ExpiraEmUtc'):
        status, temporary = request('POST', '/api/auth/login', {'email': 'security@example.com', 'password': 'Security123!@'})
        assert status == 200
        session_id = str(uuid.UUID(claims(temporary)['sid']))
        value = 'SYSUTCDATETIME()' if field == 'RevogadaEmUtc' else 'DATEADD(hour,-1,SYSUTCDATETIME())'
        sql(f"USE [{database}]; UPDATE Sessoes SET {field}={value} WHERE Id='{session_id}';")
        for path in paths:
            assert request('GET', path, session=temporary)[0] == 401

    restart_api()  # Independent rate window after adversarial JWT tests.
    started = time.monotonic()
    statuses = [request('GET', paths[i % 2], session=alice)[0] for i in range(62)]
    assert time.monotonic() - started < 60, 'Rate-limit fixture crossed a full window; inconclusive'
    assert statuses[:60] == [200] * 60
    assert statuses[60:] == [429, 429], 'Community endpoints have no shared per-user request limit'
    for path in paths:
        assert request('GET', path, session=bob)[0] == 200, 'Community rate limit leaked to another account'
    assert request('GET', '/api/me', session=alice)[0] == 200, 'Community limit blocked unrelated profile route'
    restart_api()
    print('Community security checks passed: forged/unsigned/expired JWTs, issuer/audience/algorithm, session binding, revoked tokens/sessions, query injection rejection and shared per-account throttling.', flush=True)
