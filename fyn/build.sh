#!/bin/bash

# Fyntora Builder Script
# Builds and optionally installs Fyntora AUR helper

set -e

echo "=================================="
echo "   Fyntora AUR Helper Builder"
echo "=================================="
echo

# Check if dotnet is installed
if ! command -v dotnet &> /dev/null; then
    echo "Error: .NET SDK is not installed."
    echo "Please install .NET SDK first:"
    echo "  sudo pacman -S dotnet-sdk"
    exit 1
fi

echo "Building Fyntora..."
echo

# Get the project file name
PROJECT_FILE=$(ls *.csproj 2>/dev/null | head -n 1)

if [ -z "$PROJECT_FILE" ]; then
    echo "Error: No .csproj file found in current directory!"
    exit 1
fi

PROJECT_NAME=$(basename "$PROJECT_FILE" .csproj)
echo "Found project: $PROJECT_NAME"
echo

# Build the project
dotnet publish -c Release -r linux-x64 --self-contained false -o ./build

if [ $? -ne 0 ]; then
    echo
    echo "Error: Build failed!"
    exit 1
fi

echo
echo "✓ Build successful!"
echo

# Ask if user wants to install
read -p "Do you want to install Fyntora to your system? [Y/n] " -n 1 -r
echo

if [[ ! $REPLY =~ ^[Yy]$ ]] && [[ ! -z $REPLY ]]; then
    echo "Build complete. Binary is located at: ./build/fyn"
    echo "You can manually copy it to /usr/local/bin/ if needed."
    exit 0
fi

echo "Installing Fyntora..."
echo

# Check if /usr/local/bin exists
if [ ! -d "/usr/local/bin" ]; then
    echo "Creating /usr/local/bin directory..."
    sudo mkdir -p /usr/local/bin
fi

# Copy the binary
sudo cp ./build/fyn /usr/local/bin/fyn
sudo chmod +x /usr/local/bin/fyn

# Copy runtime dependencies
if [ -d "./build" ]; then
    sudo mkdir -p /usr/local/lib/fyntora
    sudo cp -r ./build/* /usr/local/lib/fyntora/
    
    # Find the actual DLL name
    DLL_NAME=$(ls ./build/*.dll 2>/dev/null | head -n 1 | xargs -n 1 basename)
    
    if [ -z "$DLL_NAME" ]; then
        echo "Error: Could not find compiled DLL!"
        exit 1
    fi
    
    echo "Using DLL: $DLL_NAME"
    
    # Create wrapper script
    sudo tee /usr/local/bin/fyn > /dev/null << EOF
#!/bin/bash
exec dotnet /usr/local/lib/fyntora/$DLL_NAME "\$@"
EOF
    
    sudo chmod +x /usr/local/bin/fyn
fi

echo
echo "✓ Installation complete!"
echo
echo "You can now use Fyntora by running:"
echo "  fyn s <package>  - Search for packages"
echo "  fyn i <package>  - Install packages"
echo
echo "To uninstall, run:"
echo "  sudo rm /usr/local/bin/fyn"
echo "  sudo rm -rf /usr/local/lib/fyntora"
echo