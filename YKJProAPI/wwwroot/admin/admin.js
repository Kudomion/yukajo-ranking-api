"use strict";

// The admin session token lives in sessionStorage (cleared when the tab closes) and is
// sent in the Authorization header. The admin secret itself is never stored.
const TOKEN_KEY = "ykj_admin_session";
const CATEGORIES = ["TCG", "Anime", "Community"];

const ACTION_LABELS = {
  "player.create": "إضافة لاعب",
  "player.update": "تعديل لاعب",
  "player.deactivate": "تعطيل لاعب",
  "player.delete": "حذف لاعب نهائياً",
  "points.set": "تعيين نقاط",
  "points.adjust": "إضافة/خصم نقاط",
  "match.undo": "إلغاء مباراة",
  "daily.reset": "إعادة ضبط الحد اليومي",
};

const $ = (id) => document.getElementById(id);

let players = [];
let auditOldestId = null;
let editingId = null;

// ------------------------------------------------------------------ storage

function getToken() {
  try { return sessionStorage.getItem(TOKEN_KEY); } catch { return null; }
}
function setToken(t) {
  try {
    if (t) sessionStorage.setItem(TOKEN_KEY, t);
    else sessionStorage.removeItem(TOKEN_KEY);
  } catch { /* storage blocked: session lasts until reload */ }
  memoryToken = t;
}
let memoryToken = getToken();

// ------------------------------------------------------------------ api

class ApiError extends Error {}

async function api(method, path, body) {
  const headers = { "Accept": "application/json" };
  if (memoryToken) headers["Authorization"] = "Bearer " + memoryToken;
  if (body !== undefined) headers["Content-Type"] = "application/json";

  let res;
  try {
    res = await fetch("/api/admin/" + path, {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
      credentials: "omit",
      cache: "no-store",
    });
  } catch {
    throw new ApiError("تعذر الاتصال بالخادم");
  }

  let data = null;
  const text = await res.text();
  if (text) { try { data = JSON.parse(text); } catch { data = text; } }

  if (res.status === 401 && path !== "login") {
    setToken(null);
    showLogin("انتهت الجلسة، الرجاء تسجيل الدخول مجدداً");
    throw new ApiError("انتهت الجلسة");
  }
  if (!res.ok) {
    const msg = (data && data.error) || (data && data.title) || (typeof data === "string" ? data : "") || ("خطأ " + res.status);
    throw new ApiError(msg);
  }
  return data;
}

// ------------------------------------------------------------------ ui helpers

function el(tag, attrs = {}, ...children) {
  const node = document.createElement(tag);
  for (const [k, v] of Object.entries(attrs)) {
    if (v === undefined || v === null || v === false) continue;
    if (k === "class") node.className = v;
    else if (k.startsWith("on")) node.addEventListener(k.slice(2), v);
    else node.setAttribute(k, v === true ? "" : v);
  }
  for (const c of children) {
    if (c === null || c === undefined) continue;
    node.append(c instanceof Node ? c : document.createTextNode(String(c)));
  }
  return node;
}

function fmtTime(iso) {
  if (!iso) return "";
  const d = new Date(iso);
  return isNaN(d) ? iso : d.toLocaleString("en-GB", { dateStyle: "short", timeStyle: "short" });
}

function fmtSigned(n) { return n > 0 ? "+" + n : String(n); }

let flashTimer = null;
function flash(msg, isError = false) {
  const f = $("flash");
  f.textContent = msg;
  f.classList.toggle("err", isError);
  f.hidden = false;
  clearTimeout(flashTimer);
  flashTimer = setTimeout(() => { f.hidden = true; }, isError ? 8000 : 4000);
}

function emptyRow(tbody, cols, text) {
  tbody.replaceChildren(el("tr", {}, el("td", { colspan: cols, class: "empty" }, text)));
}

async function guarded(fn) {
  try { await fn(); }
  catch (e) { if (e.message !== "انتهت الجلسة") flash(e.message, true); }
}

// ------------------------------------------------------------------ login

function showLogin(message) {
  $("app-view").hidden = true;
  $("login-view").hidden = false;
  $("login-error").textContent = message || "";
  $("login-token").value = "";
  $("login-token").focus();
}

async function showApp() {
  $("login-view").hidden = true;
  $("app-view").hidden = false;
  await guarded(loadPlayers);
}

$("login-form").addEventListener("submit", async (e) => {
  e.preventDefault();
  const btn = e.submitter;
  if (btn) btn.disabled = true;
  $("login-error").textContent = "";
  try {
    const res = await api("POST", "login", { token: $("login-token").value });
    setToken(res.token);
    $("login-token").value = "";
    await showApp();
  } catch (err) {
    $("login-error").textContent = err.message;
  } finally {
    if (btn) btn.disabled = false;
  }
});

$("logout").addEventListener("click", () => {
  setToken(null);
  showLogin("");
});

// ------------------------------------------------------------------ tabs

document.querySelectorAll(".tab").forEach((tab) => {
  tab.addEventListener("click", () => {
    document.querySelectorAll(".tab").forEach((t) => t.classList.toggle("active", t === tab));
    document.querySelectorAll(".tab-panel").forEach((p) => { p.hidden = p.id !== "tab-" + tab.dataset.tab; });
    const loaders = { players: loadPlayers, points: loadPointsTab, matches: loadMatches, audit: () => loadAudit(true) };
    guarded(loaders[tab.dataset.tab]);
  });
});

// ------------------------------------------------------------------ players

function pointsFor(p, cat) {
  const r = p.rankings.find((x) => x.category === cat);
  return r ? r.points : null;
}

function renderPlayers() {
  const tbody = $("players-body");
  const showInactive = $("show-inactive").checked;
  const list = players.filter((p) => showInactive || p.isActive);
  if (!list.length) return emptyRow(tbody, 9, "لا يوجد لاعبون");

  tbody.replaceChildren(...list.map((p) => {
    const cells = CATEGORIES.map((c) => {
      const r = p.rankings.find((x) => x.category === c);
      return el("td", { class: "num", title: r ? `فوز ${r.wins} / خسارة ${r.losses}` : "" }, r ? r.points : "—");
    });
    return el("tr", { class: p.isActive ? "" : "off" },
      el("td", { class: "num" }, p.id),
      el("td", { dir: "auto" }, p.username),
      el("td", { dir: "ltr" }, p.discordId || ""),
      el("td", {}, p.categories.join("، ")),
      el("td", {}, el("span", { class: "badge " + (p.isActive ? "on" : "off") }, p.isActive ? "نشط" : "معطّل")),
      ...cells,
      el("td", { class: "actions-cell" },
        el("button", { type: "button", class: "small secondary", onclick: () => openPlayerDialog(p) }, "تعديل"),
        el("button", { type: "button", class: "small secondary", onclick: () => resetDaily(p) }, "إعادة ضبط الحد اليومي"),
        p.isActive
          ? el("button", { type: "button", class: "small secondary", onclick: () => deactivatePlayer(p) }, "تعطيل")
          : el("button", { type: "button", class: "small secondary", onclick: () => reactivatePlayer(p) }, "تفعيل"),
        el("button", { type: "button", class: "small danger", onclick: () => deletePlayer(p) }, "حذف نهائي"),
      ),
    );
  }));
}

async function loadPlayers() {
  players = await api("GET", "players");
  renderPlayers();
}

$("show-inactive").addEventListener("change", renderPlayers);
$("add-player").addEventListener("click", () => openPlayerDialog(null));

function openPlayerDialog(p) {
  editingId = p ? p.id : null;
  $("player-dialog-title").textContent = p ? `تعديل اللاعب #${p.id}` : "إضافة لاعب";
  $("pl-username").value = p ? p.username : "";
  $("pl-discord").value = p ? p.discordId : "";
  $("pl-active").checked = p ? p.isActive : true;
  $("pl-reason").value = "";
  const cats = p ? p.categories : CATEGORIES;
  document.querySelectorAll('input[name="pl-cat"]').forEach((cb) => { cb.checked = cats.includes(cb.value); });
  $("player-error").textContent = "";
  $("player-dialog").showModal();
  $("pl-username").focus();
}

$("player-cancel").addEventListener("click", () => $("player-dialog").close());

$("player-form").addEventListener("submit", async (e) => {
  e.preventDefault();
  const body = {
    username: $("pl-username").value.trim(),
    discordId: $("pl-discord").value.trim(),
    categories: [...document.querySelectorAll('input[name="pl-cat"]:checked')].map((cb) => cb.value),
    isActive: $("pl-active").checked,
    reason: $("pl-reason").value.trim() || null,
  };
  if (!body.username) { $("player-error").textContent = "الاسم مطلوب"; return; }
  if (!body.categories.length) { $("player-error").textContent = "اختر فئة واحدة على الأقل"; return; }

  try {
    if (editingId === null) await api("POST", "players", body);
    else await api("PUT", "players/" + editingId, body);
    $("player-dialog").close();
    flash(editingId === null ? "تمت إضافة اللاعب" : "تم حفظ التعديلات");
    await loadPlayers();
  } catch (err) {
    $("player-error").textContent = err.message;
  }
});

function askReason(question, required) {
  const r = window.prompt(question + (required ? " (السبب مطلوب)" : " (السبب اختياري)"), "");
  if (r === null) return null;                    // cancelled
  if (required && !r.trim()) { flash("السبب مطلوب", true); return null; }
  return r.trim();
}

function resetDaily(p) {
  const reason = askReason(`إعادة ضبط الحد اليومي للاعب "${p.username}"؟`, false);
  if (reason === null) return;
  guarded(async () => {
    await api("POST", `players/${p.id}/reset-daily-limit`, { reason: reason || null });
    flash(`تمت إعادة ضبط الحد اليومي للاعب "${p.username}"`);
  });
}

function deactivatePlayer(p) {
  const reason = askReason(`تعطيل اللاعب "${p.username}"؟ سيختفي من الترتيب ولن تُسجَّل مبارياته.`, false);
  if (reason === null) return;
  guarded(async () => {
    await api("DELETE", `players/${p.id}?reason=${encodeURIComponent(reason)}`);
    flash("تم تعطيل اللاعب");
    await loadPlayers();
  });
}

function reactivatePlayer(p) {
  guarded(async () => {
    await api("PUT", `players/${p.id}`, { isActive: true });
    flash("تم تفعيل اللاعب");
    await loadPlayers();
  });
}

function deletePlayer(p) {
  if (!window.confirm(`حذف اللاعب "${p.username}" نهائياً مع نقاطه؟ لا يمكن التراجع عن هذا.`)) return;
  const reason = askReason("سبب الحذف النهائي", true);
  if (reason === null) return;
  guarded(async () => {
    await api("DELETE", `players/${p.id}?permanent=true&reason=${encodeURIComponent(reason)}`);
    flash("تم حذف اللاعب نهائياً");
    await loadPlayers();
  });
}

// ------------------------------------------------------------------ points

async function loadPointsTab() {
  players = await api("GET", "players");
  const sel = $("pts-player");
  const prev = sel.value;
  sel.replaceChildren(...players.map((p) =>
    el("option", { value: p.id }, `${p.username} (#${p.id})${p.isActive ? "" : " — معطّل"}`)));
  if (prev && players.some((p) => String(p.id) === prev)) sel.value = prev;
  updateCurrentPoints();
}

function updateCurrentPoints() {
  const p = players.find((x) => String(x.id) === $("pts-player").value);
  const pts = p ? pointsFor(p, $("pts-category").value) : null;
  $("pts-current").textContent = pts === null ? "0 (لا يوجد سجل بعد)" : pts;
}

$("pts-player").addEventListener("change", updateCurrentPoints);
$("pts-category").addEventListener("change", updateCurrentPoints);

$("points-form").addEventListener("submit", async (e) => {
  e.preventDefault();
  const mode = document.querySelector('input[name="pts-mode"]:checked').value;
  const value = Number($("pts-value").value);
  if (!Number.isInteger(value)) { flash("القيمة يجب أن تكون عدداً صحيحاً", true); return; }
  const reason = $("pts-reason").value.trim();
  if (!reason) { flash("السبب مطلوب", true); return; }

  const p = players.find((x) => String(x.id) === $("pts-player").value);
  const cat = $("pts-category").value;
  const desc = mode === "set" ? `تعيين نقاط ${cat} للاعب "${p.username}" إلى ${value}` : `${fmtSigned(value)} نقطة ${cat} للاعب "${p.username}"`;
  if (!window.confirm(desc + "؟")) return;

  await guarded(async () => {
    const res = await api("POST", "points", { playerId: p.id, category: cat, mode, value, reason });
    flash(res.message);
    $("pts-value").value = "";
    $("pts-reason").value = "";
    await loadPointsTab();
  });
});

// ------------------------------------------------------------------ matches

async function loadMatches() {
  const [matches, pending] = await Promise.all([api("GET", "matches?limit=100"), api("GET", "pending")]);

  const tbody = $("matches-body");
  if (!matches.length) emptyRow(tbody, 8, "لا توجد مباريات");
  else tbody.replaceChildren(...matches.map((m) => el("tr", { class: m.isUndone ? "off" : "" },
    el("td", { class: "num" }, m.id),
    el("td", {}, fmtTime(m.playedAt)),
    el("td", {}, m.category),
    el("td", { dir: "auto" }, m.winner),
    el("td", { dir: "auto" }, m.loser),
    el("td", { class: "num", dir: "ltr", title: m.exactPoints ? "" : "مباراة قديمة: النقاط تقديرية" },
      `${fmtSigned(m.winnerPointsDelta)} / ${fmtSigned(m.loserPointsDelta)}${m.exactPoints ? "" : " *"}`),
    el("td", {}, m.isUndone
      ? el("span", { class: "badge undone", title: (m.undoReason || "") + " — " + fmtTime(m.undoneAt) }, "ملغاة")
      : el("span", { class: "badge on" }, "مسجّلة")),
    el("td", {}, m.isUndone ? null :
      el("button", { type: "button", class: "small danger", onclick: () => undoMatch(m) }, "تراجع")),
  )));

  const pbody = $("pending-body");
  if (!pending.length) emptyRow(pbody, 4, "لا يوجد");
  else pbody.replaceChildren(...pending.map((p) => el("tr", {},
    el("td", {}, fmtTime(p.reportedAt)),
    el("td", {}, p.category),
    el("td", { dir: "auto" }, p.winner),
    el("td", { dir: "auto" }, p.loser),
  )));
}

$("reload-matches").addEventListener("click", () => guarded(loadMatches));

function undoMatch(m) {
  const reason = askReason(`إلغاء المباراة #${m.id} (${m.winner} ضد ${m.loser}) وإرجاع النقاط؟`, true);
  if (reason === null) return;
  guarded(async () => {
    await api("POST", `matches/${m.id}/undo`, { reason });
    flash(`تم إلغاء المباراة #${m.id} وإرجاع النقاط`);
    await loadMatches();
  });
}

// ------------------------------------------------------------------ audit

async function loadAudit(reset) {
  if (reset) auditOldestId = null;
  const q = "audit?limit=100" + (auditOldestId ? "&beforeId=" + auditOldestId : "");
  const rows = await api("GET", q);
  const tbody = $("audit-body");
  if (reset) tbody.replaceChildren();
  if (reset && !rows.length) emptyRow(tbody, 7, "لا توجد تغييرات بعد");

  for (const a of rows) {
    tbody.append(el("tr", {},
      el("td", {}, fmtTime(a.at)),
      el("td", { title: a.actorIp || "" }, a.actor),
      el("td", { title: a.action }, ACTION_LABELS[a.action] || a.action),
      el("td", { dir: "auto" }, `${a.targetName || ""}${a.targetId ? " (#" + a.targetId + ")" : ""}`),
      el("td", { class: "json" }, a.oldValue || "—"),
      el("td", { class: "json" }, a.newValue || "—"),
      el("td", { dir: "auto" }, a.reason || ""),
    ));
  }
  if (rows.length) auditOldestId = rows[rows.length - 1].id;
  $("audit-more").hidden = rows.length < 100;
}

$("reload-audit").addEventListener("click", () => guarded(() => loadAudit(true)));
$("audit-more").addEventListener("click", () => guarded(() => loadAudit(false)));

// ------------------------------------------------------------------ start

(async () => {
  if (!memoryToken) return showLogin("");
  try {
    await api("GET", "me");
    await showApp();
  } catch {
    if (!$("app-view").hidden) return;
    showLogin("");
  }
})();
