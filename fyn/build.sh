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

# Build as a single self-contained executable
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o ./build

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
    echo "Build complete. Binary is located at: ./build/$PROJECT_NAME"
    echo "You can manually copy it to /usr/local/bin/fyn if needed."
    exit 0
fi

echo "Installing Fyntora..."
echo

# Check if /usr/local/bin exists
if [ ! -d "/usr/local/bin" ]; then
    echo "Creating /usr/local/bin directory..."
    sudo mkdir -p /usr/local/bin
fi

# Copy the single binary
sudo cp "./build/$PROJECT_NAME" /usr/local/bin/fyn
sudo chmod +x /usr/local/bin/fyn

echo
echo "✓ Installation complete!"
echo
echo "You can now use Fyntora by running:"
echo "  fyn s <package>  - Search for packages"
echo "  fyn i <package>  - Install packages"
echo
echo "To uninstall, run:"
echo "  sudo rm /usr/local/bin/fyn"
echo