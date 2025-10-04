set -e  # Exit on error

# Configuration
BASE_URL="http://127.0.0.1:8080"
# BASE_URL="https://whoop-broker-949011922332.europe-west3.run.app"
TEST_USER_ID="test_user"
API_KEY="KticiRN1bI8id26CgYjjCeJ782Q8L91CqQtHCw7tIiI"
WHOOP_API_BASE="https://api.prod.whoop.com"


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

print_section "Test 3: Get Access Token"
BEARER_TOKEN=$(curl -s -H "X-Api-Key: $API_KEY" "$BASE_URL/access-token?app_user_id=$TEST_USER_ID")
echo "$BEARER_TOKEN"
echo ""

# ==================================================
# WHOOP API Integration Tests
# (Requires valid bearer token)
# ==================================================

# Check if we got a valid access token (not an error JSON)
if [[ "$BEARER_TOKEN" == *"detail"* ]] || [ -z "$BEARER_TOKEN" ]; then
    echo -e "\n${BLUE}Skipping WHOOP API tests - no valid access token (user may not have completed OAuth flow)${NC}\n"
    exit 0
fi

print_section "Test 4: WHOOP - Get Sleep Activity (Yesterday)"
# Calculate yesterday's date range
START_DATE=$(date -u -v-1d +"%Y-%m-%dT00:00:00.000Z")
END_DATE=$(date -u +"%Y-%m-%dT00:00:00.000Z")
echo "Querying sleep data from $START_DATE to $END_DATE"
curl -s "$WHOOP_API_BASE/developer/v2/activity/sleep?start=$START_DATE&end=$END_DATE" \
    -H "Authorization: Bearer $BEARER_TOKEN"
echo ""

echo -e "${GREEN}All tests completed!${NC}\n"