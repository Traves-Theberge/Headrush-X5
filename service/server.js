'use strict';

const http = require('node:http');
const path = require('node:path');
const fs = require('node:fs');
const { execFile } = require('node:child_process');

const host = '127.0.0.1';
const port = Number(process.env.X5_API_PORT || 8765);
const cli = path.join(__dirname, '..', 'bin', 'service', 'Mx5BridgeCli.exe');
const vxBase = 'http://vx5.local/api/v1';
const vxState = '/AutoTune/headrush/autotune/state';
let deviceQueue = Promise.resolve();

function serial(task) {
  const run = deviceQueue.then(task, task);
  deviceQueue = run.catch(() => {});
  return run;
}

function bridge(args, timeout = 12000) {
  return serial(() => new Promise((resolve, reject) => {
    execFile(cli, args, { windowsHide: true, timeout, maxBuffer: 8 * 1024 * 1024 }, (error, stdout) => {
      let answer;
      try { answer = JSON.parse(stdout.trim()); } catch {
        return reject(new Error(error ? error.message : 'MX5 bridge returned invalid JSON'));
      }
      if (!answer.ok) return reject(new Error(answer.error || 'MX5 bridge failed'));
      if (error) return reject(new Error(error.message));
      resolve(answer.result);
    });
  }));
}

function send(res, status, value) {
  const body = JSON.stringify(value);
  res.writeHead(status, { 'Content-Type': 'application/json; charset=utf-8', 'Content-Length': Buffer.byteLength(body), 'Cache-Control': 'no-store' });
  res.end(body);
}

function input(req) {
  return new Promise((resolve, reject) => {
    let size = 0, chunks = [];
    req.on('data', chunk => {
      size += chunk.length;
      if (size > 65536) { reject(new Error('Request body is too large')); req.destroy(); return; }
      chunks.push(chunk);
    });
    req.on('end', () => {
      try { resolve(JSON.parse(Buffer.concat(chunks).toString('utf8'))); }
      catch { reject(new Error('Expected a JSON request body')); }
    });
    req.on('error', reject);
  });
}

function validPath(value) {
  return typeof value === 'string' && value.length <= 256 && /^\/Engine\/[\w/.-]+$/.test(value);
}

function transferRigs(root) {
  const rigRoot = path.join(root, 'Rigs');
  const rigs = [];
  if (!fs.existsSync(rigRoot)) return rigs;
  function walk(folder) {
    for (const entry of fs.readdirSync(folder, { withFileTypes: true })) {
      const full = path.join(folder, entry.name);
      if (entry.isDirectory()) walk(full);
      else if (entry.isFile() && /\.rig$/i.test(entry.name)) {
        const file = JSON.parse(fs.readFileSync(full, 'utf8'));
        rigs.push({ name: path.basename(entry.name, path.extname(entry.name)), path: path.relative(rigRoot, full), id: file.id || null, program: file.prog_num ?? null });
      }
    }
  }
  walk(rigRoot);
  return rigs.sort((a, b) => a.name.localeCompare(b.name, undefined, { numeric: true }));
}

async function vxRequest(route, method, body) {
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), 4500);
  try {
    const response = await fetch(vxBase + route, {
      method, signal: controller.signal,
      headers: body ? { 'Content-Type': 'application/json' } : {},
      body: body ? JSON.stringify(body) : undefined
    });
    if (!response.ok) throw new Error(`VX5 returned HTTP ${response.status}`);
    const text = await response.text();
    return text ? JSON.parse(text) : null;
  } finally { clearTimeout(timer); }
}

async function handle(req, res) {
  const url = new URL(req.url, `http://${host}:${port}`);
  const route = url.pathname;
  if (req.method === 'GET' && route === '/health') return send(res, 200, { ok: true, service: 'X5 Control API', version: 1 });

  if (req.method === 'GET' && route === '/api/v1/devices') {
    const [ports, drives] = await Promise.all([bridge(['ports']), bridge(['drives'])]);
    return send(res, 200, { ok: true, devices: {
      mx5: { bridgePortPresent: ports.inputs.some(x => /MX5/i.test(x)) && ports.outputs.some(x => /MX5/i.test(x)), transferDrives: drives },
      vx5: { address: vxBase, connected: await vxRequest('/object-properties/AutoTune/info', 'GET').then(() => true, () => false) }
    } });
  }
  if (req.method === 'GET' && route === '/api/v1/mx5/status') {
    const [ports, drives] = await Promise.all([bridge(['ports']), bridge(['drives'])]);
    const bridgePortPresent = ports.inputs.some(x => /MX5/i.test(x)) && ports.outputs.some(x => /MX5/i.test(x));
    let version = null;
    if (bridgePortPresent) version = (await bridge(['ping'])).bridge;
    return send(res, 200, { ok: true, bridgePortPresent, connected: Boolean(version), bridgeVersion: version, transferDrives: drives, ports });
  }
  if (req.method === 'GET' && route === '/api/v1/mx5/rigs') return send(res, 200, { ok: true, rigs: await bridge(['rigs'], 25000) });
  if (req.method === 'GET' && route === '/api/v1/mx5/transfer/rigs') {
    const drives = await bridge(['drives']);
    if (!drives.length) return send(res, 503, { ok: false, error: 'No HeadRush USB Transfer drive is connected' });
    return send(res, 200, { ok: true, mode: 'offline-transfer', rigs: transferRigs(drives[0].root) });
  }
  if (req.method === 'GET' && route === '/api/v1/mx5/properties') {
    const propertyPath = url.searchParams.get('path');
    if (!validPath(propertyPath)) return send(res, 400, { ok: false, error: 'Invalid MX5 property path' });
    return send(res, 200, { ok: true, property: await bridge(['get', propertyPath]) });
  }
  if (req.method === 'PUT' && route === '/api/v1/mx5/properties') {
    const body = await input(req);
    if (!validPath(body.path) || !['value', 'unnormalized', 'state', 'index', 'string', 'user'].includes(body.field) || !['string', 'number', 'boolean'].includes(typeof body.value) || String(body.value).length > 1024 || /[\r\n\t]/.test(String(body.value)))
      return send(res, 400, { ok: false, error: 'Invalid MX5 property update' });
    return send(res, 200, { ok: true, property: await bridge(['set', body.path, body.field, String(body.value)]) });
  }
  if (req.method === 'POST' && route === '/api/v1/mx5/rigs/load') {
    const body = await input(req);
    if (!Number.isInteger(body.program) || body.program < 1 || body.program > 128) return send(res, 400, { ok: false, error: 'Program must be 1 through 128' });
    return send(res, 200, { ok: true, property: await bridge(['load', String(body.program)]) });
  }
  const foot = /^\/api\/v1\/mx5\/footswitches\/([1-3])\/press$/.exec(route);
  if (req.method === 'POST' && foot) return send(res, 200, { ok: true, property: await bridge(['footswitch', foot[1]]) });

  if (req.method === 'GET' && route === '/api/v1/vx5/state')
    return send(res, 200, { ok: true, state: await vxRequest('/object-properties' + vxState, 'GET') });
  if (req.method === 'GET' && route === '/api/v1/vx5/metadata')
    return send(res, 200, { ok: true, tree: await vxRequest('/subtree', 'GET') });
  if (req.method === 'PUT' && route === '/api/v1/vx5/state') {
    const body = await input(req);
    if (!body || Array.isArray(body) || typeof body !== 'object' || Object.keys(body).length > 64 || Object.values(body).some(x => !['string', 'number', 'boolean'].includes(typeof x)))
      return send(res, 400, { ok: false, error: 'Expected a parameter object with scalar values' });
    await vxRequest('/object-properties' + vxState, 'PUT', body);
    return send(res, 200, { ok: true, state: await vxRequest('/object-properties' + vxState, 'GET') });
  }
  return send(res, 404, { ok: false, error: 'Route not found' });
}

if (!Number.isInteger(port) || port < 1 || port > 65535) throw new Error('Invalid X5_API_PORT');
http.createServer((req, res) => {
  handle(req, res).catch(error => {
    if (res.writableEnded || res.destroyed) return;
    const disconnected = /No MX5 Bridge MIDI|did not reply|ECONNREFUSED|ENOTFOUND|fetch failed|abort/i.test(error.message);
    send(res, disconnected ? 503 : 500, { ok: false, error: error.message });
  });
}).listen(port, host, () => process.stdout.write(`X5 Control API listening on http://${host}:${port}\n`));
