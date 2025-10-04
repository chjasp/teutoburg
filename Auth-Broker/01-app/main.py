# main.py
import os, secrets, hmac, hashlib, json, time
from urllib.parse import urlencode

import httpx
from fastapi import FastAPI, Request, HTTPException, Query
from fastapi.responses import RedirectResponse, HTMLResponse, PlainTextResponse

from google.cloud import firestore
from datetime import datetime, timedelta, timezone

from dotenv import load_dotenv

load_dotenv()

app = FastAPI(title="WHOOP Token Broker (Firestore)")

# ---- Config ----
WHOOP_AUTH_URL  = os.getenv("WHOOP_AUTH_URL",  "https://api.prod.whoop.com/oauth/oauth2/auth")
WHOOP_TOKEN_URL = os.getenv("WHOOP_TOKEN_URL", "https://api.prod.whoop.com/oauth/oauth2/token")
WHOOP_SCOPES    = os.getenv("WHOOP_SCOPES",    "offline read:recovery read:sleep read:workout")
WHOOP_API_BASE  = os.getenv("WHOOP_API_BASE",  "https://api.prod.whoop.com")

# Secrets provided via Secret Manager -> env (Cloud Run injects env from secrets)
WHOOP_CLIENT_ID     = os.getenv("WHOOP_CLIENT_ID")
WHOOP_CLIENT_SECRET = os.getenv("WHOOP_CLIENT_SECRET")
BROKER_API_KEY      = os.getenv("BROKER_API_KEY")  # used for header on /access-token and HMAC state signing

STATE_COOKIE = "whoop_oauth_state"
STATE_TTL_S  = 10 * 60

# Firestore
db = firestore.Client()
COLL = os.getenv("FIRESTORE_COLLECTION", "whoop_auth")  # documents keyed by app_user_id

# Optional: override redirect URI (useful for local dev)
REDIRECT_URI = os.getenv("REDIRECT_URI")  # e.g., "http://localhost:8080/oauth/callback"


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


async def _generate_access_token(app_user_id: str) -> str:
    """Generate a fresh access token using stored refresh token for the given app user."""
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
    return token

@app.get("/healthz")
def healthz():
    return {"ok": True}

@app.get("/login")
def login(request: Request, app_user_id: str = Query(..., description="Your app's user id")):
    """Start OAuth for a specific app user."""
    if not WHOOP_CLIENT_ID or not WHOOP_CLIENT_SECRET:
        raise HTTPException(500, "Server missing WHOOP_CLIENT_ID/WHOOP_CLIENT_SECRET")

    redirect_uri = REDIRECT_URI or str(request.url_for("oauth_callback"))
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

    redirect_uri = REDIRECT_URI or str(request.url_for("oauth_callback"))
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
    token = await _generate_access_token(app_user_id)
    return PlainTextResponse(token)


@app.get("/sleep-score")
async def sleep_score(
    request: Request,
    app_user_id: str = Query(..., description="Your app's user id"),
    start: str | None = Query(None, description="ISO8601 start (default: yesterday 00:00Z)"),
    end: str | None = Query(None, description="ISO8601 end (default: today 00:00Z)"),
):
    """Return the latest sleep_performance_percentage (integer 0-100) for the window.

    If no window is provided, queries yesterday 00:00Z to today 00:00Z.
    """
    _require_api_key(request)

    # Resolve time window
    if not start or not end:
        now = datetime.now(timezone.utc)
        today_00 = now.replace(hour=0, minute=0, second=0, microsecond=0)
        yesterday_00 = today_00 - timedelta(days=1)
        start_iso = yesterday_00.isoformat().replace("+00:00", "Z")
        end_iso = today_00.isoformat().replace("+00:00", "Z")
    else:
        start_iso = start
        end_iso = end

    token = await _generate_access_token(app_user_id)

    # Query WHOOP for sleep activities in the time window
    url = f"{WHOOP_API_BASE}/developer/v2/activity/sleep?start={start_iso}&end={end_iso}"
    async with httpx.AsyncClient(timeout=30) as client:
        r = await client.get(url, headers={"Authorization": f"Bearer {token}"})
    if r.status_code >= 400:
        raise HTTPException(r.status_code, r.text)

    try:
        payload = r.json()
        records = payload.get("records", []) if isinstance(payload, dict) else []
        if not records:
            # No sleep in window; return 0
            return PlainTextResponse("0")

        # Choose the most recent record by end timestamp if present
        def _end_ts(rec: dict) -> str:
            return rec.get("end", rec.get("end_time", ""))

        latest = max(records, key=_end_ts)
        score = latest.get("score", {}) or {}
        pct = score.get("sleep_performance_percentage")
        if pct is None:
            return PlainTextResponse("0")

        # Coerce to int (assuming 0-100 range)
        try:
            pct_int = int(round(float(pct)))
        except Exception:
            pct_int = 0
        return PlainTextResponse(str(max(0, min(100, pct_int))))
    except Exception as e:
        # Be conservative on parse errors
        return PlainTextResponse("0")
