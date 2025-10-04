set -e  # Exit on error

# Configuration
BASE_URL="http://127.0.0.1:8080"
# BASE_URL="https://whoop-broker-949011922332.europe-west3.run.app"
TEST_USER_ID="test_user"
API_KEY="KticiRN1bI8id26CgYjjCeJ782Q8L91CqQtHCw7tIiI"
WHOOP_API_BASE="https://api.prod.whoop.com"

# Note: Replace with actual bearer token for testing WHOOP API endpoints
BEARER_TOKEN="${WHOOP_ACCESS_TOKEN:-YOUR_ACCESS_TOKEN_HERE}"

# Color codes for output
GREEN='\033[0;32m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Helper function to print test section headers
print_section() {
    echo -e "\n${BLUE}===================================================${NC}"
    echo -e "${BLUE}$1${NC}"
    echo -e "${BLUE}===================================================${NC}\n"
}

# ==================================================
# Auth Broker Tests
# ==================================================

print_section "Test 1: Health Check"
curl -s "$BASE_URL/healthcheck"
echo ""

print_section "Test 2: Login (initiates OAuth flow)"
curl -i "$BASE_URL/login?app_user_id=$TEST_USER_ID"
echo ""
