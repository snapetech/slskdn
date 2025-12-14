#!/bin/bash
# Test script for slskdN Homebrew formula
# This script validates the formula syntax and basic functionality

set -e

FORMULA_FILE="$(dirname "$0")/slskdn.rb"

echo "🔍 Testing Homebrew formula..."

# Check if formula file exists
if [[ ! -f "$FORMULA_FILE" ]]; then
    echo "❌ Formula file not found: $FORMULA_FILE"
    exit 1
fi

# Basic Ruby syntax check
echo "🔍 Checking Ruby syntax..."
if ! ruby -c "$FORMULA_FILE"; then
    echo "❌ Ruby syntax error in formula"
    exit 1
fi

# Check for brew command
if ! command -v brew &> /dev/null; then
    echo "⚠️ Homebrew not available, skipping advanced tests"
    echo "✅ Basic validation passed!"
    exit 0
fi

# Test formula with brew
echo "🔍 Testing formula with brew..."

# Check if formula can be parsed
if ! brew style "$FORMULA_FILE"; then
    echo "❌ Formula style check failed"
    exit 1
fi

# Audit formula
if ! brew audit "$FORMULA_FILE"; then
    echo "❌ Formula audit failed"
    exit 1
fi

# Test formula installation (dry run)
echo "🔍 Testing formula installation (dry run)..."
if brew install --dry-run "$FORMULA_FILE"; then
    echo "✅ Formula installation test passed"
else
    echo "❌ Formula installation test failed"
    exit 1
fi

echo "🎉 Formula validation complete!"
echo ""
echo "Formula is ready for submission to homebrew-core"
echo ""
echo "To submit to Homebrew:"
echo "1. Fork https://github.com/Homebrew/homebrew-core"
echo "2. Add slskdn.rb to Formula/"
echo "3. Update URLs and SHA256 hashes with real release"
echo "4. Test: brew install --build-from-source Formula/slskdn.rb"
echo "5. Submit pull request"


