#!/bin/bash
# Build script for slskdN Flatpak package
# This script builds and tests the Flatpak package locally

set -e

MANIFEST_FILE="io.github.slskd.slskdn.yml"
BUILD_DIR="build-dir"
BUNDLE_FILE="slskdn.flatpak"

echo "🔍 Building slskdN Flatpak package..."

# Check prerequisites
check_prerequisites() {
    local missing_deps=()

    if ! command -v flatpak-builder &> /dev/null; then
        missing_deps+=("flatpak-builder (Ubuntu/Debian: sudo apt install flatpak-builder)")
    fi

    if ! command -v flatpak &> /dev/null; then
        missing_deps+=("flatpak (Ubuntu/Debian: sudo apt install flatpak)")
    fi

    if [[ ${#missing_deps[@]} -gt 0 ]]; then
        echo "❌ Missing dependencies:"
        for dep in "${missing_deps[@]}"; do
            echo "  - $dep"
        done
        exit 1
    fi
}

# Convert SVG icon to PNG if needed
prepare_icons() {
    if command -v convert &> /dev/null; then
        echo "🎨 Converting SVG icon to PNG..."
        if [[ -f "slskdn.svg" && (! -f "slskdn.png" || slskdn.svg -nt slskdn.png) ]]; then
            convert slskdn.svg -background transparent -size 512x512 slskdn.png
            echo "✅ Icon converted"
        fi
    elif command -v inkscape &> /dev/null; then
        echo "🎨 Converting SVG icon to PNG using inkscape..."
        if [[ -f "slskdn.svg" && (! -f "slskdn.png" || slskdn.svg -nt slskdn.png) ]]; then
            inkscape -w 512 -h 512 slskdn.svg -o slskdn.png
            echo "✅ Icon converted"
        fi
    else
        echo "⚠️ Warning: ImageMagick or Inkscape not found. Using placeholder icon."
        if [[ ! -f "slskdn.png" ]]; then
            echo "❌ slskdn.png not found. Please convert slskdn.svg to PNG format."
            echo "   Using ImageMagick: convert slskdn.svg -background transparent -size 512x512 slskdn.png"
            echo "   Using Inkscape: inkscape -w 512 -h 512 slskdn.svg -o slskdn.png"
            exit 1
        fi
    fi
}

# Validate manifest
validate_manifest() {
    echo "🔍 Validating manifest..."

    # Check for placeholder values
    if grep -q "PLACEHOLDER_SHA256" "$MANIFEST_FILE"; then
        echo "⚠️ Warning: Manifest contains placeholder SHA256 values"
        echo "   Update these before Flathub submission"
    fi

    # Check for placeholder URLs
    if grep -q "https://github.com/slskd/slskd/releases/download" "$MANIFEST_FILE"; then
        echo "⚠️ Warning: Manifest contains placeholder download URLs"
        echo "   Update these with real release URLs before submission"
    fi
}

# Main build process
main() {
    check_prerequisites
    prepare_icons
    validate_manifest

    # Clean previous build
    if [[ -d "$BUILD_DIR" ]]; then
        echo "🧹 Cleaning previous build..."
        rm -rf "$BUILD_DIR"
    fi

    # Build the package
    echo "🏗️ Building Flatpak package..."
    flatpak-builder --force-clean "$BUILD_DIR" "$MANIFEST_FILE"

    # Validate the build
    echo "✅ Validating build..."
    if [[ ! -d "$BUILD_DIR" ]]; then
        echo "❌ Build directory not created"
        exit 1
    fi

    # Test the package (run a simple command)
    echo "🧪 Testing package..."
    if flatpak-builder --run "$BUILD_DIR" "$MANIFEST_FILE" echo "Flatpak test successful" 2>/dev/null; then
        echo "✅ Package test passed"
    else
        echo "❌ Package test failed - check build logs"
        exit 1
    fi

    # Create bundle (optional)
    echo "📦 Creating bundle..."
    if flatpak build-bundle "$BUILD_DIR" "$BUNDLE_FILE" io.github.slskd.slskdn 2>/dev/null; then
        echo "✅ Bundle created: $BUNDLE_FILE"
    else
        echo "⚠️ Bundle creation failed (optional)"
    fi

    echo ""
    echo "🎉 Build complete!"
    echo ""
    echo "Installation options:"
    echo "1. Local install: flatpak --user install $BUNDLE_FILE"
    echo "2. Test run: flatpak run io.github.slskd.slskdn"
    echo "3. Bundle file: $BUNDLE_FILE"
    echo ""
    echo "For Flathub submission:"
    echo "1. Update manifest URLs and hashes with real release"
    echo "2. Test thoroughly on multiple distributions"
    echo "3. Submit to https://github.com/flathub/flathub"
    echo ""
    echo "VPN Features Testing:"
    echo "1. Enable podcore and mesh features in config"
    echo "2. Test VPN tunnel creation"
    echo "3. Verify anonymous transports work"
}

# Run main function
main "$@"
