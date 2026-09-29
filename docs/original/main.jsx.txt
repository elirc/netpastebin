import React, { useEffect, useMemo, useRef, useState } from "react";
import { createRoot } from "react-dom/client";
import "./styles.css";

const emptyForm = {
  title: "",
  content: "",
  language: "plaintext",
  visibility: 0,
  expiration: 1,
  deleteKey: ""
};

const languages = ["plaintext", "csharp", "javascript", "typescript", "html", "css", "sql", "json", "markdown"];
const expirations = [
  { value: 0, label: "1 hour" },
  { value: 1, label: "1 day" },
  { value: 2, label: "1 week" },
  { value: 3, label: "Never" }
];

function App() {
  const [route, setRoute] = useState(readRoute());
  const [recent, setRecent] = useState([]);
  const [selectedPaste, setSelectedPaste] = useState(null);
  const [form, setForm] = useState(() => withFreshDeleteKey(emptyForm));
  const [fieldErrors, setFieldErrors] = useState({});
  const [searchQuery, setSearchQuery] = useState("");
  const [searchResults, setSearchResults] = useState([]);
  const [searchStatus, setSearchStatus] = useState("idle");
  const [toast, setToast] = useState("");
  const [status, setStatus] = useState({ kind: "idle", message: "" });
  const toastTimer = useRef(null);

  useEffect(() => {
    const onPopState = () => setRoute(readRoute());
    window.addEventListener("popstate", onPopState);
    return () => window.removeEventListener("popstate", onPopState);
  }, []);

  useEffect(() => {
    loadRecent(setRecent);
  }, []);

  useEffect(() => {
    if (route.page !== "paste") {
      setSelectedPaste(null);
      return;
    }

    let ignore = false;
    setStatus({ kind: "loading", message: "Loading paste..." });

    fetch(`/api/pastes/${route.slug}`)
      .then(async (response) => {
        if (!response.ok) {
          throw new Error("Paste not found or expired.");
        }

        return response.json();
      })
      .then((paste) => {
        if (!ignore) {
          setSelectedPaste(paste);
          setStatus({ kind: "idle", message: "" });
        }
      })
      .catch((error) => {
        if (!ignore) {
          setSelectedPaste(null);
          setStatus({ kind: "error", message: error.message });
        }
      });

    return () => {
      ignore = true;
    };
  }, [route]);

  const pasteUrl = useMemo(() => {
    if (!selectedPaste) {
      return "";
    }

    return `${window.location.origin}/p/${selectedPaste.slug}`;
  }, [selectedPaste]);

  const contentStats = useMemo(() => {
    return {
      characters: form.content.length,
      lines: countLines(form.content)
    };
  }, [form.content]);

  function updateForm(patch) {
    setForm((current) => ({ ...current, ...patch }));
    clearFieldErrors(Object.keys(patch));
  }

  function clearFieldErrors(fields) {
    const backendNames = fields.map((field) => field.charAt(0).toUpperCase() + field.slice(1));
    setFieldErrors((current) => {
      const next = { ...current };
      for (const field of backendNames) {
        delete next[field];
      }
      return next;
    });
  }

  function showToast(message) {
    setToast(message);
    if (toastTimer.current) {
      clearTimeout(toastTimer.current);
    }

    toastTimer.current = setTimeout(() => setToast(""), 2200);
  }

  async function createPaste(event) {
    event.preventDefault();
    setFieldErrors({});
    setStatus({ kind: "loading", message: "Creating paste..." });

    try {
      const response = await fetch("/api/pastes", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          ...form,
          visibility: Number(form.visibility),
          expiration: Number(form.expiration)
        })
      });

      if (!response.ok) {
        const problem = await response.json().catch(() => null);
        if (problem?.errors) {
          setFieldErrors(problem.errors);
        }
        throw new Error(toErrorMessage(problem));
      }

      const created = await response.json();
      setForm(withFreshDeleteKey(emptyForm));
      await loadRecent(setRecent);
      navigate(`/p/${created.slug}`, setRoute);
      setStatus({ kind: "success", message: "Paste created." });
    } catch (error) {
      setStatus({ kind: "error", message: error.message });
    }
  }

  async function updatePaste(slug, update) {
    setStatus({ kind: "loading", message: "Saving paste..." });

    const response = await fetch(`/api/pastes/${slug}`, {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(update)
    });

    if (!response.ok) {
      const problem = await response.json().catch(() => null);
      setStatus({ kind: "error", message: toErrorMessage(problem) });
      return {
        ok: false,
        errors: problem?.errors ?? {}
      };
    }

    const updated = await response.json();
    setSelectedPaste(updated);
    await loadRecent(setRecent);
    setStatus({ kind: "success", message: "Paste updated." });
    return { ok: true, errors: {} };
  }

  async function deletePaste(slug) {
    const deleteKey = window.prompt("Enter the delete key for this paste");
    if (!deleteKey) {
      return;
    }

    setStatus({ kind: "loading", message: "Deleting paste..." });

    try {
      const response = await fetch(`/api/pastes/${slug}?deleteKey=${encodeURIComponent(deleteKey)}`, {
        method: "DELETE"
      });

      if (!response.ok) {
        throw new Error("Could not delete paste. Check the delete key.");
      }

      await loadRecent(setRecent);
      navigate("/", setRoute);
      setStatus({ kind: "success", message: "Paste deleted." });
    } catch (error) {
      setStatus({ kind: "error", message: error.message });
    }
  }

  async function searchPastes(event) {
    event.preventDefault();
    const query = searchQuery.trim();

    if (query.length < 2) {
      setSearchStatus("Search needs at least 2 characters.");
      setSearchResults([]);
      return;
    }

    setSearchStatus("Searching...");
    const response = await fetch(`/api/pastes/search?q=${encodeURIComponent(query)}`);
    if (!response.ok) {
      setSearchStatus("Search failed.");
      setSearchResults([]);
      return;
    }

    const results = await response.json();
    setSearchResults(results);
    setSearchStatus(results.length ? "idle" : "No matching public pastes found.");
  }

  async function copyText(text, message) {
    try {
      await navigator.clipboard.writeText(text);
      showToast(message);
    } catch {
      showToast("Could not copy.");
    }
  }

  function forkPaste(paste) {
    setForm(
      withFreshDeleteKey({
        ...emptyForm,
        title: `Fork of ${paste.title}`,
        content: paste.content,
        language: paste.language,
        visibility: paste.visibility,
        expiration: 1
      })
    );
    setFieldErrors({});
    navigate("/", setRoute);
    setStatus({ kind: "success", message: "Fork loaded. Edit it, then create a new paste." });
  }

  function openNewPaste() {
    navigate("/", setRoute);
  }

  return (
    <main className="shell">
      <header className="topbar">
        <button className="brand" onClick={openNewPaste}>
          Pastebin Clone
        </button>
        <button className="new-button" onClick={openNewPaste}>
          New paste
        </button>
      </header>

      {status.message && <div className={`status ${status.kind}`}>{status.message}</div>}

      {route.page === "paste" ? (
        <PasteView
          paste={selectedPaste}
          pasteUrl={pasteUrl}
          onCopy={copyText}
          onDelete={deletePaste}
          onDownload={downloadPaste}
          onFork={forkPaste}
          onUpdate={updatePaste}
        />
      ) : (
        <section className="workspace">
          <form className="editor" onSubmit={createPaste}>
            <div className="field-row">
              <label>
                Title
                <input
                  value={form.title}
                  maxLength={80}
                  placeholder="Untitled paste"
                  onChange={(event) => updateForm({ title: event.target.value })}
                />
              </label>

              <label>
                Language
                <select value={form.language} onChange={(event) => updateForm({ language: event.target.value })}>
                  {languages.map((language) => (
                    <option key={language} value={language}>
                      {language}
                    </option>
                  ))}
                </select>
              </label>
            </div>

            <label>
              <span className="label-row">
                <span>Content</span>
                <span className={`count ${contentStats.characters > 90000 ? "warning" : ""}`}>
                  {formatNumber(contentStats.lines)} lines - {formatNumber(contentStats.characters)} characters
                </span>
              </span>
              <textarea
                value={form.content}
                placeholder="Paste your text or code here..."
                onChange={(event) => updateForm({ content: event.target.value })}
              />
              <FieldErrors errors={fieldErrors.Content} />
            </label>

            <div className="options">
              <label>
                Visibility
                <select value={form.visibility} onChange={(event) => updateForm({ visibility: Number(event.target.value) })}>
                  <option value={0}>Public</option>
                  <option value={1}>Unlisted</option>
                </select>
              </label>

              <label>
                Expiration
                <select value={form.expiration} onChange={(event) => updateForm({ expiration: Number(event.target.value) })}>
                  {expirations.map((expiration) => (
                    <option key={expiration.value} value={expiration.value}>
                      {expiration.label}
                    </option>
                  ))}
                </select>
              </label>

              <label>
                Delete key
                <input value={form.deleteKey} minLength={6} onChange={(event) => updateForm({ deleteKey: event.target.value })} />
                <FieldErrors errors={fieldErrors.DeleteKey} />
              </label>
            </div>

            <button className="primary" disabled={status.kind === "loading" || form.content.length > 100000}>
              Create paste
            </button>
          </form>

          <aside className="side-panel">
            <SearchPanel
              query={searchQuery}
              results={searchResults}
              status={searchStatus}
              onQueryChange={setSearchQuery}
              onSubmit={searchPastes}
            />
            <RecentPastes pastes={recent} />
          </aside>
        </section>
      )}

      {toast && <div className="toast">{toast}</div>}
    </main>
  );
}

function PasteView({ paste, pasteUrl, onCopy, onDelete, onDownload, onFork, onUpdate }) {
  const [editing, setEditing] = useState(false);
  const [editForm, setEditForm] = useState(null);
  const [editErrors, setEditErrors] = useState({});
  const [now, setNow] = useState(Date.now());

  useEffect(() => {
    if (!paste) {
      setEditForm(null);
      setEditing(false);
      return;
    }

    setEditForm({
      title: paste.title,
      content: paste.content,
      language: paste.language,
      deleteKey: ""
    });
    setEditErrors({});
    setEditing(false);
  }, [paste?.slug]);

  useEffect(() => {
    const timer = setInterval(() => setNow(Date.now()), 30000);
    return () => clearInterval(timer);
  }, []);

  if (!paste) {
    return <section className="empty-state">No paste loaded.</section>;
  }

  async function saveEdit(event) {
    event.preventDefault();
    const result = await onUpdate(paste.slug, editForm);
    setEditErrors(result.errors);
    if (result.ok) {
      setEditing(false);
    }
  }

  return (
    <section className="paste-view">
      <div className="paste-header">
        <div>
          <h1>{paste.title}</h1>
          <p>
            {paste.language} - {formatDate(paste.createdAt)} - {paste.viewCount} views
          </p>
          {paste.updatedAt && <p>Updated {formatDate(paste.updatedAt)}</p>}
        </div>
        <div className="actions">
          <button onClick={() => onCopy(pasteUrl, "Link copied.")}>Copy link</button>
          <button onClick={() => onCopy(paste.content, "Paste text copied.")}>Copy text</button>
          <button onClick={() => window.open(`/api/pastes/${paste.slug}/raw`, "_blank", "noopener,noreferrer")}>Raw</button>
          <button onClick={() => onDownload(paste)}>Download</button>
          <button onClick={() => onFork(paste)}>Fork</button>
          <button onClick={() => setEditing((current) => !current)}>{editing ? "Cancel edit" : "Edit"}</button>
          <button className="danger" onClick={() => onDelete(paste.slug)}>
            Delete
          </button>
        </div>
      </div>

      <p className={`expires ${paste.expiresAt && getMillisecondsRemaining(paste.expiresAt, now) < 60 * 60 * 1000 ? "warning" : ""}`}>
        {paste.expiresAt ? `Expires ${formatDate(paste.expiresAt)} - ${formatTimeRemaining(paste.expiresAt, now)}` : "Never expires"}
      </p>

      {editing && editForm && (
        <form className="edit-form" onSubmit={saveEdit}>
          <div className="field-row">
            <label>
              Title
              <input value={editForm.title} onChange={(event) => setEditForm({ ...editForm, title: event.target.value })} />
            </label>
            <label>
              Language
              <select value={editForm.language} onChange={(event) => setEditForm({ ...editForm, language: event.target.value })}>
                {languages.map((language) => (
                  <option key={language} value={language}>
                    {language}
                  </option>
                ))}
              </select>
            </label>
          </div>
          <label>
            Content
            <textarea value={editForm.content} onChange={(event) => setEditForm({ ...editForm, content: event.target.value })} />
            <FieldErrors errors={editErrors.Content} />
          </label>
          <label>
            Delete key
            <input
              value={editForm.deleteKey}
              minLength={6}
              onChange={(event) => setEditForm({ ...editForm, deleteKey: event.target.value })}
            />
            <FieldErrors errors={editErrors.DeleteKey} />
          </label>
          <button className="primary">Save changes</button>
        </form>
      )}

      <pre className="paste-code">
        <code>{paste.content}</code>
      </pre>
    </section>
  );
}

function SearchPanel({ query, results, status, onQueryChange, onSubmit }) {
  return (
    <section className="recent">
      <h2>Search public pastes</h2>
      <form className="search-form" onSubmit={onSubmit}>
        <input value={query} placeholder="Search title, content, language" onChange={(event) => onQueryChange(event.target.value)} />
        <button className="primary">Search</button>
      </form>
      {status !== "idle" && <p className="muted">{status}</p>}
      {results.length > 0 && <PasteList pastes={results} />}
    </section>
  );
}

function RecentPastes({ pastes }) {
  return (
    <section className="recent">
      <h2>Recent public pastes</h2>
      {pastes.length === 0 ? <p className="muted">Public pastes will show up here.</p> : <PasteList pastes={pastes} />}
    </section>
  );
}

function PasteList({ pastes }) {
  return (
    <ul>
      {pastes.map((paste) => (
        <li key={paste.slug}>
          <a href={`/p/${paste.slug}`} onClick={(event) => intercept(event, `/p/${paste.slug}`)}>
            <strong>{paste.title}</strong>
            <span>
              {paste.language} - {formatDate(paste.createdAt)}
            </span>
            {paste.preview && <span className="preview">{paste.preview}</span>}
          </a>
        </li>
      ))}
    </ul>
  );
}

function FieldErrors({ errors }) {
  if (!errors?.length) {
    return null;
  }

  return (
    <span className="field-error">
      {errors.map((error) => (
        <span key={error}>{error}</span>
      ))}
    </span>
  );
}

async function loadRecent(setRecent) {
  const response = await fetch("/api/pastes/recent");
  if (response.ok) {
    setRecent(await response.json());
  }
}

function downloadPaste(paste) {
  const blob = new Blob([paste.content], { type: "text/plain" });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = toSafeFilename(paste.title, paste.language);
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
}

function navigate(path, setRoute) {
  window.history.pushState({}, "", path);
  setRoute(readRoute());
}

function intercept(event, path) {
  event.preventDefault();
  window.history.pushState({}, "", path);
  window.dispatchEvent(new PopStateEvent("popstate"));
}

function readRoute() {
  const match = window.location.pathname.match(/^\/p\/([^/]+)$/);
  return match ? { page: "paste", slug: match[1] } : { page: "home" };
}

function withFreshDeleteKey(values) {
  return {
    ...values,
    deleteKey: crypto.randomUUID().slice(0, 12)
  };
}

function countLines(text) {
  return text.length === 0 ? 1 : text.split("\n").length;
}

function formatNumber(value) {
  return new Intl.NumberFormat().format(value);
}

function formatDate(value) {
  return new Intl.DateTimeFormat(undefined, {
    dateStyle: "medium",
    timeStyle: "short"
  }).format(new Date(value));
}

function getMillisecondsRemaining(expiresAt, now) {
  return new Date(expiresAt).getTime() - now;
}

function formatTimeRemaining(expiresAt, now) {
  const milliseconds = getMillisecondsRemaining(expiresAt, now);
  if (milliseconds <= 0) {
    return "expired";
  }

  const minutes = Math.ceil(milliseconds / 60000);
  if (minutes < 60) {
    return `${minutes} minute${minutes === 1 ? "" : "s"} left`;
  }

  const hours = Math.ceil(minutes / 60);
  if (hours < 48) {
    return `${hours} hour${hours === 1 ? "" : "s"} left`;
  }

  const days = Math.ceil(hours / 24);
  return `${days} day${days === 1 ? "" : "s"} left`;
}

function toSafeFilename(title, language) {
  const extensions = {
    csharp: "cs",
    javascript: "js",
    typescript: "ts",
    html: "html",
    css: "css",
    sql: "sql",
    json: "json",
    markdown: "md"
  };
  const base = title
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "")
    .slice(0, 60);
  return `${base || "paste"}.${extensions[language] ?? "txt"}`;
}

function toErrorMessage(problem) {
  if (!problem?.errors) {
    return problem?.message ?? "Something went wrong.";
  }

  return Object.values(problem.errors).flat().join(" ");
}

createRoot(document.getElementById("root")).render(<App />);
