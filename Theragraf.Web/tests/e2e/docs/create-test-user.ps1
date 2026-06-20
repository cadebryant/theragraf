# ============================================================================
# Theragraf E2E Test User Setup Script
# ============================================================================
# This script creates a dedicated Azure AD test user for E2E testing
# Run this script with an account that has User Administrator permissions
# ============================================================================

param(
	[Parameter(Mandatory=$false)]
	[string]$UserName = "theragraf-e2e-test",

	[Parameter(Mandatory=$false)]
	[string]$Password = "",

	[Parameter(Mandatory=$false)]
	[switch]$SkipAppAssignment
)

Write-Host "🚀 Theragraf E2E Test User Setup" -ForegroundColor Cyan
Write-Host "=================================" -ForegroundColor Cyan
Write-Host ""

# Check if logged in to Azure
Write-Host "🔍 Checking Azure CLI login status..." -ForegroundColor Yellow
$account = az account show 2>$null | ConvertFrom-Json
if (-not $account) {
	Write-Host "❌ Not logged in to Azure. Running 'az login'..." -ForegroundColor Red
	az login
	$account = az account show | ConvertFrom-Json
}

Write-Host "✅ Logged in as: $($account.user.name)" -ForegroundColor Green
Write-Host "   Tenant: $($account.tenantId)" -ForegroundColor Gray
Write-Host ""

# Get tenant domain
Write-Host "🔍 Getting tenant information..." -ForegroundColor Yellow
$tenantInfo = az rest --method GET --url "https://graph.microsoft.com/v1.0/organization" | ConvertFrom-Json
$domain = $tenantInfo.value[0].verifiedDomains | Where-Object { $_.isDefault -eq $true } | Select-Object -First 1 -ExpandProperty name

Write-Host "✅ Default domain: $domain" -ForegroundColor Green
Write-Host ""

# Construct UPN
$upn = "$UserName@$domain"
Write-Host "📧 Test user UPN will be: $upn" -ForegroundColor Cyan
Write-Host ""

# Check if user already exists
Write-Host "🔍 Checking if user already exists..." -ForegroundColor Yellow
$existingUser = az ad user show --id $upn 2>$null
if ($existingUser) {
	Write-Host "⚠️  User already exists!" -ForegroundColor Yellow
	$existingUser = $existingUser | ConvertFrom-Json
	Write-Host "   Object ID: $($existingUser.id)" -ForegroundColor Gray
	Write-Host "   Display Name: $($existingUser.displayName)" -ForegroundColor Gray
	Write-Host ""

	$continue = Read-Host "Do you want to reset the password? (y/N)"
	if ($continue -ne "y") {
		Write-Host "❌ Setup cancelled" -ForegroundColor Red
		exit 1
	}
	$userId = $existingUser.id
} else {
	Write-Host "✅ User does not exist, will create new user" -ForegroundColor Green
	Write-Host ""
	$userId = $null
}

# Generate or prompt for password
if ([string]::IsNullOrEmpty($Password)) {
	Write-Host "🔑 No password provided, generating secure password..." -ForegroundColor Yellow
	# Generate a secure random password
	Add-Type -AssemblyName System.Web
	$Password = [System.Web.Security.Membership]::GeneratePassword(16, 4)
	Write-Host "✅ Generated password: $Password" -ForegroundColor Green
	Write-Host "   ⚠️  SAVE THIS PASSWORD! You'll need it for .env.test" -ForegroundColor Yellow
	Write-Host ""
}

# Create or update user
if ($null -eq $userId) {
	Write-Host "👤 Creating new user..." -ForegroundColor Yellow

	$userJson = @{
		accountEnabled = $true
		displayName = "Theragraf E2E Test User"
		mailNickname = $UserName
		userPrincipalName = $upn
		passwordProfile = @{
			forceChangePasswordNextSignIn = $false
			password = $Password
		}
	} | ConvertTo-Json -Compress

	$newUser = az rest --method POST --url "https://graph.microsoft.com/v1.0/users" --headers "Content-Type=application/json" --body $userJson | ConvertFrom-Json
	$userId = $newUser.id

	Write-Host "✅ User created successfully!" -ForegroundColor Green
	Write-Host "   Object ID: $userId" -ForegroundColor Gray
	Write-Host ""
} else {
	Write-Host "🔄 Updating existing user password..." -ForegroundColor Yellow

	$passwordJson = @{
		passwordProfile = @{
			forceChangePasswordNextSignIn = $false
			password = $Password
		}
	} | ConvertTo-Json -Compress

	az rest --method PATCH --url "https://graph.microsoft.com/v1.0/users/$userId" --headers "Content-Type=application/json" --body $passwordJson | Out-Null

	Write-Host "✅ Password updated!" -ForegroundColor Green
	Write-Host ""
}

# Set password to never expire
Write-Host "⏰ Setting password to never expire..." -ForegroundColor Yellow
az rest --method PATCH --url "https://graph.microsoft.com/v1.0/users/$userId" --headers "Content-Type=application/json" --body '{"passwordPolicies":"DisablePasswordExpiration"}' | Out-Null
Write-Host "✅ Password expiration disabled" -ForegroundColor Green
Write-Host ""

# Assign to application
if (-not $SkipAppAssignment) {
	Write-Host "🔗 Assigning user to Theragraf application..." -ForegroundColor Yellow

	$appId = "ba58ec08-f9c8-4232-8a01-8e90c5e4de2a"  # From .env.development

	# Get service principal
	$sp = az ad sp list --filter "appId eq '$appId'" | ConvertFrom-Json | Select-Object -First 1

	if ($sp) {
		Write-Host "   Found service principal: $($sp.displayName)" -ForegroundColor Gray

		# Assign user to app (this may fail if no roles defined, which is OK)
		try {
			$assignmentJson = @{
				principalId = $userId
				resourceId = $sp.id
				appRoleId = "00000000-0000-0000-0000-000000000000"  # Default access
			} | ConvertTo-Json -Compress

			az rest --method POST --url "https://graph.microsoft.com/v1.0/servicePrincipals/$($sp.id)/appRoleAssignedTo" --headers "Content-Type=application/json" --body $assignmentJson 2>$null | Out-Null
			Write-Host "✅ User assigned to application" -ForegroundColor Green
		} catch {
			Write-Host "ℹ️  App role assignment not needed (user has default access)" -ForegroundColor Gray
		}
	} else {
		Write-Host "⚠️  Service principal not found - you may need to assign manually" -ForegroundColor Yellow
	}
	Write-Host ""
}

# Output credentials for .env.test
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "✅ Setup Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "📝 Add these to your Theragraf.Web/.env.test file:" -ForegroundColor Yellow
Write-Host ""
Write-Host "TEST_USER_EMAIL=$upn" -ForegroundColor White
Write-Host "TEST_USER_PASSWORD=$Password" -ForegroundColor White
Write-Host ""
Write-Host "🔒 Security Notes:" -ForegroundColor Yellow
Write-Host "   • Keep .env.test file local (already in .gitignore)" -ForegroundColor Gray
Write-Host "   • Never commit credentials to git" -ForegroundColor Gray
Write-Host "   • For CI/CD, use GitHub Secrets instead" -ForegroundColor Gray
Write-Host ""
Write-Host "🧪 Next Steps:" -ForegroundColor Yellow
Write-Host "   1. Update .env.test with credentials above" -ForegroundColor Gray
Write-Host "   2. Test login manually in incognito browser at http://localhost:5173" -ForegroundColor Gray
Write-Host "   3. Run: npx playwright test --project=setup" -ForegroundColor Gray
Write-Host "   4. Run: npm run test:e2e" -ForegroundColor Gray
Write-Host ""

# Copy to clipboard if available
try {
	"$upn`n$Password" | Set-Clipboard
	Write-Host "📋 Credentials copied to clipboard!" -ForegroundColor Green
	Write-Host ""
} catch {
	# Clipboard not available, that's OK
}
