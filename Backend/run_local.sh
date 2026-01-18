#!/bin/bash
# Run the Sacrifice Food Analysis API locally
# Uses GCP Application Default Credentials for Vertex AI

set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
cd "$SCRIPT_DIR"

# Check for .env file
if [ ! -f ".env" ]; then
    echo "⚠️  No .env file found. Copy env.example to .env and configure."
    echo "   cp env.example .env"
    exit 1
fi

# Check for GCP authentication
if ! gcloud auth application-default print-access-token &>/dev/null; then
    echo "⚠️  No GCP Application Default Credentials found."
    echo "   Run: gcloud auth application-default login"
    echo "   Then: gcloud config set project steam-378309"
    exit 1
fi

echo "✅ GCP credentials found"

# Create virtual environment if it doesn't exist
if [ ! -d "venv" ]; then
    echo "📦 Creating virtual environment..."
    python3 -m venv venv
fi

# Activate virtual environment
source venv/bin/activate

# Install dependencies
echo "📦 Installing dependencies..."
pip install -q -r requirements.txt

# Run the server
echo ""
echo "🚀 Starting Sacrifice Food Analysis API on http://localhost:8080"
echo "   Using GCP project: steam-378309 (Vertex AI)"
echo "   Health check: http://localhost:8080/health"
echo "   Analyze endpoint: POST http://localhost:8080/analyze"
echo ""
python -m uvicorn app.main:app --host 0.0.0.0 --port 8080 --reload
