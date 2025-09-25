# Test the health check endpoint
curl http://127.0.0.1:8000/healthz
# Expected output: {"ok":true}

# ---

# To test the full authentication flow, open this URL in your browser:
# Replace 'test_user' with any user identifier you want.
# http://127.0.0.1:8000/login?app_user_id=test_user

# ---

# After a user has successfully authenticated through the browser,
# you can get an access token for them using this command.
# Make sure to use the same BROKER_API_KEY and app_user_id.
curl -H "X-Api-Key: 2nvonvow" "http://127.0.0.1:8000/access-token?app_user_id=test_user"