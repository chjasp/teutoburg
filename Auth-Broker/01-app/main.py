# main.py
import os, secrets, hmac, hashlib, json, time
from urllib.parse import urlencode

import httpx
from fastapi import FastAPI, Request, HTTPException, Query
from fastapi.responses import RedirectResponse, HTMLResponse, PlainTextResponse

from google.cloud import firestore
from google.cloud import secretmanager
from google.api_core.exceptions import NotFound

from dotenv import load_dotenv
load_dotenv()

app = FastAPI(title="WHOOP Token Broker (Firestore)")

# ---- Config ----
WHOOP_AUTH_URL  = os.getenv("WHOOP_AUTH_URL",  "https://api.prod.whoop.com/oauth/oauth2/auth")
WHOOP_TOKEN_URL = os.getenv("WHOOP_TOKEN_URL", "https://api.prod.whoop.com/oauth/oauth2/token")
WHOOP_SCOPES    = os.getenv("WHOOP_SCOPES",    "offline read:recovery read:cycles read:sleep read:workout")

# Secrets provided via Secret Manager -> env (Cloud Run injects env from secrets)
WHOOP_CLIENT_ID     = os.getenv("WHOOP_CLIENT_ID")
WHOOP_CLIENT_SECRET = os.getenv("WHOOP_CLIENT_SECRET")
BROKER_API_KEY      = os.getenv("BROKER_API_KEY")  # used for header on /access-token and HMAC state signing

STATE_COOKIE = "whoop_oauth_state"
STATE_TTL_S  = 10 * 60

# Firestore
db = firestore.Client()
COLL = os.getenv("FIRESTORE_COLLECTION", "whoop_auth")  # documents keyed by app_user_id

# Optional: Secret Manager client (only if you prefer fetching secrets at runtime)
_sm = secretmanager.SecretManagerServiceClient()

def _sign_state(app_user_id: str, nonce: str) -> str:
    if not BROKER_API_KEY:
        # For dev: still return a basic state
        return f"{app_user_id}.{nonce}.nosig"
    msg = f"{app_user_id}.{nonce}".encode()
    sig = hmac.new(BROKER_API_KEY.encode(), msg, hashlib.sha256).hexdigest()[:24]
    return f"{app_user_id}.{nonce}.{sig}"

def _verify_state(state: str) -> str:
    """Return app_user_id if signature is valid; else raise."""
    try:
        app_user_id, nonce, sig = state.split(".", 2)
    except ValueError:
        raise HTTPException(400, "Invalid state format")
    if BROKER_API_KEY and sig != hmac.new(BROKER_API_KEY.encode(), f"{app_user_id}.{nonce}".encode(), hashlib.sha256).hexdigest()[:24]:
        raise HTTPException(400, "Invalid state signature")
    return app_user_id

def _require_api_key(request: Request):
    if not BROKER_API_KEY:
        return
    key = request.headers.get("X-Api-Key")
    if key != BROKER_API_KEY:
        raise HTTPException(status_code=401, detail="Invalid API key")

@app.get("/healthz")
def healthz():
    return {"ok": True}

@app.get("/login")
def login(request: Request, app_user_id: str = Query(..., description="Your app's user id")):
    """Start OAuth for a specific app user."""
    if not WHOOP_CLIENT_ID or not WHOOP_CLIENT_SECRET:
        raise HTTPException(500, "Server missing WHOOP_CLIENT_ID/WHOOP_CLIENT_SECRET")

    redirect_uri = str(request.url_for("oauth_callback"))
    nonce = secrets.token_urlsafe(16)
    state = _sign_state(app_user_id, nonce)

    params = {
        "response_type": "code",
        "client_id": WHOOP_CLIENT_ID,
        "redirect_uri": redirect_uri,
        "scope": WHOOP_SCOPES,
        "state": state,
    }
    url = f"{WHOOP_AUTH_URL}?{urlencode(params)}"

    resp = RedirectResponse(url)
    # state cookie (not strictly necessary since we sign the state,
    # but we can store expiry to expire the flow)
    payload = {"nonce": nonce, "exp": int(time.time()) + STATE_TTL_S}
    resp.set_cookie(
        key=STATE_COOKIE,
        value=json.dumps(payload),
        httponly=True,
        secure=True,
        samesite="lax",
        max_age=STATE_TTL_S
    )
    return resp

@app.get("/oauth/callback")
async def oauth_callback(request: Request, code: str | None = None, state: str | None = None):
    """Exchange code, store refresh token in Firestore under app_user_id."""
    if not code or not state:
        raise HTTPException(400, "Missing code/state")

    # Optional cookie expiry check
    try:
        c = request.cookies.get(STATE_COOKIE)
        if c:
            meta = json.loads(c)
            if int(time.time()) > int(meta.get("exp", 0)):
                raise HTTPException(400, "State expired, restart login")
    except Exception:
        pass  # tolerate missing/invalid cookie if state signature is valid

    # Verify state and extract app_user_id
    app_user_id = _verify_state(state)

    redirect_uri = str(request.url_for("oauth_callback"))
    async with httpx.AsyncClient(timeout=30) as client:
        data = {
            "grant_type": "authorization_code",
            "code": code,
            "redirect_uri": redirect_uri,
            "client_id": WHOOP_CLIENT_ID,
            "client_secret": WHOOP_CLIENT_SECRET,
        }
        r = await client.post(WHOOP_TOKEN_URL, data=data, headers={"Content-Type": "application/x-www-form-urlencoded"})
        r.raise_for_status()
        tokens = r.json()

    refresh_token = tokens.get("refresh_token")
    if not refresh_token:
        raise HTTPException(500, "WHOOP did not return a refresh token (include 'offline' scope).")

    # Store/overwrite in Firestore: one doc per app user
    doc_ref = db.collection(COLL).document(app_user_id)
    doc_ref.set({
        "refresh_token": refresh_token,
        "created_at": firestore.SERVER_TIMESTAMP,
        "scopes": WHOOP_SCOPES,
        "whoop_client_id": WHOOP_CLIENT_ID,
    }, merge=True)

    return HTMLResponse(
        "<h1>WHOOP connected</h1><p>You can close this window and return to the app.</p>"
    )

@app.get("/access-token")
async def access_token(request: Request, app_user_id: str = Query(..., description="Your app's user id")):
    """Return a fresh access token for the given app user."""
    _require_api_key(request)

    doc = db.collection(COLL).document(app_user_id).get()
    if not doc.exists:
        raise HTTPException(404, "No WHOOP link for this user. Call /login first.")

    refresh_token = doc.to_dict().get("refresh_token")
    if not refresh_token:
        raise HTTPException(404, "Missing refresh token for user.")

    async with httpx.AsyncClient(timeout=30) as client:
        data = {
            "grant_type": "refresh_token",
            "refresh_token": refresh_token,
            "client_id": WHOOP_CLIENT_ID,
            "client_secret": WHOOP_CLIENT_SECRET,
        }
        r = await client.post(WHOOP_TOKEN_URL, data=data, headers={"Content-Type": "application/x-www-form-urlencoded"})
    if r.status_code >= 400:
        raise HTTPException(r.status_code, r.text)

    token = r.json().get("access_token")
    if not token:
        raise HTTPException(500, "WHOOP did not return an access token")
    return PlainTextResponse(token)

# curl -H "Authorization: Bearer o31GbKX3JaUN-63kl9v73zMKBXr_rnz4Bc5idRzGaWE.EIzR9bsNEbCDR9x8M3AVRovFQ2kL0Mg7etit20a1gYY%" "https://api.prod.whoop.com/developer/v1/recovery?limit=1"