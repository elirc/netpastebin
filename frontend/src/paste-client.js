export const languages = [
  "plaintext",
  "csharp",
  "javascript",
  "typescript",
  "html",
  "css",
  "sql",
  "json",
  "markdown",
];
export const emptyForm = () => ({
  title: "",
  content: "",
  language: "plaintext",
  visibility: "public",
  expiration: "never",
});

export class ApiError extends Error {
  constructor(message, status = 0) {
    super(message);
    this.name = "ApiError";
    this.status = status;
  }
}

export function base64url(bytes) {
  return btoa(String.fromCharCode(...bytes))
    .replaceAll("+", "-")
    .replaceAll("/", "_")
    .replace(/=+$/, "");
}

export function newAttempt(form, cryptoApi = globalThis.crypto) {
  if (!cryptoApi?.getRandomValues || !cryptoApi?.randomUUID)
    throw new Error(
      "A secure browser context is required to generate an edit key. Use the loopback development address.",
    );
  validateForm(form);
  return Object.freeze({
    requestId: cryptoApi.randomUUID(),
    key: base64url(cryptoApi.getRandomValues(new Uint8Array(32))),
    body: Object.freeze({ ...form }),
  });
}

export function validateForm(form, editing = false) {
  if (
    form.title.trim().length > 80 ||
    /[\u0000-\u001f\u007f-\u009f]/u.test(form.title)
  )
    throw new Error("Title must be at most 80 characters without controls.");
  if (!form.content.trim() || form.content.length > 100000)
    throw new Error(
      "Content must contain text and be at most 100,000 characters.",
    );
  if (
    /[\u0000-\u0008\u000b\u000c\u000e-\u001f\u007f-\u009f]/u.test(form.content)
  )
    throw new Error(
      "Content may contain tabs and line breaks, but not other control characters.",
    );
  if (!languages.includes(form.language))
    throw new Error("Choose a supported language.");
  if (!["public", "unlisted"].includes(form.visibility))
    throw new Error("Choose public or unlisted visibility.");
  if (
    ![...(editing ? ["keep"] : []), "hour", "day", "week", "never"].includes(
      form.expiration,
    )
  )
    throw new Error("Choose a supported expiration.");
}

export function keyHeader(key) {
  if (typeof key !== "string" || key.length < 6 || key.length > 256)
    throw new Error(
      "Enter the exact saved edit key (6 to 256 characters for older pastes).",
    );
  // Old user-selected keys can contain spaces or Unicode. Header framing preserves
  // their exact UTF-8 bytes; it is an encoding, not encryption or stronger entropy.
  return /^[A-Za-z0-9_-]{43}$/u.test(key)
    ? key
    : `utf8.${base64url(new TextEncoder().encode(key))}`;
}

export function versionHeader(version) {
  if (!Number.isSafeInteger(version) || version < 1)
    throw new Error(
      "A supported current version is required. Reload this paste.",
    );
  return `"${version}"`;
}

export async function api(
  path,
  { method = "GET", body, key, version, requestId, signal } = {},
  fetcher = globalThis.fetch,
) {
  const headers = { Accept: "application/json" };
  if (body !== undefined) headers["Content-Type"] = "application/json";
  if (key !== undefined) headers["X-Paste-Key"] = keyHeader(key);
  if (version !== undefined) headers["If-Match"] = versionHeader(version);
  if (requestId !== undefined) headers["X-Request-Id"] = requestId;
  let response;
  try {
    response = await fetcher(`/api${path}`, {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
      signal,
      cache: "no-store",
    });
  } catch (error) {
    if (error.name === "AbortError") throw error;
    throw new ApiError(
      "The response was not received. Keep this draft. Retry a create with the same request and key; review current data before retrying an edit.",
    );
  }
  if (response.status === 204) return null;
  let data;
  try {
    data = await response.json();
  } catch {
    throw new ApiError(
      "The server response could not be read. Keep the draft and review current data before retrying.",
      response.status,
    );
  }
  if (!response.ok)
    throw new ApiError(
      data.error || "The request was rejected. Your draft is still available.",
      response.status,
    );
  return data;
}

export const createPaste = (attempt, fetcher) =>
  api(
    "/pastes",
    {
      method: "POST",
      body: attempt.body,
      key: attempt.key,
      requestId: attempt.requestId,
    },
    fetcher,
  );
export const formFromPaste = (paste) => ({
  title: paste.title,
  content: paste.content,
  language: paste.language,
  visibility: paste.visibility,
  expiration: "keep",
});
export const pastePath = (slug) => `/p/${encodeURIComponent(slug)}`;

export function slugFromPath(path) {
  const match = /^\/p\/([A-Za-z0-9_-]{8,64})\/?$/u.exec(path);
  return match?.[1] || null;
}

export function shouldHandleLink(event) {
  return (
    event.button === 0 &&
    !event.ctrlKey &&
    !event.metaKey &&
    !event.shiftKey &&
    !event.altKey &&
    !event.defaultPrevented
  );
}

export function download(name, text, type = "application/json;charset=utf-8") {
  const url = URL.createObjectURL(new Blob([text], { type }));
  const link = document.createElement("a");
  link.href = url;
  link.download = name;
  document.body.append(link);
  link.click();
  link.remove();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
