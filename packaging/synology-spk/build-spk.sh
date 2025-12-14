#!/bin/bash
# Build script for slskdN Synology SPK package

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUILD_DIR="$SCRIPT_DIR/build"
PACKAGE_DIR="$BUILD_DIR/package"
SPK_FILE="$BUILD_DIR/slskdn.spk"

echo "🔨 Building slskdN SPK package..."

# Clean previous build
if [[ -d "$BUILD_DIR" ]]; then
    echo "🧹 Cleaning previous build..."
    rm -rf "$BUILD_DIR"
fi

# Create build directories
mkdir -p "$PACKAGE_DIR"

# Copy package files (simulate .NET build output)
echo "📦 Copying package files..."
# In real build, this would copy from dotnet publish output
# For now, create placeholder structure
mkdir -p "$PACKAGE_DIR/bin"
mkdir -p "$PACKAGE_DIR/lib"

# Create placeholder slskd binary
cat > "$PACKAGE_DIR/bin/slskd" << 'EOF'
#!/bin/bash
# slskdN placeholder binary for SPK package
# In real build, this would be the actual .NET binary

echo "slskdN - Soulseek Network Client - Next Generation"
echo "This is a placeholder binary for the SPK package"
echo "Replace with actual .NET publish output"
echo ""
echo "Usage: $0 [options]"
echo ""
echo "Web UI will be available at: http://localhost:5030"
EOF

chmod +x "$PACKAGE_DIR/bin/slskd"

# Copy package metadata and scripts
echo "📋 Copying package metadata..."
cp "$SCRIPT_DIR/INFO" "$BUILD_DIR/"
cp -r "$SCRIPT_DIR/scripts" "$BUILD_DIR/"
cp -r "$SCRIPT_DIR/conf" "$BUILD_DIR/"
cp -r "$SCRIPT_DIR/ui" "$BUILD_DIR/" 2>/dev/null || true

# Create package.tgz
echo "📦 Creating package archive..."
cd "$PACKAGE_DIR"
tar czf "$BUILD_DIR/package.tgz" *

# Create SPK file
echo "📦 Creating SPK file..."
cd "$BUILD_DIR"
tar cf "$SPK_FILE" INFO package.tgz scripts conf ui 2>/dev/null || tar cf "$SPK_FILE" INFO package.tgz scripts conf

echo "✅ Build complete!"
echo ""
echo "SPK file created: $SPK_FILE"
echo ""
echo "To install on Synology:"
echo "1. Copy $SPK_FILE to your Synology"
echo "2. Package Center → Manual Install → Upload SPK"
echo "3. Follow installation wizard"
echo ""
echo "For production builds:"
echo "1. Build .NET application: dotnet publish -c Release -r linux-x64 --self-contained"
echo "2. Copy output to $PACKAGE_DIR/"
echo "3. Run this script again"
