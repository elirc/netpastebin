"""Exercise the real loopback HTTP server against disposable, retained evidence data."""
import base64
import contextlib
import hashlib
import json
import os
from pathlib import Path
import secrets
import socket
import subprocess
import sys
import time
import urllib.error
import urllib.request
import uuid

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = Path(os.environ.get('PASTEBIN_HTTP_OUTPUT', ROOT / 'verification-output' / 'http')).resolve()
DLL = Path(os.environ.get('PASTEBIN_DLL', ROOT / 'backend' / 'bin' / 'Debug' / 'net10.0' / 'PastebinClone.Api.dll')).resolve()
PORT = int(os.environ.get('PASTEBIN_HTTP_PORT', '5584'))
checks = []
output_ready = False


def require(condition, message):
    if not condition:
        raise AssertionError(message)


def key():
    return base64.urlsafe_b64encode(secrets.token_bytes(32)).decode().rstrip('=')


def request(path, method='GET', body=None, headers=None, raw=None, port=PORT):
    payload = raw if raw is not None else None if body is None else json.dumps(body).encode()
    actual_headers = {'Accept': 'application/json', **(headers or {})}
    if payload is not None:
        actual_headers.setdefault('Content-Type', 'application/json')
    req = urllib.request.Request(f'http://127.0.0.1:{port}{path}', data=payload, method=method, headers=actual_headers)
    try:
        response = urllib.request.urlopen(req, timeout=60)
    except urllib.error.HTTPError as error:
        response = error
    with response:
        content = response.read()
        try:
            data = json.loads(content)
        except (ValueError, UnicodeDecodeError):
            data = content.decode('utf-8', errors='replace')
        return response.status, dict(response.headers), data, content


def check(name, action):
    started = time.monotonic()
    try:
        action()
        checks.append({'name': name, 'passed': True, 'seconds': round(time.monotonic() - started, 3)})
        print('PASS', name, flush=True)
    except Exception as error:
        checks.append({'name': name, 'passed': False, 'error': repr(error), 'seconds': round(time.monotonic() - started, 3)})
        print('FAIL', name, repr(error), flush=True)


@contextlib.contextmanager
def server(data_dir, port, label, limit=240):
    with socket.socket() as probe:
        require(probe.connect_ex(('127.0.0.1', port)) != 0, f'Port {port} is occupied; no existing process will be stopped.')
    env = {**os.environ, 'PASTEBIN_PORT': str(port), 'PASTEBIN_DATA_DIR': str(data_dir),
           'PASTEBIN_REQUESTS_PER_MINUTE': str(limit), 'ASPNETCORE_ENVIRONMENT': 'Production',
           'DOTNET_NOLOGO': '1', 'DOTNET_CLI_TELEMETRY_OPTOUT': '1'}
    with (OUTPUT / f'{label}-server.log').open('w', encoding='utf-8') as log:
        process = subprocess.Popen(['dotnet', str(DLL)], cwd=ROOT / 'backend', env=env,
                                   stdout=log, stderr=subprocess.STDOUT,
                                   creationflags=subprocess.CREATE_NO_WINDOW if os.name == 'nt' else 0)
        try:
            deadline = time.monotonic() + 300
            while time.monotonic() < deadline:
                require(process.poll() is None, f'Server stopped with exit {process.returncode}. Read {label}-server.log.')
                try:
                    if request('/api/health', port=port)[0] == 200:
                        break
                except (OSError, urllib.error.URLError):
                    pass
                time.sleep(.3)
            else:
                raise TimeoutError('Server readiness exceeded 300 seconds.')
            yield process
        finally:
            if process.poll() is None:
                process.terminate()
                try:
                    process.wait(timeout=20)
                except subprocess.TimeoutExpired:
                    process.kill()
                    process.wait(timeout=20)


def main():
    global output_ready
    require(DLL.is_file(), 'Build backend before running HTTP checks.')
    require(not OUTPUT.exists() or not any(OUTPUT.iterdir()), 'Use a new empty HTTP output directory.')
    OUTPUT.mkdir(parents=True, exist_ok=True)
    output_ready = True
    data_dir = OUTPUT / 'workspace'
    original_content = 'const sample = 1;  \r\n\tlast line\n\n'
    body = {'title': 'HTTP example', 'content': original_content, 'language': 'javascript', 'visibility': 'public', 'expiration': 'never'}
    capability = key()
    headers = {'X-Paste-Key': capability, 'X-Request-Id': str(uuid.uuid4())}
    saved = {}
    with server(data_dir, PORT, 'primary'):
        active = data_dir / 'workspace-v1.json'

        def health():
            status, response_headers, data, _ = request('/api/health')
            require(status == 200 and data['status'] == 'ok', 'Health failed.')
            require(response_headers.get('X-Content-Type-Options') == 'nosniff' and response_headers.get('Cache-Control') == 'no-store', 'Required API headers missing.')
            require('Server' not in response_headers, 'Server banner exposed.')
        check('Health and API response headers', health)

        def create():
            status, response_headers, result, _ = request('/api/pastes', 'POST', body, headers)
            require(status == 201 and result['version'] == 1 and not result['replayed'], f'Creation failed: {status} {result}')
            require(response_headers.get('ETag') == '"1"' and response_headers.get('Location') == f"/api/pastes/{result['slug']}", 'Creation headers missing.')
            saved.update(result)
        check('Create returns Location and initial ETag', create)
        require('slug' in saved, 'Further checks require an accepted fixture paste.')
        path = '/api/pastes/' + saved['slug']

        def retry():
            before = active.read_bytes()
            status, _, result, _ = request('/api/pastes', 'POST', body, headers)
            require(status == 200 and result['replayed'] and result['slug'] == saved['slug'], 'Retry created another resource.')
            require(active.read_bytes() == before, 'Retry rewrote workspace.')
        check('Identical creation retry recovers receipt without writing', retry)

        def readonly():
            before = active.read_bytes()
            status, response_headers, detail, _ = request(path)
            raw_status, raw_headers, _, raw = request(path + '/raw')
            require(status == raw_status == 200 and detail['content'] == original_content and raw == original_content.encode(), 'Content fidelity failed.')
            require(response_headers.get('ETag') == '"1"' and raw_headers.get('Content-Type', '').startswith('text/plain'), 'Read contract headers failed.')
            require('editKeyHash' not in detail and 'key' not in detail and active.read_bytes() == before, 'Read leaked credentials or wrote data.')
        check('Detail and raw reads preserve bytes without counting views or leaking keys', readonly)

        def strict_json():
            before = active.read_bytes()
            variants = [b'null', b'{}', json.dumps({**body, 'extra': True}).encode(), json.dumps({**body, 'content': None}).encode(),
                        json.dumps(body).replace('"title": "HTTP example"', '"title": "First", "title": "Second"').encode(), b'{bad']
            for raw in variants:
                status, _, result, _ = request('/api/pastes', 'POST', headers={**headers, 'X-Request-Id': str(uuid.uuid4())}, raw=raw)
                require(status == 400 and isinstance(result, dict) and 'error' in result, f'Malformed request was not a structured 400: {status}')
            require(active.read_bytes() == before, 'Malformed body wrote data.')
        check('Null, missing, duplicate, unknown and malformed JSON are rejected', strict_json)

        def body_limit():
            before = active.read_bytes()
            status, _, _, _ = request('/api/pastes', 'POST', {**body, 'content': 'x' * (513 * 1024)}, {**headers, 'X-Request-Id': str(uuid.uuid4())})
            require(status == 413 and active.read_bytes() == before, f'Body limit failed: {status}')
        check('Oversized HTTP bodies cannot reach persistence', body_limit)

        def foreign_origin():
            before = active.read_bytes()
            require(request('/api/pastes', 'POST', body, {**headers, 'Origin': 'https://example.invalid'})[0] == 403, 'Foreign origin accepted.')
            require(request('/api/health', headers={'Host': f'rebound.invalid:{PORT}'})[0] == 403, 'Unexpected host accepted.')
            require(active.read_bytes() == before, 'Rejected origin changed data.')
        check('Foreign write origins and unexpected hosts are rejected', foreign_origin)

        def missing_key():
            before = active.read_bytes()
            require(request(path + '?deleteKey=' + capability, 'DELETE', headers={'If-Match': '"1"'})[0] == 400, 'URL delete key was accepted.')
            require(request(path, 'DELETE', headers={'X-Paste-Key': key(), 'If-Match': '"1"'})[0] == 404, 'Wrong capability accepted.')
            require(active.read_bytes() == before, 'Unauthorized delete changed storage.')
        check('Deletion requires the capability header; URL keys grant no access', missing_key)

        def update():
            changed = {**body, 'content': 'accepted edit  \n', 'expiration': 'keep', 'visibility': 'unlisted'}
            require(request(path, 'PUT', changed, {'X-Paste-Key': capability})[0] == 428, 'Missing precondition accepted.')
            for invalid in ['*', 'W/"1"', '1', '"1", "2"']:
                require(request(path, 'PUT', changed, {'X-Paste-Key': capability, 'If-Match': invalid})[0] == 400, 'Malformed precondition accepted.')
            status, response_headers, result, _ = request(path, 'PUT', changed, {'X-Paste-Key': capability, 'If-Match': '"1"'})
            require(status == 200 and result['version'] == 2 and response_headers.get('ETag') == '"2"', f'Update failed: {status} {result}')
            before = active.read_bytes()
            require(request(path, 'PUT', {**changed, 'content': 'stale overwrite'}, {'X-Paste-Key': capability, 'If-Match': '"1"'})[0] == 412, 'Stale edit accepted.')
            require(active.read_bytes() == before, 'Stale edit changed storage.')
        check('Update enforces quoted ETags and rejects stale drafts', update)

        def unlisted():
            status, _, page, _ = request('/api/pastes')
            require(status == 200 and page['total'] == 0 and request(path)[0] == 200, 'Unlisted visibility contract failed.')
        check('Unlisted content is directly readable but absent from public search', unlisted)

        def pagination():
            for i in range(14):
                status, _, _, _ = request('/api/pastes', 'POST', {**body, 'title': f'Page {i:02}', 'content': 'literal [x] ' + 'a' * 200}, {'X-Paste-Key': key(), 'X-Request-Id': str(uuid.uuid4())})
                require(status == 201, f'Pagination fixture failed: {status}')
            a = request('/api/pastes?q=%5Bx%5D&language=javascript&page=1&pageSize=12')[2]
            b = request('/api/pastes?q=%5Bx%5D&language=javascript&page=2&pageSize=12')[2]
            require(a['total'] == 14 and len(a['items']) == 12 and len(b['items']) == 2, 'Page boundaries wrong.')
            require(all(len(item['preview']) <= 160 and 'content' not in item for item in a['items']), 'List is not a bounded summary.')
            require(request('/api/pastes?q=.*')[2]['total'] == 0, 'Search interpreted regex.')
            require(request('/api/pastes?pageSize=51')[0] == 400 and request('/api/pastes?page=1.5')[0] == 400, 'Invalid pages accepted.')
        check('Literal search, language filtering and bounded pagination work over HTTP', pagination)

        def recovery():
            before = active.read_bytes()
            try:
                active.write_bytes(b'{malformed test fixture')
                status, _, result, _ = request(path)
                require(status == 500 and 'preserve' in result['error'].lower(), 'Corruption did not fail closed.')
                require(active.read_bytes() == b'{malformed test fixture', 'Read repaired corrupt fixture silently.')
            finally:
                active.write_bytes(before)  # Restore only this verifier-owned temporary fixture.
            require(request(path)[0] == 200, 'Restored fixture was not readable.')
        check('Corrupt storage returns a recovery error and preserves the original bytes', recovery)

        def delete():
            require(request(path, 'DELETE', headers={'X-Paste-Key': capability, 'If-Match': '"1"'})[0] == 412, 'Stale delete accepted.')
            require(request(path, 'DELETE', headers={'X-Paste-Key': capability, 'If-Match': '"2"'})[0] == 204, 'Delete failed.')
            require(request(path)[0] == 404, 'Deleted content readable.')
            require(request('/api/pastes', 'POST', body, headers)[0] == 410, 'Old request resurrected deleted paste.')
        check('Versioned deletion cannot be undone by retrying an old create', delete)

        def unknown_route():
            status, _, result, _ = request('/api/no-such-endpoint')
            require(status == 404 and isinstance(result, dict) and 'error' in result, 'Unknown API route fell through to HTML.')
        check('Unknown API routes return JSON instead of the application page', unknown_route)
        accepted_bytes = active.read_bytes()

    with server(data_dir, PORT, 'restarted'):
        check('Server restart retains accepted workspace bytes', lambda: require(active.read_bytes() == accepted_bytes and request('/api/pastes')[2]['total'] == 14, 'Restart changed accepted state.'))

    with server(OUTPUT / 'limited-workspace', PORT + 1, 'limited', limit=3):
        def rate():
            statuses = [request('/api/health', port=PORT + 1)[0] for _ in range(4)]
            status, response_headers, result, _ = request('/api/health', port=PORT + 1)
            require(429 in statuses and status == 429 and response_headers.get('Retry-After') == '60' and 'error' in result, 'Rate limit contract failed.')
        check('Rate limits return a bounded retry response', rate)

    def migration():
        legacy_key = '  older café key  '
        legacy = [{'slug': 'Abcd1234', 'title': 'Old paste', 'content': 'old trailing text  \n', 'language': 'plaintext', 'visibility': 1,
                   'createdAt': '2026-01-01T00:00:00Z', 'updatedAt': None, 'expiresAt': None,
                   'deleteKeyHash': hashlib.sha256(legacy_key.encode()).hexdigest().upper(), 'viewCount': 7, 'lastViewedAt': None}]
        source = OUTPUT / 'legacy-original.json'
        original = json.dumps(legacy, ensure_ascii=False).encode()
        source.write_bytes(original)
        destination = OUTPUT / 'migrated-workspace'
        executed = subprocess.run(['dotnet', str(DLL), 'migrate', str(source), str(destination)], cwd=ROOT / 'backend',
                                  capture_output=True, text=True, timeout=300, creationflags=subprocess.CREATE_NO_WINDOW if os.name == 'nt' else 0)
        (OUTPUT / 'migration-cli.log').write_text(executed.stdout + executed.stderr, encoding='utf-8')
        require(executed.returncode == 0 and source.read_bytes() == original, f'Migration failed: {executed.returncode}')
        backups = list(destination.glob('legacy-backup-*.json'))
        require(len(backups) == 1 and backups[0].read_bytes() == original, 'Exact recovery copy missing.')
        with server(destination, PORT, 'migrated'):
            require(request('/api/pastes/abcd1234')[0] == 200, 'Old URL case matching lost.')
            encoded = 'utf8.' + base64.urlsafe_b64encode(legacy_key.encode()).decode().rstrip('=')
            changed = {**body, 'content': 'edited migrated text', 'expiration': 'keep'}
            status, _, result, _ = request('/api/pastes/ABCD1234', 'PUT', changed, {'X-Paste-Key': encoded, 'If-Match': '"1"'})
            require(status == 200 and result['version'] == 2, f'Legacy key transport failed: {status} {result}')
        repeated = subprocess.run(['dotnet', str(DLL), 'migrate', str(source), str(destination)], cwd=ROOT / 'backend',
                                  capture_output=True, text=True, timeout=300, creationflags=subprocess.CREATE_NO_WINDOW if os.name == 'nt' else 0)
        require(repeated.returncode != 0 and source.read_bytes() == original, 'Repeated migration replaced data.')
    check('CLI migration preserves bytes and legacy UTF-8 capabilities work over HTTP', migration)


if __name__ == '__main__':
    fatal = None
    try:
        main()
    except Exception as error:
        fatal = repr(error)
        print('FATAL', fatal, flush=True)
    passed = fatal is None and len(checks) == 17 and all(item['passed'] for item in checks)
    if output_ready:
        (OUTPUT / 'results.json').write_text(json.dumps({'passed': passed, 'total': len(checks), 'expected': 17, 'checks': checks, 'fatal': fatal}, indent=2), encoding='utf-8')
    sys.exit(0 if passed else 1)
