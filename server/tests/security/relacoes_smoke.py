"""Following checks in the disposable HTTPS/SQL smoke environment."""
import concurrent.futures


def validar_relacoes(request, sql, database, restart_api, alice, bob):
    target = '/api/users/bob'
    owner = '/api/users/security'
    follow = target + '/follow'

    def summary(path, session=alice):
        status, data = request('GET', path + '/connections', session=session)
        assert status == 200, (status, data)
        return data

    for suffix in ('/connections', '/followers', '/following'):
        assert request('GET', target + suffix)[0] == 401
        assert request('GET', '/api/users/missing' + suffix, session=alice)[0] == 404
        assert request('GET', '/api/users/ab' + suffix, session=alice)[0] == 400
    for method in ('PUT', 'DELETE'):
        assert request(method, follow)[0] == 401
        assert request(method, follow, session=alice, client_header=False)[0] == 403
        assert request(method, follow, session=alice, origin='https://evil.example')[0] == 403
        assert request(method, '/api/users/SECURITY/follow', session=alice)[0] == 400
        assert request(method, '/api/users/missing/follow', session=alice)[0] == 404
        assert request(method, '/api/users/ab/follow', session=alice)[0] == 400
    for suffix in ('/followers', '/following'):
        for query in ('limit=0', 'limit=51', 'offset=-1', 'offset=10001', 'limit=abc'):
            assert request('GET', target + suffix + '?' + query, session=alice)[0] == 400
    assert summary(target) == {'isFollowing': False, 'followersCount': 0, 'followingCount': 0}
    assert request('DELETE', follow, session=alice)[0] == 204
    assert request('GET', target + '/followers', session=alice)[1]['items'] == []

    with concurrent.futures.ThreadPoolExecutor(max_workers=8) as executor:
        statuses = list(executor.map(lambda _: request('PUT', '/api/users/BOB/follow', session=alice)[0], range(8)))
    assert statuses == [204] * 8, statuses
    assert summary(target) == {'isFollowing': True, 'followersCount': 1, 'followingCount': 0}
    assert summary(owner) == {'isFollowing': False, 'followersCount': 0, 'followingCount': 1}
    assert summary(target, bob)['isFollowing'] is False
    followers = request('GET', target + '/followers', session=bob)[1]
    assert followers['total'] == 1 and not followers['hasMore']
    assert followers['items'][0]['username'] == 'security'
    assert set(followers['items'][0]) == {'username', 'displayName', 'bio', 'avatarUrl', 'followedAt'}
    followed_at = followers['items'][0]['followedAt']
    assert request('PUT', follow, session=alice)[0] == 204
    assert request('GET', target + '/followers', session=bob)[1]['items'][0]['followedAt'] == followed_at

    # A third profile exercises multi-page lists and stable timestamp ties.
    assert request('POST', '/api/auth/register', {'email': 'charlie@example.com', 'username': 'charlie',
        'password': 'Security123!@', 'displayName': 'Charlie'})[0] == 201
    assert request('PUT', '/api/users/charlie/follow', session=alice)[0] == 204
    sql(f"USE [{database}]; UPDATE RelacoesSeguir SET CreatedAt='2026-01-01T00:00:00' WHERE SeguidorId=(SELECT Id FROM AspNetUsers WHERE UserName='security');")
    all_items = request('GET', owner + '/following', session=alice)[1]
    assert all_items['total'] == 2 and {x['username'] for x in all_items['items']} == {'bob', 'charlie'}
    pages = [request('GET', owner + f'/following?limit=1&offset={i}', session=bob)[1] for i in range(2)]
    assert [p['items'][0] for p in pages] == all_items['items']
    assert pages[0]['hasMore'] and not pages[1]['hasMore']
    assert request('GET', owner + '/following?offset=2', session=alice)[1]['items'] == []
    assert request('PUT', follow, session=alice)[0] == 204
    assert request('GET', owner + '/following', session=bob)[1] == all_items

    # Reverse relationships are independent; Bob cannot remove Alice's follow.
    assert request('PUT', owner + '/follow', session=bob)[0] == 204
    assert summary(owner, bob) == {'isFollowing': True, 'followersCount': 1, 'followingCount': 2}
    for _ in range(2):
        assert request('DELETE', owner + '/follow', session=bob)[0] == 204
    assert summary(target)['isFollowing'] is True
    assert summary(owner)['followersCount'] == 0
    restart_api()
    assert summary(target) == {'isFollowing': True, 'followersCount': 1, 'followingCount': 0}

    # SQL also rejects self-follow independently of application validation.
    sql(f"""USE [{database}];
        DECLARE @owner uniqueidentifier=(SELECT Id FROM AspNetUsers WHERE UserName='security');
        BEGIN TRY
            INSERT INTO RelacoesSeguir(SeguidorId,SeguidoId,CreatedAt) VALUES(@owner,@owner,SYSUTCDATETIME());
            THROW 51000, 'SQL accepted self-follow', 1;
        END TRY
        BEGIN CATCH
            IF ERROR_NUMBER()<>547 THROW;
        END CATCH;
        IF (SELECT COUNT(*) FROM RelacoesSeguir WHERE SeguidorId=@owner)<>2
            THROW 51000, 'Unexpected follow count after concurrent requests', 1;
        """)
    with concurrent.futures.ThreadPoolExecutor(max_workers=8) as executor:
        statuses = list(executor.map(lambda _: request('DELETE', follow, session=alice)[0], range(8)))
    assert statuses == [204] * 8, statuses
    assert summary(target) == {'isFollowing': False, 'followersCount': 0, 'followingCount': 0}
    assert request('DELETE', '/api/users/charlie/follow', session=alice)[0] == 204
    assert summary(owner)['followingCount'] == 0
    assert request('GET', owner + '/following', session=bob)[1]['total'] == 0
    print('Following checks passed: authenticated PUT/DELETE, self-follow rejection, SQL uniqueness, concurrency, independent reverse relationships, counts, pagination, persistence and safe public fields.', flush=True)
