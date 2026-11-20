namespace Kairos.Api.Modules.Identity.Passkeys;

/// <summary>
/// Development-only page to try passkeys in a real browser.
/// </summary>
public static class PasskeyDemoEndpoint
{
    public static IEndpointRouteBuilder MapPasskeyDemo(
        this IEndpointRouteBuilder app)
    {
        app.MapGet("/passkeys/demo", () =>
                Results.Content(Html, "text/html; charset=utf-8"))
            .AllowAnonymous()
            .ExcludeFromDescription();

        return app;
    }

    private const string Html = """
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Kairos passkeys (development only)</title>
<style>
  body { font-family: system-ui, sans-serif; max-width: 44rem; margin: 2rem auto; padding: 0 1rem; line-height: 1.5; }
  input { width: 100%; box-sizing: border-box; padding: .5rem; margin: .25rem 0 .5rem; }
  button { padding: .5rem 1rem; margin-right: .5rem; cursor: pointer; }
  pre { background: #f2f2f2; color: #111; padding: 1rem; overflow: auto; white-space: pre-wrap; word-break: break-all; }
</style>
</head>
<body>
<h1>Kairos passkeys <small>(development only)</small></h1>

<h2>1. Register a passkey</h2>
<p>Sign in with email and password first (Swagger), then paste the access token here.</p>
<input id="token" placeholder="Access token (JWT)">
<input id="name" placeholder="Passkey name (e.g. My laptop)">
<button id="register">Register passkey</button>
<button id="list">List my passkeys</button>

<h2>2. Sign in with a passkey</h2>
<button id="login">Sign in with passkey</button>

<pre id="out">Result appears here.</pre>

<script>
const $ = id => document.getElementById(id);
const show = (title, value) => {
  $('out').textContent = title + '\n\n' +
    (typeof value === 'string' ? value : JSON.stringify(value, null, 2));
};

const toBuf = s => {
  s = s.replace(/-/g, '+').replace(/_/g, '/');
  const bin = atob(s + '='.repeat((4 - s.length % 4) % 4));
  return Uint8Array.from(bin, c => c.charCodeAt(0)).buffer;
};
const toB64u = buf => btoa(String.fromCharCode(...new Uint8Array(buf)))
  .replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');

async function api(path, { method = 'POST', body, token } = {}) {
  const headers = { 'X-Inbox-Id': crypto.randomUUID() };
  if (body) headers['Content-Type'] = 'application/json';
  if (token) headers['Authorization'] = 'Bearer ' + token;
  const res = await fetch('/api/v1/auth/passkeys' + path, {
    method, headers, body: body ? JSON.stringify(body) : undefined
  });
  const json = await res.json().catch(() => ({}));
  if (!res.ok) throw new Error(res.status + ' ' + JSON.stringify(json));
  return json;
}
const payload = envelope => envelope.data ?? envelope.Data;

$('register').onclick = async () => {
  try {
    const token = $('token').value.trim();
    const start = payload(await api('/register/options', { token }));
    const o = start.options;
    o.challenge = toBuf(o.challenge);
    o.user.id = toBuf(o.user.id);
    (o.excludeCredentials || []).forEach(c => c.id = toBuf(c.id));

    const cred = await navigator.credentials.create({ publicKey: o });

    const done = await api('/register/complete', { token, body: {
      flowId: start.flowId,
      name: $('name').value.trim() || null,
      credential: {
        id: cred.id,
        rawId: toB64u(cred.rawId),
        type: cred.type,
        response: {
          attestationObject: toB64u(cred.response.attestationObject),
          clientDataJSON: toB64u(cred.response.clientDataJSON)
        }
      }
    }});
    show('Passkey registered', done);
  } catch (e) { show('Error', String(e)); }
};

$('login').onclick = async () => {
  try {
    const start = payload(await api('/login/options'));
    const o = start.options;
    o.challenge = toBuf(o.challenge);
    (o.allowCredentials || []).forEach(c => c.id = toBuf(c.id));

    const cred = await navigator.credentials.get({ publicKey: o });

    const done = await api('/login/complete', { body: {
      flowId: start.flowId,
      credential: {
        id: cred.id,
        rawId: toB64u(cred.rawId),
        type: cred.type,
        response: {
          authenticatorData: toB64u(cred.response.authenticatorData),
          clientDataJSON: toB64u(cred.response.clientDataJSON),
          signature: toB64u(cred.response.signature),
          userHandle: cred.response.userHandle
            ? toB64u(cred.response.userHandle) : null
        }
      }
    }});
    show('Signed in with passkey', done);
  } catch (e) { show('Error', String(e)); }
};

$('list').onclick = async () => {
  try {
    show('My passkeys', await api('', { method: 'GET', token: $('token').value.trim() }));
  } catch (e) { show('Error', String(e)); }
};
</script>
</body>
</html>
""";
}