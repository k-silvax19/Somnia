"""Feed/discovery regression against the isolated HTTPS/SQL environment."""


def validar_comunidade(request, sql, database, env, restart_api, alice, bob):
    restart_api()
    feed = '/api/feed'
    discover = '/api/discover'

    def page(path, session=alice):
        status, data = request('GET', path, session=session)
        assert status == 200, (status, data)
        return data

    def ids(path, session=alice):
        return [item['id'] for item in page(path, session)['items']]

    for path in (feed, discover):
        assert request('GET', path)[0] == 401
        assert request('GET', path, session={'cookies': alice['cookies']})[0] == 401
        for query in ('limit=0', 'limit=51', 'offset=-1', 'offset=10001', 'limit=abc', 'offset=abc'):
            assert request('GET', path + '?' + query, session=alice)[0] == 400
        assert page(path) == {'items': [], 'limit': 20, 'offset': 0, 'total': 0, 'hasMore': False}

    status, charlie = request('POST', '/api/auth/login', {'email': 'charlie@example.com', 'password': 'Security123!@'})
    assert status == 200  # Fixture created by the preceding following checks.
    album = {'provider': 'simulated', 'type': 'album', 'externalId': 'somnia-demo-album-aurora'}
    track = {'provider': 'simulated', 'type': 'track', 'externalId': 'somnia-demo-track-primeira-luz'}

    def create(item, session, **content):
        status, data = request('POST', '/api/reviews', dict(item, **content), session=session)
        assert status == 201, (status, data)
        return data

    own = create(album, alice, score=90)
    followed = create(album, bob, score=80, review='Opinião original')
    review_only = create(track, bob, review='Apenas texto')
    outsider = create(album, charlie, score=70)
    reviews = [own, followed, review_only, outsider]
    assert page(discover)['total'] == 4
    assert set(ids(discover)) == {r['id'] for r in reviews}
    assert set(ids(discover, charlie)) == set(ids(discover, bob))
    assert page(feed)['total'] == 0, 'Own reviews or non-followed authors leaked into feed'
    assert request('PUT', '/api/users/bob/follow', session=alice)[0] == 204
    assert set(ids(feed)) == {followed['id'], review_only['id']}, 'Following must include existing reviews'
    assert page(feed, bob)['total'] == 0, 'Feed is not personalized to JWT'
    alice_id = request('GET', '/api/me', session=alice)[1]['id']
    assert page(feed + '?usuarioId=' + alice_id, bob)['total'] == 0, 'Query overrode JWT identity'
    assert request('PUT', '/api/users/security/follow', session=charlie)[0] == 204
    assert ids(feed, charlie) == [own['id']]

    # Force ties to verify stable ordering and pagination in both queries.
    fixture_ids = ','.join("'" + r['id'] + "'" for r in reviews)
    sql(f"USE [{database}]; UPDATE Avaliacoes SET CreatedAt='2026-01-01T00:00:00' WHERE Id IN ({fixture_ids});")
    for path, total in ((feed, 2), (discover, 4)):
        ordered = ids(path)
        pages = [page(path + f'?limit=1&offset={offset}') for offset in range(total)]
        assert [p['items'][0]['id'] for p in pages] == ordered
        assert all(p['total'] == total for p in pages)
        assert pages[0]['hasMore'] and not pages[-1]['hasMore']
        assert page(path + f'?offset={total}')['items'] == []
    ordered_feed, ordered_discover = ids(feed), ids(discover)
    for item in page(discover)['items']:
        assert set(item) == {'id', 'author', 'item', 'score', 'review', 'createdAt', 'updatedAt'}
        assert set(item['author']) == {'username', 'displayName'}
        assert set(item['item']) == {'provider', 'type', 'externalId'}
    assert next(x for x in page(feed)['items'] if x['id'] == review_only['id'])['score'] is None
    assert request('PUT', '/api/reviews/' + followed['id'], {'score': 1}, session=alice)[0] == 404

    # Reading community content never needs the external catalog.
    env['MusicCatalog__Provider'] = 'Disabled'
    restart_api()
    try:
        assert ids(feed) == ordered_feed and ids(discover) == ordered_discover
        status, edited = request('PUT', '/api/reviews/' + followed['id'], {'score': 100, 'review': 'Revisada'}, session=bob)
        assert status == 200 and edited['createdAt'].startswith('2026-01-01')
        for path in (feed, discover):
            item = next(x for x in page(path)['items'] if x['id'] == followed['id'])
            assert item['score'] == 100 and item['review'] == 'Revisada' and item['updatedAt'] is not None
        assert ids(feed) == ordered_feed and ids(discover) == ordered_discover, 'Edit moved or duplicated review'
        assert request('PATCH', '/api/me/profile', {'displayName': 'Bob no feed'}, session=bob)[0] == 200
        assert all(x['author']['displayName'] == 'Bob no feed' for x in page(feed)['items'])
        assert request('PATCH', '/api/me/profile', {'displayName': followed['author']['displayName']}, session=bob)[0] == 200
        assert request('DELETE', '/api/reviews/' + review_only['id'], session=bob)[0] == 204
        assert ids(feed) == [followed['id']] and review_only['id'] not in ids(discover)
        assert page(discover)['total'] == 3
        assert request('DELETE', '/api/users/bob/follow', session=alice)[0] == 204
        assert page(feed)['total'] == 0 and page(discover)['total'] == 3
        assert request('PUT', '/api/users/charlie/follow', session=alice)[0] == 204
        assert ids(feed) == [outsider['id']]
        assert request('DELETE', '/api/users/charlie/follow', session=alice)[0] == 204
        assert request('PUT', '/api/users/bob/follow', session=alice)[0] == 204
        assert ids(feed) == [followed['id']]
        assert request('DELETE', '/api/reviews/' + followed['id'], session=bob)[0] == 204
        assert page(feed)['total'] == 0
        assert followed['id'] not in ids(discover)
        assert request('DELETE', '/api/reviews/' + own['id'], session=alice)[0] == 204
        assert page(feed, charlie)['total'] == 0
        assert request('DELETE', '/api/reviews/' + outsider['id'], session=charlie)[0] == 204
        assert page(discover)['total'] == 0
    finally:
        env['MusicCatalog__Provider'] = 'Simulated'
        restart_api()
    assert request('DELETE', '/api/users/bob/follow', session=alice)[0] == 204
    assert request('DELETE', '/api/users/security/follow', session=charlie)[0] == 204
    print('Community checks passed: authenticated personalized feed, global discovery, stable pagination, review-only content, current author data, edits/deletions, follow changes and catalog outage.', flush=True)
