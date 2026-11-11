"""Regression checks for reviews against the disposable SQL/HTTPS smoke environment."""
import concurrent.futures


def validar_avaliacoes(request, sql, database, env, restart_api, alice, bob):
    restart_api()  # New catalog rate window; review creation validates the external reference.
    album = {'provider': 'simulated', 'type': 'album', 'externalId': 'somnia-demo-album-aurora'}
    track = {'provider': 'simulated', 'type': 'track', 'externalId': 'somnia-demo-track-primeira-luz'}
    second = dict(album, externalId='somnia-demo-album-noturno')
    profile = '/api/users/SECURITY/reviews'

    def item_path(item):
        return f"/api/catalog/{item['provider']}/{item['type']}/{item['externalId']}/reviews"

    def summary(item=album):
        status, result = request('GET', item_path(item), session=alice)
        assert status == 200, (status, result)
        return result['summary']

    for path in (profile, item_path(album), '/api/reviews/00000000-0000-0000-0000-000000000001'):
        assert request('GET', path)[0] == 401
    assert request('POST', '/api/reviews', dict(album, score=90))[0] == 401
    assert request('GET', '/api/users/missing/reviews', session=alice)[0] == 404
    assert summary() == {'average': None, 'count': 0}
    assert request('GET', item_path(album), session=alice)[1]['myReview'] is None
    for query in ('limit=0', 'limit=51', 'offset=-1', 'offset=10001', 'limit=abc'):
        assert request('GET', profile + '?' + query, session=alice)[0] == 400
        assert request('GET', item_path(album) + '?' + query, session=alice)[0] == 400

    for body in ({}, dict(album), dict(album, review=' \t\n '), dict(album, score=-1),
                 dict(album, score=101), dict(album, score=1.5), dict(album, score='90'),
                 dict(album, review='a' * 5001), dict(album, score=90, usuarioId='other'),
                 dict(album, score=90, visibility='private'), dict(album, score=90, externalId='missing'),
                 dict(album, score=90, provider='spotify'), dict(album, score=90, type='artist')):
        status, payload = request('POST', '/api/reviews', body, session=alice)
        assert status in (400, 404), (body, status, payload)
    assert request('POST', '/api/reviews', dict(album, score=90), session=alice, client_header=False)[0] == 403
    assert request('POST', '/api/reviews', dict(album, score=90), session=alice, origin='https://evil.example')[0] == 403

    status, first = request('POST', '/api/reviews', dict(album, score=90, review='  Resenha  '), session=alice)
    assert status == 201 and first['score'] == 90 and first['review'] == 'Resenha', (status, first)
    assert first['item'] == album and first['author'] == {'username': 'security', 'displayName': 'Security'}
    assert first['updatedAt'] is None
    path = '/api/reviews/' + first['id']
    assert request('GET', path, session=bob)[1] == first
    assert request('PUT', path, {'score': 50}, session=bob)[0] == 404
    assert request('DELETE', path, session=bob)[0] == 404
    assert request('PUT', path, {'score': 50})[0] == 401
    assert request('DELETE', path)[0] == 401
    assert request('POST', '/api/reviews', dict(album, score=20), session=alice)[0] == 409
    assert request('PUT', path, {}, session=alice)[0] == 400
    assert request('PUT', path, {'score': 101}, session=alice)[0] == 400
    assert request('PUT', path, {'score': 80, 'provider': 'spotify'}, session=alice)[0] == 400
    status, bob_review = request('POST', '/api/reviews', dict(album, score=100), session=bob)
    assert status == 201
    assert summary() == {'average': 95, 'count': 2}
    assert request('GET', item_path(album) + '?limit=1&offset=2', session=alice)[1]['myReview']['id'] == first['id']
    assert request('GET', item_path(album), session=bob)[1]['myReview']['id'] == bob_review['id']

    status, track_review = request('POST', '/api/reviews', dict(track, score=99), session=alice)
    assert status == 201 and summary(track) == {'average': 99, 'count': 1}
    status, edited = request('PUT', path, {'score': 80}, session=alice)
    assert status == 200 and edited['id'] == first['id'] and edited['createdAt'] == first['createdAt']
    assert edited['updatedAt'] is not None and edited['review'] is None
    assert summary() == {'average': 90, 'count': 2} and summary(track)['average'] == 99
    status, edited = request('PUT', path, {'review': 'a' * 5000}, session=alice)
    assert status == 200 and edited['score'] is None
    assert summary() == {'average': 100, 'count': 1}
    assert request('PUT', path, {'score': 0}, session=alice)[0] == 200
    assert summary() == {'average': 50, 'count': 2}
    assert request('DELETE', '/api/reviews/' + bob_review['id'], session=bob)[0] == 204
    assert summary() == {'average': 0, 'count': 1}, 'Zero was treated as missing'

    # Parallel requests exercise the SQL unique constraint as well as the preflight check.
    with concurrent.futures.ThreadPoolExecutor(max_workers=8) as executor:
        results = list(executor.map(lambda _: request('POST', '/api/reviews', dict(second, review='Sem nota'), session=alice), range(8)))
    statuses = [result[0] for result in results]
    assert statuses.count(201) == 1 and statuses.count(409) == 7, statuses
    second_review = next(payload for status, payload in results if status == 201)
    assert summary(second) == {'average': None, 'count': 0}

    # Stable tie-break and pagination, independent of creation/edit timestamps.
    sql(f"USE [{database}]; UPDATE Avaliacoes SET CreatedAt='2026-01-01T00:00:00' WHERE UsuarioId=(SELECT Id FROM AspNetUsers WHERE UserName='security');")
    history = request('GET', profile, session=bob)[1]
    assert history['total'] == 3 and len(history['items']) == 3
    ids = [item['id'] for item in history['items']]
    pages = [request('GET', profile + f'?limit=1&offset={offset}', session=bob)[1] for offset in range(3)]
    assert [page['items'][0]['id'] for page in pages] == ids
    assert all(page['total'] == 3 for page in pages)
    assert pages[0]['hasMore'] and not pages[-1]['hasMore']
    assert request('GET', profile + '?offset=3', session=alice)[1]['items'] == []
    assert request('PUT', path, {'score': 0}, session=alice)[0] == 200
    assert [item['id'] for item in request('GET', profile, session=bob)[1]['items']] == ids
    # SQL collation must preserve case-sensitive external identifiers.
    assert request('GET', item_path(dict(album, externalId=album['externalId'].upper())), session=alice)[1]['reviews']['total'] == 0
    sql(f"""USE [{database}];
        IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('AspNetUsers') AND name IN ('DefaultScoreVisibility','DefaultReviewVisibility'))
            THROW 51000, 'Obsolete visibility preferences still exist', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('Avaliacoes') AND name='IX_Avaliacoes_UsuarioId_Provider_Type_ExternalId' AND is_unique=1)
            THROW 51000, 'Missing review unique index', 1;
        """)

    env['MusicCatalog__Provider'] = 'Disabled'
    restart_api()
    try:
        assert request('GET', path, session=bob)[0] == 200
        assert request('GET', profile, session=bob)[1]['total'] == 3
        assert summary() == {'average': 0, 'count': 1}
        assert request('PUT', path, {'score': 10}, session=alice)[0] == 200
        assert summary() == {'average': 10, 'count': 1}
        assert request('POST', '/api/reviews', dict(album, score=90), session=alice)[0] == 409
        assert request('POST', '/api/reviews', dict(track, score=90), session=bob)[0] == 503
        assert request('DELETE', path, session=alice)[0] == 204
        assert request('GET', path, session=alice)[0] == 404
        assert summary() == {'average': None, 'count': 0}
        assert request('GET', item_path(album), session=alice)[1]['myReview'] is None
        assert request('GET', profile, session=bob)[1]['total'] == 2
        for review in (track_review, second_review):
            assert request('DELETE', '/api/reviews/' + review['id'], session=alice)[0] == 204
        assert request('GET', profile, session=alice)[1]['total'] == 0
    finally:
        env['MusicCatalog__Provider'] = 'Simulated'
        restart_api()
    print('Review checks passed: authenticated CRUD, ownership, 0/100, review-only, SQL uniqueness under concurrency, stable pagination, averages, deletion and catalog outage.', flush=True)
