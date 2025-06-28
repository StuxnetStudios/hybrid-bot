# PowerShell script to publish Hybrid Bot to GitHub
# Replace YOUR_USERNAME with your actual GitHub username
# Replace REPOSITORY_NAME with your chosen repository name (e.g., hybrid-bot)

param(
    [Parameter(Mandatory=$true)]
    [string]$GitHubUsername,
    
    [Parameter(Mandatory=$false)]
    [string]$RepositoryName = "hybrid-bot"
)

Write-Host "🚀 Publishing Hybrid Bot to GitHub..." -ForegroundColor Green
Write-Host ""

# Check if we're in the right directory
if (!(Test-Path "HybridBot.csproj")) {
    Write-Host "❌ Error: Please run this script from the project root directory" -ForegroundColor Red
    exit 1
}

# Verify git is installed
try {
    git --version | Out-Null
} catch {
    Write-Host "❌ Error: Git is not installed or not in PATH" -ForegroundColor Red
    exit 1
}

# Check if repository exists locally
if (!(Test-Path ".git")) {
    Write-Host "❌ Error: Not a git repository. Please run 'git init' first." -ForegroundColor Red
    exit 1
}

# Set up the GitHub remote
$remoteUrl = "https://github.com/$GitHubUsername/$RepositoryName.git"
Write-Host "🔗 Adding GitHub remote: $remoteUrl" -ForegroundColor Yellow

try {
    git remote add origin $remoteUrl
    Write-Host "✅ Remote added successfully" -ForegroundColor Green
} catch {
    Write-Host "⚠️  Remote might already exist, trying to set URL..." -ForegroundColor Yellow
    git remote set-url origin $remoteUrl
}

# Verify remote
Write-Host "🔍 Verifying remote configuration..." -ForegroundColor Yellow
git remote -v

# Ensure we're on main branch
Write-Host "🌿 Setting up main branch..." -ForegroundColor Yellow
git branch -M main

# Show current status
Write-Host "📊 Current repository status:" -ForegroundColor Yellow
git status

# Push to GitHub
Write-Host "⬆️  Pushing to GitHub..." -ForegroundColor Green
Write-Host "Note: You may be prompted for your GitHub credentials" -ForegroundColor Cyan

try {
    git push -u origin main
    Write-Host ""
    Write-Host "🎉 Successfully published to GitHub!" -ForegroundColor Green
    Write-Host "🌐 Repository URL: https://github.com/$GitHubUsername/$RepositoryName" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "🔗 Next steps:" -ForegroundColor Yellow
    Write-Host "1. Visit your repository on GitHub"
    Write-Host "2. Enable GitHub Actions (if not already enabled)"
    Write-Host "3. Open the repository in VS Code with GitHub Copilot"
    Write-Host "4. Start extending your bot with new roles!"
} catch {
    Write-Host ""
    Write-Host "❌ Error pushing to GitHub. This might be due to:" -ForegroundColor Red
    Write-Host "   - Authentication issues (check your GitHub credentials)"
    Write-Host "   - Repository doesn't exist on GitHub"
    Write-Host "   - Network connectivity issues"
    Write-Host ""
    Write-Host "💡 Manual commands to try:" -ForegroundColor Yellow
    Write-Host "   git remote -v"
    Write-Host "   git push -u origin main"
}
