import test from "node:test";
import assert from "node:assert/strict";
import { webcrypto } from "node:crypto";
import {
  ApiError,
  api,
  base64url,
  createPaste,
  emptyForm,
  formFromPaste,
  keyHeader,
  newAttempt,
  shouldHandleLink,
  slugFromPath,
  validateForm,
  versionHeader,
} from "../src/paste-client.js";

const form = () => ({
  ...emptyForm(),
  title: "Example",
  content: "const n = 1;  \n\n",
});
test("create attempts generate independent 256-bit capability keys and immutable exact text", () => {
  const input = form(),
    a = newAttempt(input, webcrypto),
    b = newAttempt(input, webcrypto);
  assert.match(a.key, /^[A-Za-z0-9_-]{43}$/);
  assert.notEqual(a.key, b.key);
  assert.notEqual(a.requestId, b.requestId);
  assert.equal(Buffer.from(a.key, "base64url").length, 32);
  input.content = "changed";
  assert.equal(a.body.content, "const n = 1;  \n\n");
  assert.ok(Object.isFrozen(a.body));
  assert.ok(Object.isFrozen(a));
});
test("missing secure randomness blocks creation before a request exists", () =>
  assert.throws(() => newAttempt(form(), {}), /secure browser/));
test("validation retains source content and enforces actual limits", () => {
  const valid = { ...form(), content: "\tvalue\r\n  " };
  validateForm(valid);
  assert.equal(valid.content, "\tvalue\r\n  ");
  for (const content of ["", "  \n", "x".repeat(100001), "a\u0000b", "x\u0085"])
    assert.throws(() => validateForm({ ...form(), content }));
  validateForm({ ...form(), content: "x".repeat(100000) });
});
test("form enums, title controls and edit-only keep expiration are checked", () => {
  for (const change of [
    { title: "x".repeat(81) },
    { title: "x\n" },
    { language: "__proto__" },
    { visibility: "private" },
    { expiration: "keep" },
  ])
    assert.throws(() => validateForm({ ...form(), ...change }));
  validateForm({ ...form(), expiration: "keep" }, true);
});
test("legacy key header round-trips spaces and UTF-8 without putting a key in a URL", () => {
  const key = "  older café 密钥  ";
  assert.equal(
    Buffer.from(keyHeader(key).slice(5), "base64url").toString("utf8"),
    key,
  );
  assert.equal(base64url(new Uint8Array([255, 255, 254])), "___-");
  const generated = newAttempt(form(), webcrypto).key;
  assert.equal(keyHeader(generated), generated);
  for (const key of ["", "short", "x".repeat(257), null])
    assert.throws(() => keyHeader(key));
});
test("version preconditions reject values JavaScript cannot safely represent", () => {
  assert.equal(versionHeader(42), '"42"');
  for (const n of [0, -1, 1.2, Infinity, Number.MAX_SAFE_INTEGER + 1, "2"])
    assert.throws(() => versionHeader(n));
});
test("retry sends the identical creation request and returns the server receipt", async () => {
  const selected = newAttempt(form(), webcrypto),
    calls = [];
  const fetcher = async (url, options) => {
    calls.push({ url, options });
    return Response.json(
      { slug: "abc12345", replayed: calls.length > 1 },
      { status: calls.length > 1 ? 200 : 201 },
    );
  };
  await createPaste(selected, fetcher);
  const retried = await createPaste(selected, fetcher);
  assert.deepEqual(calls[0], calls[1]);
  assert.equal(calls[0].url, "/api/pastes");
  assert.equal(calls[0].options.headers["X-Request-Id"], selected.requestId);
  assert.equal(calls[0].options.headers["X-Paste-Key"], selected.key);
  assert.ok(!calls[0].url.includes(selected.key));
  assert.equal(retried.replayed, true);
});
test("failed acknowledgement preserves the caller-owned attempt for retry", async () => {
  const selected = newAttempt(form(), webcrypto),
    before = JSON.stringify(selected);
  await assert.rejects(
    createPaste(selected, async () => {
      throw new TypeError("offline");
    }),
    (error) =>
      error instanceof ApiError &&
      error.status === 0 &&
      /same request/.test(error.message),
  );
  assert.equal(JSON.stringify(selected), before);
});
test("stale edit errors preserve status and server explanation", async () => {
  const calls = [];
  await assert.rejects(
    api(
      "/pastes/abc12345",
      {
        method: "PUT",
        body: { ...form(), expiration: "keep" },
        key: "olderkey",
        version: 7,
      },
      async (url, options) => {
        calls.push({ url, options });
        return Response.json({ error: "Review version 8." }, { status: 412 });
      },
    ),
    (error) => error.status === 412 && error.message === "Review version 8.",
  );
  assert.equal(calls[0].options.headers["If-Match"], '"7"');
  assert.equal(calls[0].options.cache, "no-store");
});
test("delete uses a header and handles the empty successful response", async () => {
  const result = await api(
    "/pastes/abc12345",
    { method: "DELETE", key: "olderkey", version: 3 },
    async (url, options) => {
      assert.equal(url, "/api/pastes/abc12345");
      assert.equal(options.body, undefined);
      assert.ok(options.headers["X-Paste-Key"]);
      return new Response(null, { status: 204 });
    },
  );
  assert.equal(result, null);
});
test("unreadable server replies are not reported as accepted edits", async () => {
  await assert.rejects(
    api(
      "/pastes",
      {},
      async () => new Response("<html>failure</html>", { status: 500 }),
    ),
    (error) => error.status === 500 && /could not be read/.test(error.message),
  );
});
test("aborted searches keep their AbortError so stale effects can ignore them", async () => {
  const aborted = new DOMException("aborted", "AbortError");
  await assert.rejects(
    api("/pastes", {}, async () => {
      throw aborted;
    }),
    (error) => error === aborted,
  );
});
test("editing preserves existing expiry unless a new expiry is explicitly chosen", () => {
  const value = formFromPaste({
    ...form(),
    expiresAt: "2027-01-01T00:00:00Z",
    version: 3,
  });
  assert.equal(value.expiration, "keep");
  assert.equal(value.content, form().content);
  assert.ok(!("version" in value));
});
test("routes accept only bounded slug shapes", () => {
  assert.equal(slugFromPath("/p/Abcd_123-/"), "Abcd_123-");
  for (const path of [
    "/p/short",
    "/p/../../secret",
    "/api/pastes",
    "/p/abc12345/raw",
    `/p/${"a".repeat(65)}`,
  ])
    assert.equal(slugFromPath(path), null);
});
test("modified links retain normal browser tab and window behavior", () => {
  const event = {
    button: 0,
    ctrlKey: false,
    metaKey: false,
    shiftKey: false,
    altKey: false,
    defaultPrevented: false,
  };
  assert.ok(shouldHandleLink(event));
  for (const field of [
    "ctrlKey",
    "metaKey",
    "shiftKey",
    "altKey",
    "defaultPrevented",
  ])
    assert.equal(shouldHandleLink({ ...event, [field]: true }), false);
  assert.equal(shouldHandleLink({ ...event, button: 1 }), false);
});
