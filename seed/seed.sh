#!/bin/bash

# MongoDB Seed Script Wrapper
# Makkelijk uitvoerbaar script voor Linux omgevingen

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

echo "🌱 Chatfish MongoDB Seed Script"
echo "================================"
echo ""

# Check of Node.js geïnstalleerd is
if ! command -v node &> /dev/null; then
    echo "❌ Node.js is niet geïnstalleerd. Installeer Node.js eerst."
    echo "   Ubuntu/Debian: sudo apt-get install nodejs npm"
    echo "   Arch Linux: sudo pacman -S nodejs npm"
    exit 1
fi

# Check of npm geïnstalleerd is
if ! command -v npm &> /dev/null; then
    echo "❌ npm is niet geïnstalleerd. Installeer npm eerst."
    exit 1
fi

echo "✅ Node.js versie: $(node --version)"
echo "✅ npm versie: $(npm --version)"
echo ""

# Installeer dependencies als node_modules niet bestaat
if [ ! -d "node_modules" ]; then
    echo "📦 Dependencies installeren..."
    npm install
    echo ""
fi

# Haal connection string en database naam op uit argumenten of environment variabelen
CONNECTION_STRING="${1:-${MONGODB_CONNECTION_STRING:-mongodb://localhost:27017}}"
DATABASE_NAME="${2:-${MONGODB_DATABASE_NAME:-ChatfishDbtest}}"
MINIO_ENDPOINT="${3:-${MINIO_ENDPOINT:-minio-api-wpp-team-10.apps.okd.ucll.cloud}}"
MINIO_AK="${4:-${MINIO_AK:-minioadmin}}"
MINIO_SK="${5:-${MINIO_SK:-minioadmin}}"

echo "🔌 Connection string: $CONNECTION_STRING"
echo "📦 Database naam: $DATABASE_NAME"
echo "🪣 MinIO endpoint: $MINIO_ENDPOINT"
echo ""

# Vraag bevestiging
read -p "⚠️  Dit zal alle bestaande data in de database verwijderen. Doorgaan? (j/N): " -n 1 -r
echo ""
if [[ ! $REPLY =~ ^[Jj]$ ]]; then
    echo "❌ Geannuleerd."
    exit 1
fi

# Voer seed script uit
echo ""
node seed-mongodb.js "$CONNECTION_STRING" "$DATABASE_NAME" "$MINIO_ENDPOINT" "$MINIO_AK" "$MINIO_SK"
