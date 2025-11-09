#!/bin/bash
# Manual test script for configuration persistence
# Tests that configuration survives application restart

set -e

echo "=== Configuration Persistence Test ==="
echo ""

API_URL="http://localhost:5005/api/configuration"

echo "Step 1: Check if API is running..."
if ! curl -s -f "$API_URL/health/database" > /dev/null 2>&1; then
    echo "❌ API is not running. Please start the application first:"
    echo "   cd Aura.Api && dotnet run"
    exit 1
fi
echo "✅ API is running"
echo ""

echo "Step 2: Check database health..."
HEALTH=$(curl -s "$API_URL/health/database")
echo "$HEALTH" | jq '.'
if echo "$HEALTH" | jq -e '.healthy == true' > /dev/null; then
    echo "✅ Database is healthy"
else
    echo "❌ Database health check failed"
    exit 1
fi
echo ""

echo "Step 3: Set test configuration..."
curl -s -X POST "$API_URL/TestKey" \
    -H "Content-Type: application/json" \
    -d '{"value":"TestValue123","category":"Test","description":"Test configuration"}' \
    | jq '.'
echo "✅ Configuration set"
echo ""

echo "Step 4: Retrieve configuration..."
RETRIEVED=$(curl -s "$API_URL/TestKey")
echo "$RETRIEVED" | jq '.'
if echo "$RETRIEVED" | jq -e '.value == "TestValue123"' > /dev/null; then
    echo "✅ Configuration retrieved successfully"
else
    echo "❌ Configuration retrieval failed"
    exit 1
fi
echo ""

echo "Step 5: Update configuration..."
curl -s -X POST "$API_URL/TestKey" \
    -H "Content-Type: application/json" \
    -d '{"value":"UpdatedValue456","category":"Test","description":"Updated configuration"}' \
    | jq '.'
echo "✅ Configuration updated"
echo ""

echo "Step 6: Verify update..."
UPDATED=$(curl -s "$API_URL/TestKey")
echo "$UPDATED" | jq '.'
if echo "$UPDATED" | jq -e '.value == "UpdatedValue456"' > /dev/null; then
    echo "✅ Configuration update verified"
else
    echo "❌ Configuration update failed"
    exit 1
fi
echo ""

echo "Step 7: Test bulk set..."
curl -s -X POST "$API_URL/bulk" \
    -H "Content-Type: application/json" \
    -d '{
        "configurations": [
            {"key":"Bulk1","value":"BulkValue1","category":"BulkTest"},
            {"key":"Bulk2","value":"BulkValue2","category":"BulkTest"},
            {"key":"Bulk3","value":"BulkValue3","category":"BulkTest"}
        ]
    }' | jq '.'
echo "✅ Bulk configurations set"
echo ""

echo "Step 8: Test category retrieval..."
CATEGORY=$(curl -s "$API_URL/category/BulkTest")
echo "$CATEGORY" | jq '.'
COUNT=$(echo "$CATEGORY" | jq '.count')
if [ "$COUNT" -eq 3 ]; then
    echo "✅ Category retrieval works ($COUNT items)"
else
    echo "❌ Category retrieval failed (expected 3, got $COUNT)"
    exit 1
fi
echo ""

echo "Step 9: Dump all configurations..."
curl -s "$API_URL/debug/dump" | jq '.totalCount, .activeCount'
echo "✅ Configuration dump successful"
echo ""

echo "Step 10: Clear cache..."
curl -s -X POST "$API_URL/cache/clear" | jq '.'
echo "✅ Cache cleared"
echo ""

echo "Step 11: Verify configuration still exists after cache clear..."
AFTER_CLEAR=$(curl -s "$API_URL/TestKey")
if echo "$AFTER_CLEAR" | jq -e '.value == "UpdatedValue456"' > /dev/null; then
    echo "✅ Configuration persists after cache clear"
else
    echo "❌ Configuration lost after cache clear"
    exit 1
fi
echo ""

echo "==================================="
echo "✅ All tests passed!"
echo ""
echo "To test persistence across restarts:"
echo "1. Note the current values"
echo "2. Stop the application"
echo "3. Restart the application"
echo "4. Run: curl $API_URL/TestKey"
echo "5. Verify the value is still 'UpdatedValue456'"
echo ""
echo "Cleanup: To delete test configurations, run:"
echo "  curl -X DELETE $API_URL/TestKey"
echo "  curl -X DELETE $API_URL/Bulk1"
echo "  curl -X DELETE $API_URL/Bulk2"
echo "  curl -X DELETE $API_URL/Bulk3"
