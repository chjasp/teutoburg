# main.py
import os, secrets, hmac, hashlib, json, time, logging
from urllib.parse import urlencode

import httpx
from fastapi import FastAPI, Request, HTTPException, Query
from fastapi.responses import RedirectResponse, HTMLResponse, PlainTextResponse

from google.cloud import firestore

from dotenv import load_dotenv

load_dotenv()

# Configure logging
LOG_LEVEL = os.getenv("LOG_LEVEL", "INFO").upper()
logging.basicConfig(
    level=getattr(logging, LOG_LEVEL, logging.INFO),
    format='%(asctime)s - %(levelname)s - %(message)s'
)
logger = logging.getLogger(__name__)

app = FastAPI(title="WHOOP Token Broker (Firestore)")

# ---- Config ----
WHOOP_AUTH_URL  = os.getenv("WHOOP_AUTH_URL",  "https://api.prod.whoop.com/oauth/oauth2/auth")
WHOOP_TOKEN_URL = os.getenv("WHOOP_TOKEN_URL", "https://api.prod.whoop.com/oauth/oauth2/token")
WHOOP_SCOPES    = os.getenv("WHOOP_SCOPES",    "offline read:recovery read:sleep read:workout")

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

# Log startup configuration (sanitized)
logger.info("WHOOP Token Broker starting (log=%s, redirect_uri=%s)", LOG_LEVEL, REDIRECT_URI or "auto")


def _sign_state(app_user_id: str, nonce: str) -> str:
    if not BROKER_API_KEY:
        logger.warning("BROKER_API_KEY not set - using unsigned state")
        # For dev: still return a basic state
        return f"{app_user_id}.{nonce}.nosig"
    msg = f"{app_user_id}.{nonce}".encode()
    sig = hmac.new(BROKER_API_KEY.encode(), msg, hashlib.sha256).hexdigest()[:24]
    return f"{app_user_id}.{nonce}.{sig}"

def _verify_state(state: str) -> str:
    """Return app_user_id if signature is valid; else raise."""
    logger.debug("Verifying state")
    try:
        app_user_id, nonce, sig = state.split(".", 2)
    except ValueError:
        logger.error(f"Invalid state format: {state}")
        raise HTTPException(400, "Invalid state format")
    if BROKER_API_KEY and sig != hmac.new(BROKER_API_KEY.encode(), f"{app_user_id}.{nonce}".encode(), hashlib.sha256).hexdigest()[:24]:
        logger.error(f"Invalid state signature for user: {app_user_id}")
        raise HTTPException(400, "Invalid state signature")
    logger.debug("State verified")
    return app_user_id

def _require_api_key(request: Request):
    if not BROKER_API_KEY:
        logger.warning("BROKER_API_KEY not configured - skipping API key validation")
        return
    key = request.headers.get("X-Api-Key")
    if key != BROKER_API_KEY:
        raise HTTPException(status_code=401, detail="Invalid API key")

@app.exception_handler(Exception)
async def global_exception_handler(request: Request, exc: Exception):
    """Catch all unhandled exceptions and log them."""
    logger.exception(f"Unhandled exception on {request.method} {request.url.path}")
    
    # Return a generic error to the client
    return PlainTextResponse(
        content=f"Internal server error: {type(exc).__name__}: {str(exc)}",
        status_code=500
    )

@app.get("/healthcheck")
def healthcheck():
    return {"ok": True}

@app.get("/login")
def login(request: Request, app_user_id: str = Query(..., description="Your app's user id")):
    """Start OAuth for a specific app user."""
    logger.debug("Login request")
    
    if not WHOOP_CLIENT_ID or not WHOOP_CLIENT_SECRET:
        logger.error("Missing WHOOP_CLIENT_ID or WHOOP_CLIENT_SECRET")
        raise HTTPException(500, "Server missing WHOOP_CLIENT_ID/WHOOP_CLIENT_SECRET")

    redirect_uri = REDIRECT_URI or str(request.url_for("oauth_callback"))
    logger.debug("Using redirect URI")
    
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
    logger.debug("Redirecting to WHOOP auth URL")

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
    logger.debug("OAuth callback received")
    
    if not code or not state:
        logger.error("Missing code or state in callback")
        raise HTTPException(400, "Missing code/state")

    # Optional cookie expiry check
    try:
        c = request.cookies.get(STATE_COOKIE)
        if c:
            meta = json.loads(c)
            if int(time.time()) > int(meta.get("exp", 0)):
                logger.error(f"State cookie expired for state: {state[:20]}...")
                raise HTTPException(400, "State expired, restart login")
    except Exception as e:
        logger.debug(f"Cookie validation error (tolerated): {e}")
        pass  # tolerate missing/invalid cookie if state signature is valid

    # Verify state and extract app_user_id
    app_user_id = _verify_state(state)
    logger.debug("Processing OAuth callback")

    redirect_uri = REDIRECT_URI or str(request.url_for("oauth_callback"))
    async with httpx.AsyncClient(timeout=30) as client:
        data = {
            "grant_type": "authorization_code",
            "code": code,
            "redirect_uri": redirect_uri,
            "client_id": WHOOP_CLIENT_ID,
            "client_secret": WHOOP_CLIENT_SECRET,
        }
        logger.debug("Exchanging auth code for tokens")
        r = await client.post(WHOOP_TOKEN_URL, data=data, headers={"Content-Type": "application/x-www-form-urlencoded"})
        
        if r.status_code >= 400:
            logger.error(f"WHOOP token exchange failed - status: {r.status_code}")
        
        r.raise_for_status()
        tokens = r.json()
        logger.debug("Token exchange successful")

    refresh_token = tokens.get("refresh_token")
    if not refresh_token:
        logger.error(f"No refresh token in WHOOP response - response keys: {list(tokens.keys())}")
        raise HTTPException(500, "WHOOP did not return a refresh token (include 'offline' scope).")

    # Store/overwrite in Firestore: one doc per app user
    logger.debug("Storing refresh token in Firestore")
    doc_ref = db.collection(COLL).document(app_user_id)
    doc_ref.set({
        "refresh_token": refresh_token,
        "created_at": firestore.SERVER_TIMESTAMP,
        "scopes": WHOOP_SCOPES,
        "whoop_client_id": WHOOP_CLIENT_ID,
        "last_refresh_at": None,
    }, merge=True)
    logger.debug("Successfully stored tokens")

    return HTMLResponse(
        "<h1>WHOOP connected</h1><p>You can close this window and return to the app.</p>"
    )

@app.get("/access-token")
async def access_token(request: Request, app_user_id: str = Query(..., description="Your app's user id")):
    """Return a fresh access token for the given app user."""
    logger.debug("Access token request")
    
    try:
        _require_api_key(request)
    except HTTPException as e:
        raise

    doc_ref = db.collection(COLL).document(app_user_id)
    doc = doc_ref.get()
    
    if not doc.exists:
        raise HTTPException(404, "No WHOOP link for this user. Call /login first.")
    
    doc_data = doc.to_dict()
    
    refresh_token = doc_data.get("refresh_token") if doc_data else None
    if not refresh_token:
        raise HTTPException(404, "Missing refresh token for user.")

    logger.debug("Refreshing access token from WHOOP")
    async with httpx.AsyncClient(timeout=30) as client:
        data = {
            "grant_type": "refresh_token",
            "refresh_token": refresh_token,
            "client_id": WHOOP_CLIENT_ID,
            "client_secret": WHOOP_CLIENT_SECRET,
        }
        r = await client.post(WHOOP_TOKEN_URL, data=data, headers={"Content-Type": "application/x-www-form-urlencoded"})

    # Handle refresh failures explicitly for invalid_grant
    if r.status_code >= 400:
        try:
            err_json = r.json()
        except Exception:
            err_json = None
        err_code = (err_json or {}).get("error") if isinstance(err_json, dict) else None
        if err_code == "invalid_grant" or (isinstance(err_json, dict) and (err_json.get("error_description") or "").lower().startswith("invalid_grant")) or "invalid_grant" in (r.text or ""):
            # Invalidate stored refresh token so client can re-link
            try:
                doc_ref.update({
                    "refresh_token": firestore.DELETE_FIELD,
                    "invalidated_at": firestore.SERVER_TIMESTAMP,
                })
            except Exception:
                pass
            raise HTTPException(401, "Refresh token invalid. Please re-link WHOOP via /login.")
        raise HTTPException(r.status_code, r.text)

    token_response = r.json()
    
    token = token_response.get("access_token")
    if not token:
        raise HTTPException(500, "WHOOP did not return an access token")
    
    # Optional rotation: update stored refresh_token if provider rotated it
    new_refresh_token = token_response.get("refresh_token")
    try:
        updates = {"last_refresh_at": firestore.SERVER_TIMESTAMP}
        if new_refresh_token and new_refresh_token != refresh_token:
            updates["refresh_token"] = new_refresh_token
            updates["rotated_at"] = firestore.SERVER_TIMESTAMP
        if updates:
            doc_ref.update(updates)
    except Exception:
        # Do not fail token issuance due to storage issues
        pass

    return PlainTextResponse(token)
