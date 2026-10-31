"use strict";
const el = id => document.getElementById(id);
const text = el("text"), status = el("status");
const message = key => { status.textContent = el("m-" + key).textContent; };
function connected(value) { el("pair").hidden = value; el("workspace").hidden = !value; }
async function request(path, body = {}) {
  const response = await fetch("/api/" + path, { method: "POST", credentials: "same-origin", cache: "no-store",
    headers: { "Content-Type": "application/json" }, body: JSON.stringify(body), signal: AbortSignal.timeout(12000) });
  if (!response.ok) { const error = new Error("Transfer failed"); error.status = response.status; throw error; }
  return response;
}
function failed(error) {
  if (error.status === 401 || error.status === 429) { connected(false); message("code"); }
  else message(error.status === 413 ? "large" : error.status === 503 ? "busy" : "error");
}
async function action(button, task) { button.disabled = true; try { await task(); } catch (error) { failed(error); } finally { button.disabled = false; } }
el("pair-form").addEventListener("submit", event => { event.preventDefault(); action(event.submitter, async () => {
  await request("pair", { code: el("code").value }); el("code").value = ""; connected(true); message("connected");
}); });
el("read").addEventListener("click", event => action(event.currentTarget, async () => {
  const response = await request("read"); const result = await response.json(); text.value = result.text; message("read");
}));
el("write").addEventListener("click", event => action(event.currentTarget, async () => {
  if (new TextEncoder().encode(text.value).length > 65536) { message("large"); return; }
  await request("write", { text: text.value }); message("written");
}));
el("copy").addEventListener("click", () => {
  // Keep the write directly inside the user gesture for Safari; no fetch/await precedes it.
  if (!navigator.clipboard) { text.focus(); text.select(); message("paste"); return; }
  navigator.clipboard.writeText(text.value).then(() => message("copied"), () => { text.focus(); text.select(); message("paste"); });
});
el("clear").addEventListener("click", () => { text.value = ""; status.textContent = ""; });
window.addEventListener("pagehide", () => { text.value = ""; el("code").value = ""; });
request("status").then(() => { connected(true); message("connected"); }, () => connected(false));
