#!/bin/bash
# Test script for provider validation endpoints
# Usage: ./test-provider-validation.sh

API_BASE_URL="${API_BASE_URL:-http://localhost:5005}"

echo "=== Provider API Key Validation Tests ==="
echo "API Base URL: $API_BASE_URL"
echo ""

# Test 1: Get provider status
echo "Test 1: GET /api/providers/status"
curl -X GET "$API_BASE_URL/api/providers/status" \
  -H "Content-Type: application/json" \
  -w "\nHTTP Status: %{http_code}\n" \
  -s | jq '.' || echo "Response (raw): $(curl -s -X GET $API_BASE_URL/api/providers/status)"
echo ""
echo "---"
echo ""

# Test 2: Validate OpenAI key (with invalid key)
echo "Test 2: POST /api/providers/openai/validate (invalid key)"
curl -X POST "$API_BASE_URL/api/providers/openai/validate" \
  -H "Content-Type: application/json" \
  -d '{
    "apiKey": "sk-invalid-test-key"
  }' \
  -w "\nHTTP Status: %{http_code}\n" \
  -s | jq '.' || echo "Response (raw): $(curl -s -X POST $API_BASE_URL/api/providers/openai/validate -H 'Content-Type: application/json' -d '{\"apiKey\": \"sk-invalid-test-key\"}')"
echo ""
echo "---"
echo ""

# Test 3: Validate ElevenLabs key (with invalid key)
echo "Test 3: POST /api/providers/elevenlabs/validate (invalid key)"
curl -X POST "$API_BASE_URL/api/providers/elevenlabs/validate" \
  -H "Content-Type: application/json" \
  -d '{
    "apiKey": "invalid-elevenlabs-key"
  }' \
  -w "\nHTTP Status: %{http_code}\n" \
  -s | jq '.' || echo "Response (raw): $(curl -s -X POST $API_BASE_URL/api/providers/elevenlabs/validate -H 'Content-Type: application/json' -d '{\"apiKey\": \"invalid-elevenlabs-key\"}')"
echo ""
echo "---"
echo ""

# Test 4: Validate PlayHT key (with invalid key)
echo "Test 4: POST /api/providers/playht/validate (invalid key)"
curl -X POST "$API_BASE_URL/api/providers/playht/validate" \
  -H "Content-Type: application/json" \
  -d '{
    "apiKey": "invalid-playht-key"
  }' \
  -w "\nHTTP Status: %{http_code}\n" \
  -s | jq '.' || echo "Response (raw): $(curl -s -X POST $API_BASE_URL/api/providers/playht/validate -H 'Content-Type: application/json' -d '{\"apiKey\": \"invalid-playht-key\"}')"
echo ""
echo "---"
echo ""

# Test 5: Validate with empty key (should return 400)
echo "Test 5: POST /api/providers/openai/validate (empty key)"
curl -X POST "$API_BASE_URL/api/providers/openai/validate" \
  -H "Content-Type: application/json" \
  -d '{
    "apiKey": ""
  }' \
  -w "\nHTTP Status: %{http_code}\n" \
  -s | jq '.' || echo "Response (raw): $(curl -s -X POST $API_BASE_URL/api/providers/openai/validate -H 'Content-Type: application/json' -d '{\"apiKey\": \"\"}')"
echo ""
echo "---"
echo ""

echo "=== Tests Complete ==="
echo ""
echo "Note: These tests use invalid API keys to demonstrate the validation."
echo "To test with real keys, set them as environment variables:"
echo "  export OPENAI_KEY='sk-...'"
echo "  export ELEVENLABS_KEY='...'"
echo "  export PLAYHT_KEY='...'"
echo ""
echo "Then modify this script to use those variables."
