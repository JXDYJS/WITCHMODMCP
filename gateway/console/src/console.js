/**
 * console.js — browser Lua REPL console (CodeMirror 6 + WebSocket).
 *
 * Bundled into gateway/console/dist/console.js by esbuild; served by the
 * gateway's local HTTP server. Connects to the in-game Lua WebSocket
 * (mod port + 1) — the execution channel MUST stay in the game process.
 */
import { EditorState } from "@codemirror/state";
import { EditorView, keymap } from "@codemirror/view";
import { basicSetup } from "codemirror";
import { StreamLanguage } from "@codemirror/language";
import { lua } from "@codemirror/legacy-modes/mode/lua";
import { oneDark } from "@codemirror/theme-one-dark";
import { indentWithTab } from "@codemirror/commands";

// ── DOM helpers ────────────────────────────────────────────────
const $ = (id) => document.getElementById(id);
const outputEl = $("output");
const statusEl = $("status");
const runBtn = $("run");

const escapeHtml = (s) =>
  String(s).replace(/[&<>"']/g, (c) => (
    { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]
  ));

// ANSI SGR colors used by the game's ConsoleFormatter (fg 30-37/90-97, reset 0)
const ANSI_CLASS = {
  "0": "", "30": "c-gray", "31": "c-red", "32": "c-green", "33": "c-yellow",
  "34": "c-blue", "35": "c-magenta", "36": "c-cyan", "37": "c-white",
  "90": "c-gray", "91": "c-red", "92": "c-green", "93": "c-yellow",
  "94": "c-blue", "95": "c-magenta", "96": "c-cyan", "97": "c-white",
};

/** Escape HTML first, then translate ANSI SGR codes into color spans. */
function ansiToHtml(text) {
  const escaped = escapeHtml(text);
  return escaped.replace(/\x1b\[([0-9;]*)m/g, (match, codes) => {
    let out = "";
    for (const p of String(codes).split(";")) {
      if (p === "0") out += "</span>";
      else if (ANSI_CLASS[p]) out += '<span class="' + ANSI_CLASS[p] + '">';
    }
    return out;
  });
}

function appendOutput(html) {
  const div = document.createElement("div");
  div.innerHTML = html;
  outputEl.appendChild(div);
  outputEl.scrollTop = outputEl.scrollHeight;
}

function setStatus(text, cls) {
  statusEl.textContent = text;
  statusEl.className = cls || "";
}

// ── WebSocket (in-game Lua executor) ────────────────────────────
const WS_PORT = Number(window.__WS_PORT__) || 3101;
$("ws-host").textContent = location.hostname + ":" + WS_PORT;

let ws = null;
let retryTimer = null;

function wsUrl() {
  const proto = location.protocol === "https:" ? "wss:" : "ws:";
  return proto + "//" + location.hostname + ":" + WS_PORT + "/ws";
}

function connect() {
  setStatus("连接 " + location.hostname + ":" + WS_PORT + "…", "warn");
  runBtn.disabled = true;
  try {
    ws = new WebSocket(wsUrl());
  } catch (e) {
    setStatus("WebSocket 创建失败：" + e.message, "err");
    retryTimer = setTimeout(connect, 3000);
    return;
  }

  ws.onopen = () => {
    setStatus("已连接", "ok");
    runBtn.disabled = false;
  };

  ws.onmessage = (e) => {
    let msg;
    try { msg = JSON.parse(e.data); } catch {
      appendOutput('<span class="c-red">[raw] ' + escapeHtml(e.data) + "</span>");
      return;
    }
    if (msg.type === "error") {
      appendOutput('<span class="c-red">' + ansiToHtml(msg.message || "") + "</span>");
    } else {
      appendOutput(ansiToHtml(msg.data || ""));
    }
  };

  ws.onclose = () => {
    setStatus("已断开，3 秒后重连…", "err");
    runBtn.disabled = true;
    if (retryTimer) clearTimeout(retryTimer);
    retryTimer = setTimeout(connect, 3000);
  };
  ws.onerror = () => {};
}

// ── Run ────────────────────────────────────────────────────────
function runCode() {
  const code = view.state.doc.toString();
  if (!code.trim()) return;
  if (!ws || ws.readyState !== WebSocket.OPEN) {
    appendOutput('<span class="c-red">未连接，无法执行。</span>');
    return;
  }
  appendOutput('<span class="c-gray">&gt;&gt; ' + escapeHtml(code) + "</span>");
  ws.send(JSON.stringify({ cmd: "exec", code }));
}

// ── Editor (CodeMirror 6) ──────────────────────────────────────
const runKeymap = keymap.of([
  { key: "Mod-Enter", run: () => { runCode(); return true; } },
  indentWithTab,
]);

const view = new EditorView({
  parent: $("editor"),
  state: EditorState.create({
    doc: "-- Witch Lua REPL：输入 Lua 代码，Ctrl+Enter 执行\n-- Shift+Enter 换行\n",
    extensions: [
      basicSetup,
      StreamLanguage.define(lua),
      oneDark,
      runKeymap,
      EditorView.theme({
        "&": { height: "100%", fontSize: "13px" },
        ".cm-scroller": { overflow: "auto" },
      }),
    ],
  }),
});

runBtn.addEventListener("click", runCode);
connect();