#!/bin/bash
# CI test script for slskdN TrueNAS SCALE Helm chart
# This script validates the chart structure and performs basic tests

set -e

CHART_DIR="$(dirname "$0")"
CHART_NAME="slskdn"

echo "🔍 Validating Helm chart structure..."

# Check required files exist
required_files=(
    "Chart.yaml"
    "values.yaml"
    "templates/deployment.yaml"
    "templates/service.yaml"
    "README.md"
)

for file in "${required_files[@]}"; do
    if [[ ! -f "$CHART_DIR/$file" ]]; then
        echo "❌ Missing required file: $file"
        exit 1
    fi
done

echo "✅ Required files present"

# Validate Chart.yaml
echo "🔍 Validating Chart.yaml..."
if ! yq eval '.name' "$CHART_DIR/Chart.yaml" > /dev/null; then
    echo "❌ Invalid Chart.yaml"
    exit 1
fi

# Validate values.yaml
echo "🔍 Validating values.yaml..."
if ! yq eval '.image.repository' "$CHART_DIR/values.yaml" > /dev/null; then
    echo "❌ Invalid values.yaml"
    exit 1
fi

# Check for helm if available
if command -v helm &> /dev/null; then
    echo "🔍 Running Helm lint..."
    if helm lint "$CHART_DIR"; then
        echo "✅ Helm lint passed"
    else
        echo "❌ Helm lint failed"
        exit 1
    fi

    echo "🔍 Running Helm template..."
    if helm template test "$CHART_DIR" > /dev/null; then
        echo "✅ Helm template passed"
    else
        echo "❌ Helm template failed"
        exit 1
    fi
else
    echo "⚠️ Helm not available, skipping advanced validation"
fi

echo "🎉 Chart validation complete!"
echo ""
echo "To install this chart on TrueNAS SCALE:"
echo "1. Copy the chart directory to your TrueNAS system"
echo "2. Use the TrueNAS SCALE Apps interface to install from directory"
echo "3. Or use helm: helm install slskdn ./charts/slskdn"

