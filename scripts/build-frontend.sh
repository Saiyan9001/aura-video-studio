#!/bin/bash
# Automated Frontend Build and Deployment Script
# This script ensures the frontend is built and deployed to the backend wwwroot directory

set -e  # Exit on error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Script directory and workspace root
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
WORKSPACE_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
FRONTEND_DIR="$WORKSPACE_ROOT/Aura.Web"
BACKEND_DIR="$WORKSPACE_ROOT/Aura.Api"
WWWROOT_DIR="$BACKEND_DIR/wwwroot"

echo -e "${BLUE}========================================${NC}"
echo -e "${BLUE}  Aura Video Studio - Frontend Build${NC}"
echo -e "${BLUE}========================================${NC}"
echo ""

# Check if frontend directory exists
if [ ! -d "$FRONTEND_DIR" ]; then
    echo -e "${RED}ERROR: Frontend directory not found at: $FRONTEND_DIR${NC}"
    exit 1
fi

# Check if Node.js is installed
if ! command -v node &> /dev/null; then
    echo -e "${RED}ERROR: Node.js is not installed. Please install Node.js 20+ to continue.${NC}"
    exit 1
fi

# Check if npm is installed
if ! command -v npm &> /dev/null; then
    echo -e "${RED}ERROR: npm is not installed. Please install npm to continue.${NC}"
    exit 1
fi

echo -e "${GREEN}✓${NC} Node.js version: $(node --version)"
echo -e "${GREEN}✓${NC} npm version: $(npm --version)"
echo ""

# Navigate to frontend directory
cd "$FRONTEND_DIR"

# Install dependencies if node_modules doesn't exist
if [ ! -d "node_modules" ]; then
    echo -e "${YELLOW}→${NC} Installing frontend dependencies..."
    npm install
    echo -e "${GREEN}✓${NC} Dependencies installed"
    echo ""
else
    echo -e "${GREEN}✓${NC} Dependencies already installed"
    echo ""
fi

# Build frontend
echo -e "${YELLOW}→${NC} Building frontend for production..."
npm run build:prod
echo -e "${GREEN}✓${NC} Frontend build completed"
echo ""

# Check if dist directory was created
if [ ! -d "$FRONTEND_DIR/dist" ]; then
    echo -e "${RED}ERROR: Build failed - dist directory not found${NC}"
    exit 1
fi

# Check if index.html exists in dist
if [ ! -f "$FRONTEND_DIR/dist/index.html" ]; then
    echo -e "${RED}ERROR: Build failed - index.html not found in dist${NC}"
    exit 1
fi

echo -e "${GREEN}✓${NC} Build output verified"
echo ""

# Create or clean wwwroot directory
echo -e "${YELLOW}→${NC} Preparing backend wwwroot directory..."
if [ -d "$WWWROOT_DIR" ]; then
    echo -e "${YELLOW}  Cleaning existing wwwroot...${NC}"
    rm -rf "$WWWROOT_DIR"/*
else
    mkdir -p "$WWWROOT_DIR"
fi
echo -e "${GREEN}✓${NC} wwwroot directory ready"
echo ""

# Copy built files to wwwroot
echo -e "${YELLOW}→${NC} Deploying frontend to backend..."
cp -r "$FRONTEND_DIR/dist"/* "$WWWROOT_DIR/"
echo -e "${GREEN}✓${NC} Frontend deployed to: $WWWROOT_DIR"
echo ""

# Verify deployment
echo -e "${YELLOW}→${NC} Verifying deployment..."
REQUIRED_FILES=("index.html" "assets")
for file in "${REQUIRED_FILES[@]}"; do
    if [ ! -e "$WWWROOT_DIR/$file" ]; then
        echo -e "${RED}ERROR: Required file/directory missing: $file${NC}"
        exit 1
    fi
    echo -e "${GREEN}✓${NC} $file"
done

# Count JavaScript bundles
JS_COUNT=$(find "$WWWROOT_DIR/assets" -name "*.js" -type f | wc -l)
echo -e "${GREEN}✓${NC} $JS_COUNT JavaScript bundles deployed"
echo ""

# Summary
echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}  Frontend Build & Deployment Complete${NC}"
echo -e "${GREEN}========================================${NC}"
echo ""
echo -e "Frontend directory: ${BLUE}$FRONTEND_DIR${NC}"
echo -e "Backend wwwroot:    ${BLUE}$WWWROOT_DIR${NC}"
echo -e "Build output:       ${BLUE}$FRONTEND_DIR/dist${NC}"
echo ""
echo -e "${YELLOW}Next steps:${NC}"
echo -e "1. Start the backend API: ${BLUE}cd $BACKEND_DIR && dotnet run${NC}"
echo -e "2. Access the application at: ${BLUE}http://127.0.0.1:5005/${NC}"
echo -e "3. Check health endpoint: ${BLUE}http://127.0.0.1:5005/healthz${NC}"
echo ""
