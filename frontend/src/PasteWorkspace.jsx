import { useEffect, useRef, useState } from "react";
import {
  api,
  createPaste,
  download,
  emptyForm,
  formFromPaste,
  languages,
  newAttempt,
  pastePath,
  shouldHandleLink,
  slugFromPath,
  validateForm,
} from "./paste-client.js";

function Fields({ form, onChange, editing = false, disabled = false }) {
  const field = (name) => (event) =>
    onChange({ ...form, [name]: event.target.value });
  return (
    <fieldset disabled={disabled} className="fields">
      <div className="field-row">
        <label>
          Title
          <input
            name="title"
            aria-label="Title"
            value={form.title}
            onChange={field("title")}
            maxLength={80}
            placeholder="Untitled paste"
          />
        </label>
        <label>
          Language
          <select
            name="language"
            aria-label="Language"
            value={form.language}
            onChange={field("language")}
          >
            {languages.map((value) => (
              <option key={value}>{value}</option>
            ))}
          </select>
        </label>
      </div>
      <label>
        Content
        <textarea
          name="content"
          aria-label="Content"
          value={form.content}
          onChange={field("content")}
          maxLength={100000}
          required
          spellCheck={false}
        />
      </label>
      <span className="count">
        {form.content.length.toLocaleString()} / 100,000 characters. Trailing
        spaces and line breaks are retained.
      </span>
      <div className="field-row">
        <label>
          Visibility
          <select
            aria-label="Visibility"
            value={form.visibility}
            onChange={field("visibility")}
          >
            <option value="public">Public: listed in search</option>
            <option value="unlisted">
              Unlisted: anyone with the link can read
            </option>
          </select>
        </label>
        <label>
          Expiration
          <select
            aria-label="Expiration"
            value={form.expiration}
            onChange={field("expiration")}
          >
            {editing && <option value="keep">Keep current expiry</option>}
            <option value="never">Never</option>
            <option value="hour">In one hour</option>
            <option value="day">In one day</option>
            <option value="week">In one week</option>
          </select>
        </label>
      </div>
    </fieldset>
  );
}

function Explorer({ refresh, onNavigate }) {
  const [input, setInput] = useState("");
  const [language, setLanguage] = useState("");
  const [criteria, setCriteria] = useState({ q: "", language: "", page: 1 });
  const [result, setResult] = useState(null);
  const [message, setMessage] = useState("Loading public pastes...");
  useEffect(() => {
    const controller = new AbortController();
    let current = true;
    setMessage("Loading public pastes...");
    const query = new URLSearchParams({ ...criteria, pageSize: "12" });
    api(`/pastes?${query}`, { signal: controller.signal })
      .then((data) => {
        if (!current) return;
        const last = Math.max(1, Math.ceil(data.total / data.pageSize));
        if (criteria.page > last) {
          setCriteria((value) => ({ ...value, page: last }));
          return;
        }
        setResult(data);
        setMessage("");
      })
      .catch((error) => {
        if (current && error.name !== "AbortError") {
          setResult(null);
          setMessage(error.message);
        }
      });
    return () => {
      current = false;
      controller.abort();
    };
  }, [criteria, refresh]);
  function search(event) {
    event.preventDefault();
    if (input.trim().length === 1) {
      setMessage("Use at least two characters, or clear the search.");
      return;
    }
    setCriteria({ q: input.trim(), language, page: 1 });
  }
  return (
    <aside className="recent" aria-label="Public paste browser">
      <h2>Public pastes</h2>
      <p className="muted">
        Search matches literal text. Unlisted and expired pastes are excluded.
      </p>
      <form className="search-form" onSubmit={search}>
        <label>
          Search public pastes
          <input
            value={input}
            onChange={(event) => setInput(event.target.value)}
            maxLength={120}
          />
        </label>
        <label>
          Filter language
          <select
            aria-label="Filter language"
            value={language}
            onChange={(event) => setLanguage(event.target.value)}
          >
            <option value="">All languages</option>
            {languages.map((value) => (
              <option key={value}>{value}</option>
            ))}
          </select>
        </label>
        <button className="primary">Search</button>
      </form>
      {message && <p role="status">{message}</p>}
      {!message && result && (
        <>
          <p className="muted">
            {result.total} matches · page {result.page} of{" "}
            {Math.max(1, Math.ceil(result.total / result.pageSize))}
          </p>
          {result.items.length === 0 ? (
            <p>No public pastes match.</p>
          ) : (
            <ul>
              {result.items.map((paste) => (
                <li key={paste.slug}>
                  <a
                    href={pastePath(paste.slug)}
                    onClick={(event) => {
                      if (shouldHandleLink(event)) {
                        event.preventDefault();
                        onNavigate(paste.slug);
                      }
                    }}
                  >
                    <strong>{paste.title}</strong>
                    <span>
                      {paste.language} ·{" "}
                      {new Date(paste.createdAt).toLocaleDateString()}
                    </span>
                    <span className="preview">{paste.preview}</span>
                  </a>
                </li>
              ))}
            </ul>
          )}
          <div className="actions pager">
            <button
              disabled={result.page <= 1}
              onClick={() =>
                setCriteria((value) => ({ ...value, page: value.page - 1 }))
              }
            >
              Previous
            </button>
            <button
              disabled={result.page * result.pageSize >= result.total}
              onClick={() =>
                setCriteria((value) => ({ ...value, page: value.page + 1 }))
              }
            >
              Next
            </button>
          </div>
        </>
      )}
    </aside>
  );
}

export default function PasteWorkspace() {
  const [slug, setSlug] = useState(() => slugFromPath(location.pathname));
  const [paste, setPaste] = useState(null);
  const [loading, setLoading] = useState(false);
  const [newForm, setNewForm] = useState(emptyForm);
  const [attempt, setAttempt] = useState(null);
  const [receipt, setReceipt] = useState(null);
  const [receiptSaved, setReceiptSaved] = useState(false);
  const [edit, setEdit] = useState(null);
  const [editKey, setEditKey] = useState("");
  const [busy, setBusy] = useState(false);
  const busyRef = useRef(false);
  const [notice, setNotice] = useState(null);
  const [refresh, setRefresh] = useState(0);
  const atRisk =
    busy ||
    attempt !== null ||
    edit !== null ||
    JSON.stringify(newForm) !== JSON.stringify(emptyForm()) ||
    (receipt && !receiptSaved);
  const navigation = useRef({ atRisk, slug });
  useEffect(() => {
    navigation.current = { atRisk, slug };
  }, [atRisk, slug]);
  useEffect(() => {
    const beforeUnload = (event) => {
      if (navigation.current.atRisk) {
        event.preventDefault();
        event.returnValue = "";
      }
    };
    const pop = () => {
      if (busyRef.current) {
        history.pushState(
          {},
          "",
          navigation.current.slug ? pastePath(navigation.current.slug) : "/",
        );
        return;
      }
      if (
        navigation.current.atRisk &&
        !window.confirm(
          "A draft, unresolved creation or unsaved receipt is open. Leave it? Export recovery information first if needed.",
        )
      ) {
        history.pushState(
          {},
          "",
          navigation.current.slug ? pastePath(navigation.current.slug) : "/",
        );
        return;
      }
      setSlug(slugFromPath(location.pathname));
      setEdit(null);
      setAttempt(null);
      setNewForm(emptyForm());
      setEditKey("");
      setNotice(null);
    };
    addEventListener("beforeunload", beforeUnload);
    addEventListener("popstate", pop);
    return () => {
      removeEventListener("beforeunload", beforeUnload);
      removeEventListener("popstate", pop);
    };
  }, []);
  useEffect(() => {
    if (!slug) {
      setPaste(null);
      setLoading(false);
      return;
    }
    const controller = new AbortController();
    let current = true;
    setLoading(true);
    setPaste(null);
    api(`/pastes/${encodeURIComponent(slug)}`, { signal: controller.signal })
      .then((data) => {
        if (current) setPaste(data);
      })
      .catch((error) => {
        if (current && error.name !== "AbortError")
          setNotice({ type: "error", text: error.message });
      })
      .finally(() => {
        if (current) setLoading(false);
      });
    return () => {
      current = false;
      controller.abort();
    };
  }, [slug]);

  function navigate(nextSlug, { force = false } = {}) {
    if (busyRef.current && !force) return false;
    if (
      !force &&
      atRisk &&
      !window.confirm(
        "Leave this draft or unsaved receipt? Export recovery information first if needed.",
      )
    )
      return false;
    history.pushState({}, "", nextSlug ? pastePath(nextSlug) : "/");
    setSlug(nextSlug);
    setEdit(null);
    setEditKey("");
    setAttempt(null);
    setNewForm(emptyForm());
    setNotice(null);
    return true;
  }
  async function run(action) {
    if (busyRef.current) return;
    busyRef.current = true;
    setBusy(true);
    setNotice(null);
    try {
      await action();
    } catch (error) {
      setNotice({ type: "error", text: error.message });
    } finally {
      busyRef.current = false;
      setBusy(false);
    }
  }
  function exportJson(name, data) {
    download(name, JSON.stringify(data, null, 2));
  }
  function exportDraft() {
    exportJson("paste-draft-recovery.json", {
      kind: "manual-recovery-draft",
      slug,
      edit,
      newForm,
      attempt,
      instruction:
        "Contains text and possibly an edit key. Keep locally. An unresolved creation must retry this exact body, request ID and key.",
    });
  }
  function submitCreate(event) {
    event.preventDefault();
    run(async () => {
      if (receipt && !receiptSaved)
        throw new Error(
          "Download the previous edit-key receipt before creating another paste.",
        );
      const selected = attempt || newAttempt(newForm);
      setAttempt(selected);
      const result = await createPaste(selected);
      const saved = {
        ...result,
        key: selected.key,
        requestId: selected.requestId,
        url: `${location.origin}${pastePath(result.slug)}`,
      };
      setReceipt(saved);
      setReceiptSaved(false);
      navigate(result.slug, { force: true });
      setRefresh((value) => value + 1);
      setNotice({
        type: "success",
        text: result.replayed
          ? "The earlier creation was found. No second paste was created. Save this edit-key receipt."
          : "Paste created. Save your edit-key receipt before leaving this tab.",
      });
    });
  }
  function saveEdit(event) {
    event.preventDefault();
    run(async () => {
      validateForm(edit.form, true);
      const result = await api(`/pastes/${encodeURIComponent(slug)}`, {
        method: "PUT",
        body: edit.form,
        key: editKey,
        version: edit.version,
      });
      setPaste(result);
      setEdit(null);
      setRefresh((value) => value + 1);
      setNotice({ type: "success", text: `Saved version ${result.version}.` });
    });
  }
  function checkCurrent() {
    run(async () => {
      const current = await api(`/pastes/${encodeURIComponent(slug)}`);
      setPaste(current);
      setNotice({
        type: "success",
        text: `Current version ${current.version} loaded below. Your edit draft still expects version ${edit?.version ?? current.version}. Compare before choosing a new version.`,
      });
    });
  }
  function removePaste() {
    if (
      !window.confirm(
        `Delete "${paste.title}" at version ${paste.version}? This cannot be undone through this app.`,
      )
    )
      return;
    run(async () => {
      await api(`/pastes/${encodeURIComponent(slug)}`, {
        method: "DELETE",
        key: editKey,
        version: paste.version,
      });
      if (receipt?.slug === slug) {
        setReceipt(null);
        setReceiptSaved(true);
      }
      navigate(null, { force: true });
      setRefresh((value) => value + 1);
      setNotice({
        type: "success",
        text: "Paste deleted. Its creation receipt remains in server history to prevent accidental recreation on retry.",
      });
    });
  }
  function fork() {
    const selected = {
      ...formFromPaste(paste),
      title: `Fork: ${paste.title}`.slice(0, 80),
      expiration: "never",
    };
    if (navigate(null)) setNewForm(selected);
  }
  return (
    <div className="shell">
      <header className="topbar">
        <button className="brand" onClick={() => navigate(null)}>
          Paste workspace
        </button>
        <button
          className="new-button"
          disabled={busy}
          onClick={() => navigate(null)}
        >
          New paste
        </button>
      </header>
      {notice && (
        <div
          className={`status ${notice.type}`}
          role={notice.type === "error" ? "alert" : "status"}
        >
          {notice.text}
        </div>
      )}
      {receipt && (
        <section className="receipt" aria-label="Creation receipt">
          <h2>Your edit-key receipt</h2>
          <p>
            The key controls edits and deletion. Save it locally. The server
            cannot recover the original key.
          </p>
          <label>
            Created paste link
            <input readOnly value={receipt.url} />
          </label>
          <label>
            Saved edit key
            <input readOnly value={receipt.key} />
          </label>
          <div className="actions">
            <button
              onClick={() => {
                exportJson("paste-edit-key-receipt.json", receipt);
                setReceiptSaved(true);
              }}
            >
              Download receipt
            </button>
            <button
              onClick={() =>
                run(async () => {
                  await navigator.clipboard.writeText(receipt.key);
                  setNotice({
                    type: "success",
                    text: "Edit key copied. Save it somewhere you control, or download the receipt.",
                  });
                })
              }
            >
              Copy edit key
            </button>
            <button disabled={!receiptSaved} onClick={() => setReceipt(null)}>
              Dismiss saved receipt
            </button>
          </div>
        </section>
      )}
      <main className="workspace">
        <section className="main-panel">
          {!slug ? (
            <form
              className="editor"
              onSubmit={submitCreate}
              aria-label="Create paste"
            >
              <div>
                <p className="eyebrow">A small, local text workspace</p>
                <h1>Create a paste</h1>
                <p className="muted">
                  Public pastes appear in search. Unlisted pastes can be read by
                  anyone with their link. Keep secrets out of paste content.
                </p>
              </div>
              <Fields
                form={newForm}
                onChange={setNewForm}
                disabled={busy || attempt !== null}
              />
              {attempt && (
                <div className="recovery">
                  <h2>Creation request retained</h2>
                  <p>
                    The form is frozen until this request is resolved. Retry
                    sends the exact same text, request ID and key. It cannot
                    create a second paste if the first request was accepted.
                  </p>
                  <p>
                    Request: <code>{attempt.requestId}</code>
                  </p>
                  <button type="button" onClick={exportDraft}>
                    Download unresolved creation
                  </button>
                  <button
                    type="button"
                    disabled={busy}
                    onClick={() => {
                      if (
                        window.confirm(
                          "The server may already have created this paste. Export this request first. Abandoning it can leave a paste you cannot recover. Start a different request?",
                        )
                      )
                        setAttempt(null);
                    }}
                  >
                    Abandon retained request
                  </button>
                </div>
              )}
              <div className="actions">
                <button type="button" onClick={exportDraft}>
                  Export draft
                </button>
                <button className="primary" disabled={busy}>
                  {busy
                    ? "Waiting for acceptance..."
                    : attempt
                      ? "Retry exact creation"
                      : "Create paste"}
                </button>
              </div>
            </form>
          ) : loading ? (
            <div className="empty-state" role="status">
              Loading paste...
            </div>
          ) : !paste ? (
            <div className="empty-state">
              <h1>Paste unavailable</h1>
              <p>
                The paste may be expired, deleted or temporarily unreadable.
                Check the message above.
              </p>
              <button onClick={checkCurrent} disabled={busy}>
                Try loading again
              </button>
            </div>
          ) : (
            <article className="paste-view">
              <div className="paste-header">
                <div>
                  <p className="eyebrow">
                    {paste.language} · {paste.visibility} · version{" "}
                    {paste.version}
                  </p>
                  <h1>{paste.title}</h1>
                  <p>Created {new Date(paste.createdAt).toLocaleString()}</p>
                  <p className="expires">
                    {paste.expiresAt
                      ? `Expires ${new Date(paste.expiresAt).toLocaleString()}`
                      : "No expiration"}
                  </p>
                </div>
              </div>
              <div className="actions">
                <button
                  onClick={() =>
                    run(async () => {
                      await navigator.clipboard.writeText(paste.content);
                      setNotice({ type: "success", text: "Content copied." });
                    })
                  }
                >
                  Copy content
                </button>
                <button
                  onClick={() =>
                    download(
                      `${paste.slug}.txt`,
                      paste.content,
                      "text/plain;charset=utf-8",
                    )
                  }
                >
                  Download text
                </button>
                <a
                  href={`/api/pastes/${encodeURIComponent(paste.slug)}/raw`}
                  target="_blank"
                  rel="noreferrer"
                >
                  Open raw text
                </a>
                <button disabled={busy} onClick={fork}>
                  Fork as new paste
                </button>
              </div>
              <pre className="paste-code" aria-label="Current saved content">
                {paste.content}
              </pre>
              <section className="ownership">
                <h2>Edit or delete</h2>
                <p className="muted">
                  Use the exact saved key. Older user-selected keys retain their
                  original strength. The key stays in this tab’s memory.
                </p>
                <label>
                  Edit key
                  <input
                    type="password"
                    value={editKey}
                    onChange={(event) => setEditKey(event.target.value)}
                    maxLength={256}
                    autoComplete="off"
                    spellCheck={false}
                  />
                </label>
                {!edit && (
                  <div className="actions">
                    <button
                      disabled={busy}
                      onClick={() =>
                        setEdit({
                          version: paste.version,
                          form: formFromPaste(paste),
                        })
                      }
                    >
                      Open edit draft
                    </button>
                    <button disabled={busy} onClick={checkCurrent}>
                      Check current version
                    </button>
                    <button
                      className="danger"
                      disabled={busy || !editKey}
                      onClick={removePaste}
                    >
                      Delete paste
                    </button>
                  </div>
                )}
              </section>
              {edit && (
                <form
                  className="edit-form"
                  onSubmit={saveEdit}
                  aria-label="Edit paste"
                >
                  <h2>Edit draft · expected version {edit.version}</h2>
                  <Fields
                    form={edit.form}
                    onChange={(form) =>
                      setEdit((value) => ({ ...value, form }))
                    }
                    editing
                    disabled={busy}
                  />
                  {paste.version !== edit.version && (
                    <div className="recovery">
                      <p>
                        The saved paste above is now version {paste.version}.
                        Your draft still expects {edit.version}.
                      </p>
                      <button
                        type="button"
                        disabled={busy}
                        onClick={() => {
                          if (
                            window.confirm(
                              "After comparing the saved content above, keep your draft and use the current version for the next save?",
                            )
                          )
                            setEdit((value) => ({
                              ...value,
                              version: paste.version,
                            }));
                        }}
                      >
                        Use reviewed current version
                      </button>
                      <button
                        type="button"
                        disabled={busy}
                        onClick={() => {
                          if (
                            window.confirm(
                              "Discard this draft and load the current saved content?",
                            )
                          )
                            setEdit({
                              version: paste.version,
                              form: formFromPaste(paste),
                            });
                        }}
                      >
                        Replace draft with current data
                      </button>
                    </div>
                  )}
                  <div className="actions">
                    <button type="button" onClick={exportDraft}>
                      Export edit draft
                    </button>
                    <button
                      type="button"
                      disabled={busy}
                      onClick={checkCurrent}
                    >
                      Check current version
                    </button>
                    <button
                      type="button"
                      disabled={busy}
                      onClick={() => {
                        if (window.confirm("Discard this edit draft?"))
                          setEdit(null);
                      }}
                    >
                      Cancel edit
                    </button>
                    <button className="primary" disabled={busy || !editKey}>
                      {busy ? "Waiting for acceptance..." : "Save edit"}
                    </button>
                  </div>
                </form>
              )}
            </article>
          )}
        </section>
        <Explorer refresh={refresh} onNavigate={navigate} />
      </main>
      <footer>
        Local learning workspace · Files stay in the configured server data
        directory · Unlisted links are shareable, not private accounts
      </footer>
    </div>
  );
}
