"""Browser checks against the built app and disposable local data; no live accounts."""
import json
import os
from pathlib import Path
import re
import sys
import time
import uuid

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = Path(os.environ.get('PASTEBIN_BROWSER_OUTPUT', ROOT / 'verification-output' / 'browser')).resolve()
PORT = int(os.environ.get('PASTEBIN_BROWSER_PORT', '5586'))
os.environ['PASTEBIN_HTTP_OUTPUT'] = str(OUTPUT)
from verify_http import key, request, require, server  # Shared own-process lifecycle.
from playwright.sync_api import sync_playwright

checks = []
output_ready = False
diagnostic_page = None


def record(name, action):
    started = time.monotonic()
    try:
        action()
        checks.append({'name': name, 'passed': True, 'seconds': round(time.monotonic() - started, 3)})
        print('PASS', name, flush=True)
    except Exception as error:
        checks.append({'name': name, 'passed': False, 'error': repr(error)})
        if diagnostic_page is not None:
            try:
                diagnostic_page.screenshot(path=str(OUTPUT / 'failure.png'), full_page=True, timeout=15000)
                (OUTPUT / 'failure.html').write_text(diagnostic_page.content(), encoding='utf-8')
                labels = diagnostic_page.locator('input,textarea,select,form').evaluate_all('elements => elements.map(e => ({tag:e.tagName,aria:e.getAttribute("aria-label"),labels:[...(e.labels||[])].map(l=>l.textContent)}))')
                (OUTPUT / 'failure-labels.json').write_text(json.dumps(labels, indent=2), encoding='utf-8')
            except Exception as diagnostic_error:
                checks[-1]['diagnosticError'] = repr(diagnostic_error)
        raise


def main():
    global output_ready
    global diagnostic_page
    require((ROOT / 'frontend/dist/index.html').is_file(), 'Build frontend before browser verification.')
    require(not OUTPUT.exists() or not any(OUTPUT.iterdir()), 'Use a new empty browser output directory.')
    OUTPUT.mkdir(parents=True, exist_ok=True)
    output_ready = True
    (OUTPUT / 'images').mkdir()
    base = f'http://127.0.0.1:{PORT}'
    data_dir = OUTPUT / 'workspace'
    with server(data_dir, PORT, 'browser', limit=1000), sync_playwright() as playwright:
        executable = os.environ.get('ASTRA_BROWSER_EXECUTABLE')
        browser = playwright.chromium.launch(headless=True, **({'executable_path': executable} if executable else {}))
        context = browser.new_context(viewport={'width': 1440, 'height': 1000}, accept_downloads=True,
                                      permissions=['clipboard-read', 'clipboard-write'])
        page = context.new_page()
        diagnostic_page = page
        page.set_default_timeout(60000)
        dialogs = {'accept': True}
        page.on('dialog', lambda dialog: dialog.accept() if dialogs['accept'] else dialog.dismiss())
        page_errors = []
        page.on('pageerror', lambda error: page_errors.append(str(error)))
        active = data_dir / 'workspace-v1.json'

        def state():
            return json.loads(active.read_text(encoding='utf-8'))

        def screenshot(name):
            page.screenshot(path=str(OUTPUT / 'images' / name), full_page=True)

        def download_receipt():
            capability = page.get_by_label('Saved edit key', exact=True).input_value()
            with page.expect_download() as pending:
                page.get_by_role('button', name='Download receipt', exact=True).click()
            item = pending.value
            destination = OUTPUT / f'receipt-{uuid.uuid4().hex}.json'
            item.save_as(str(destination))
            receipt = json.loads(destination.read_text(encoding='utf-8'))
            require(receipt['key'] == capability, 'Downloaded receipt differs from shown key.')
            page.get_by_role('button', name='Dismiss saved receipt', exact=True).click()
            return capability

        def create(title, content, visibility='public'):
            page.get_by_role('button', name='New paste', exact=True).click()
            form = page.get_by_role('form', name='Create paste')
            form.get_by_label('Title', exact=True).fill(title)
            form.get_by_label('Content', exact=True).fill(content)
            form.get_by_label('Visibility', exact=True).select_option(visibility)
            form.get_by_role('button', name='Create paste', exact=True).click()
            page.wait_for_url(re.compile(r'/p/[A-Za-z0-9_-]+$'), timeout=180000)
            page.get_by_role('heading', name=title, exact=True).wait_for()
            return page.url.rsplit('/', 1)[1], download_receipt()

        def landing():
            before = active.read_bytes()
            page.goto(base, wait_until='networkidle', timeout=180000)
            page.get_by_role('heading', name='Create a paste', exact=True).wait_for()
            page.get_by_text('No public pastes match.', exact=True).wait_for()
            require(active.read_bytes() == before, 'Landing page wrote data.')
            screenshot('01-empty-workspace.png')
        record('Empty landing page does not create sample records or write on read', landing)

        saved = {}
        def initial_create():
            slug, capability = create('Browser sample', 'const value = 1;  \n\n')
            saved.update(slug=slug, key=capability)
            require(len(state()['pastes']) == 1 and state()['pastes'][0]['version'] == 1, 'Initial create not persisted.')
            screenshot('02-created-paste.png')
        record('Create, show receipt and download the exact edit key', initial_create)

        def raw_and_download():
            before = active.read_bytes()
            content = 'const value = 1;  \n\n'
            with page.expect_download() as pending:
                page.get_by_role('button', name='Download text', exact=True).click()
            pending.value.save_as(str(OUTPUT / 'downloaded-content.txt'))
            require((OUTPUT / 'downloaded-content.txt').read_bytes() == content.encode(), 'Download changed trailing whitespace.')
            response = context.request.get(f'{base}/api/pastes/{saved["slug"]}/raw')
            require(response.body() == content.encode(), 'Raw endpoint changed bytes.')
            page.get_by_role('button', name='Copy content', exact=True).click()
            page.get_by_text('Content copied.', exact=True).wait_for()
            clipboard = page.evaluate('navigator.clipboard.readText()')
            (OUTPUT / 'clipboard-content.json').write_text(json.dumps({'stored': content, 'clipboard': clipboard}, indent=2), encoding='utf-8')
            require(clipboard.replace('\r\n', '\n') == content, 'Clipboard changed text beyond platform line-ending conversion.')
            require(active.read_bytes() == before, 'Copy or download wrote server state.')
        record('Clipboard text and exact raw/download content are preserved without writes', raw_and_download)

        def successful_edit():
            page.get_by_label('Edit key', exact=True).fill(saved['key'])
            page.get_by_role('button', name='Open edit draft', exact=True).click()
            edit = page.get_by_role('form', name='Edit paste')
            edit.get_by_label('Content', exact=True).fill('first accepted edit  \n')
            edit.get_by_role('button', name='Save edit', exact=True).click()
            page.get_by_text('Saved version 2.', exact=True).wait_for()
            require(state()['pastes'][0]['content'] == 'first accepted edit  \n', 'Edited content not persisted.')
        record('Versioned edit persists exact text and closes only after acceptance', successful_edit)

        def bad_key():
            page.get_by_role('button', name='Open edit draft', exact=True).click()
            edit = page.get_by_role('form', name='Edit paste')
            edit.get_by_label('Content', exact=True).fill('draft with wrong key')
            page.get_by_label('Edit key', exact=True).fill('incorrect-old-key')
            before = active.read_bytes()
            edit.get_by_role('button', name='Save edit', exact=True).click()
            page.get_by_role('alert').filter(has_text='edit key is incorrect').wait_for()
            require(edit.get_by_label('Content', exact=True).input_value() == 'draft with wrong key', 'Rejected edit lost draft.')
            require(active.read_bytes() == before, 'Wrong key changed data.')
            screenshot('03-rejected-edit.png')
            page.get_by_label('Edit key', exact=True).fill(saved['key'])
            edit.get_by_role('button', name='Cancel edit', exact=True).click()
        record('Incorrect capability rejects edit and retains the draft', bad_key)

        def conflict():
            other = context.new_page(); other.set_default_timeout(60000); other.on('dialog', lambda dialog: dialog.accept())
            other.goto(page.url, wait_until='networkidle', timeout=180000)
            other.get_by_label('Edit key', exact=True).fill(saved['key'])
            other.get_by_role('button', name='Open edit draft', exact=True).click()
            other_form = other.get_by_role('form', name='Edit paste')
            other_form.get_by_label('Content', exact=True).fill('second tab retained draft')
            page.bring_to_front()
            page.get_by_role('button', name='Open edit draft', exact=True).click()
            edit = page.get_by_role('form', name='Edit paste')
            edit.get_by_label('Content', exact=True).fill('first tab winner')
            edit.get_by_role('button', name='Save edit', exact=True).click()
            page.get_by_text('Saved version 3.', exact=True).wait_for()
            before = active.read_bytes()
            other.bring_to_front()
            other_form.get_by_role('button', name='Save edit', exact=True).click()
            other.get_by_role('alert').filter(has_text='changed after this draft').wait_for()
            require(other_form.get_by_label('Content', exact=True).input_value() == 'second tab retained draft' and active.read_bytes() == before, 'Stale draft was lost or accepted.')
            other_form.get_by_role('button', name='Check current version', exact=True).click()
            other.get_by_text('The saved paste above is now version 3.', exact=False).wait_for()
            other.screenshot(path=str(OUTPUT / 'images/04-version-conflict.png'), full_page=True)
            other_form.get_by_role('button', name='Use reviewed current version', exact=True).click()
            other_form.get_by_role('button', name='Save edit', exact=True).click()
            other.get_by_text('Saved version 4.', exact=True).wait_for()
            require(state()['pastes'][0]['content'] == 'second tab retained draft', 'Reviewed retry did not persist.')
            other.close(); page.reload(wait_until='networkidle', timeout=180000)
        record('Two-tab conflict retains text and requires explicit current-version review', conflict)

        def draft_guard():
            page.get_by_role('button', name='Open edit draft', exact=True).click()
            edit = page.get_by_role('form', name='Edit paste')
            edit.get_by_label('Content', exact=True).fill('exported unsaved draft')
            dialogs['accept'] = False
            page.get_by_role('button', name='New paste', exact=True).click()
            require(edit.get_by_label('Content', exact=True).input_value() == 'exported unsaved draft', 'Cancelled navigation discarded draft.')
            dialogs['accept'] = True
            with page.expect_download() as pending:
                edit.get_by_role('button', name='Export edit draft', exact=True).click()
            pending.value.save_as(str(OUTPUT / 'edit-draft.json'))
            require(json.loads((OUTPUT / 'edit-draft.json').read_text())['edit']['form']['content'] == 'exported unsaved draft', 'Draft export differs.')
            edit.get_by_role('button', name='Cancel edit', exact=True).click()
        record('Dirty-navigation guard and draft export preserve unfinished work', draft_guard)

        def lost_creation():
            page.get_by_role('button', name='New paste', exact=True).click()
            form = page.get_by_role('form', name='Create paste')
            form.get_by_label('Title', exact=True).fill('Recovered creation')
            form.get_by_label('Content', exact=True).fill('accepted once despite a lost response')
            before_count = len(state()['pastes'])
            captures = []
            def intercept(route):
                if route.request.method != 'POST':
                    route.continue_(); return
                captures.append({'body': route.request.post_data, 'key': route.request.headers.get('x-paste-key'), 'id': route.request.headers.get('x-request-id')})
                if len(captures) == 1:
                    accepted = route.fetch()
                    require(accepted.status == 201, 'Fault fixture was not accepted by server.')
                    route.abort('failed')
                else:
                    route.continue_()
            page.route('**/api/pastes', intercept)
            form.get_by_role('button', name='Create paste', exact=True).click()
            page.get_by_role('alert').filter(has_text='response was not received').wait_for()
            require(form.get_by_label('Content', exact=True).is_disabled(), 'Unresolved creation was editable.')
            screenshot('05-retained-creation.png')
            form.get_by_role('button', name='Retry exact creation', exact=True).click()
            page.wait_for_url(re.compile(r'/p/[A-Za-z0-9_-]+$'), timeout=180000)
            page.get_by_role('heading', name='Recovered creation', exact=True).wait_for()
            require(len(captures) == 2 and captures[0] == captures[1] and len(state()['pastes']) == before_count + 1, 'Retry changed its request or duplicated content.')
            page.unroute('**/api/pastes', intercept)
            download_receipt()
        record('Lost creation acknowledgement retries the exact request and creates only once', lost_creation)

        hidden = {}
        def unlisted():
            slug, capability = create('Unlisted browser fixture', 'link readable', 'unlisted')
            hidden.update(slug=slug, key=capability)
            result = request('/api/pastes?q=Unlisted', port=PORT)
            require(result[0] == 200 and result[2]['total'] == 0, 'Unlisted paste appeared in search.')
            require(request('/api/pastes/' + slug, port=PORT)[0] == 200, 'Unlisted direct read failed.')
        record('Unlisted paste remains link-readable and excluded from public search', unlisted)

        def search():
            for index in range(14):
                status, _, _, _ = request('/api/pastes', 'POST', {'title': f'Search fixture {index:02}', 'content': 'literal [x] ' + 'a' * 200, 'language': 'sql', 'visibility': 'public', 'expiration': 'never'}, {'X-Paste-Key': key(), 'X-Request-Id': str(uuid.uuid4())}, port=PORT)
                require(status == 201, 'Search fixture creation failed.')
            side = page.get_by_role('complementary', name='Public paste browser')
            side.get_by_label('Search public pastes', exact=True).fill('[x]')
            side.get_by_label('Filter language', exact=True).select_option('sql')
            side.get_by_role('button', name='Search', exact=True).click()
            side.get_by_text('14 matches · page 1 of 2', exact=True).wait_for()
            require(side.get_by_role('link').count() == 12, 'First page row count wrong.')
            side.get_by_role('button', name='Next', exact=True).click()
            side.get_by_text('14 matches · page 2 of 2', exact=True).wait_for()
            require(side.get_by_role('link').count() == 2, 'Last page row count wrong.')
            screenshot('06-search-pagination.png')
            side.get_by_label('Search public pastes', exact=True).fill('.*')
            side.get_by_role('button', name='Search', exact=True).click()
            side.get_by_text('No public pastes match.', exact=True).wait_for()
        record('Literal search, language filter and pagination render actual server results', search)

        def fork():
            before = active.read_bytes()
            page.get_by_role('button', name='Fork as new paste', exact=True).click()
            form = page.get_by_role('form', name='Create paste')
            require(form.get_by_label('Content', exact=True).input_value() == 'link readable', 'Fork did not copy exact content.')
            require(form.get_by_label('Expiration', exact=True).input_value() == 'never' and active.read_bytes() == before, 'Fork wrote data before submission.')
        record('Fork creates a new draft without implicitly writing or retaining an old expiry', fork)

        def responsive():
            for width in [1440, 800, 390]:
                page.set_viewport_size({'width': width, 'height': 950})
                page.get_by_role('heading', name='Create a paste', exact=True).wait_for()
                require(page.evaluate('document.documentElement.scrollWidth <= window.innerWidth + 1'), f'Horizontal overflow at {width}.')
                screenshot(f'07-form-{width}.png')
            page.set_viewport_size({'width': 1440, 'height': 1000})
        record('Create form is usable without horizontal overflow at three viewport widths', responsive)

        def rejected_delete():
            page.goto(f'{base}/p/{hidden["slug"]}', wait_until='networkidle', timeout=180000)
            before = active.read_bytes()
            page.get_by_label('Edit key', exact=True).fill('incorrect-old-key')
            page.get_by_role('button', name='Delete paste', exact=True).click()
            page.get_by_role('alert').filter(has_text='edit key is incorrect').wait_for()
            require(active.read_bytes() == before and page.get_by_role('heading', name='Unlisted browser fixture', exact=True).is_visible(), 'Rejected deletion removed content.')
            page.get_by_label('Edit key', exact=True).fill(hidden['key'])
            page.get_by_role('button', name='Delete paste', exact=True).click()
            page.get_by_role('heading', name='Create a paste', exact=True).wait_for()
            require(request('/api/pastes/' + hidden['slug'], port=PORT)[0] == 404, 'Accepted deletion left content readable.')
        record('Delete rejects a wrong key and removes only the confirmed version with the correct key', rejected_delete)

        def reload():
            before = active.read_bytes()
            page.goto(f'{base}/p/{saved["slug"]}', wait_until='networkidle', timeout=180000)
            page.get_by_label('Current saved content', exact=True).wait_for()
            require(page.get_by_label('Current saved content', exact=True).inner_text() == 'second tab retained draft', 'Reload lost accepted content.')
            require(active.read_bytes() == before and not page_errors, f'Reload wrote data or page errors occurred: {page_errors}')
        record('Reload preserves accepted content and the browser has no uncaught application errors', reload)
        browser.close()


if __name__ == '__main__':
    fatal = None
    try:
        main()
    except Exception as error:
        fatal = repr(error)
        print('FATAL', fatal, flush=True)
    passed = fatal is None and len(checks) == 14 and all(item['passed'] for item in checks)
    if output_ready:
        (OUTPUT / 'results.json').write_text(json.dumps({'passed': passed, 'total': len(checks), 'expected': 14, 'checks': checks, 'fatal': fatal}, indent=2), encoding='utf-8')
    sys.exit(0 if passed else 1)
