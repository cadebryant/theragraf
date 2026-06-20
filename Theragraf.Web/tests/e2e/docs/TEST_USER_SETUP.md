# Test User Setup Guide

This guide walks you through creating a dedicated test user account in Azure AD for E2E testing.

## Why a Dedicated Test User?

- **Isolation**: Test runs don't affect your personal account
- **Automation**: Password-based authentication (no passkey/MFA complications)
- **Security**: Uses minimal permissions needed for testing
- **CI/CD Ready**: Can be safely stored in GitHub Secrets

---

## Step 1: Create the Test User in Azure AD

### Option A: Using Azure Portal (Recommended for First Time)

1. **Open Azure Portal**
   - Navigate to https://portal.azure.com
   - Sign in with an account that has User Administrator rights

2. **Navigate to Microsoft Entra ID** (formerly Azure AD)
   - Search for "Microsoft Entra ID" in the top search bar
   - Or go directly to: https://portal.azure.com/#view/Microsoft_AAD_IAM/ActiveDirectoryMenuBlade

3. **Create New User**
   - Click **Users** in the left sidebar
   - Click **+ New user** → **Create new user**
   - Fill in the form:
	 ```
	 User principal name: theragraf-e2e-test@[yourdomain].onmicrosoft.com
	 Display name: Theragraf E2E Test User
	 Password: [Generate strong password - save this!]

	 Account enabled: ✅ Yes
	 ```
   - **IMPORTANT**: Uncheck "Require this user to change password at first sign-in"
   - Click **Review + create** → **Create**

4. **Save Credentials Immediately**
   ```
   Email: theragraf-e2e-test@[yourdomain].onmicrosoft.com
   Password: [the generated password]
   ```

### Option B: Using Azure CLI (Faster)

```powershell
# Login to Azure
az login

# Get your tenant domain
$domain = (az account show --query "tenantDisplayName" -o tsv)
echo "Your domain: $domain"

# Create test user
az ad user create --display-name "Theragraf E2E Test User" --user-principal-name "theragraf-e2e-test@[yourdomain].onmicrosoft.com" --password "[YourStrongPassword123!]" --force-change-password-next-sign-in false

# Get the user object ID
$testUserId = (az ad user show --id "theragraf-e2e-test@[yourdomain].onmicrosoft.com" --query "id" -o tsv)
echo "Test User ID: $testUserId"
```

---

## Step 2: Assign Application Permissions

The test user needs access to your Theragraf application.

### Find Your App Registration

```powershell
# List your app registrations
az ad app list --display-name "Theragraf" --query "[].{Name:displayName, AppId:appId, ObjectId:id}" -o table
```

Your `VITE_AZURE_AD_CLIENT_ID` from `.env.development` is: **ba58ec08-f9c8-4232-8a01-8e90c5e4de2a**

### Assign User to Application (Portal)

1. Go to **Microsoft Entra ID** → **Enterprise Applications**
2. Search for "Theragraf" (or use your app ID: `ba58ec08-f9c8-4232-8a01-8e90c5e4de2a`)
3. Click **Users and groups** in the left sidebar
4. Click **+ Add user/group**
5. Select your test user: `theragraf-e2e-test@[yourdomain].onmicrosoft.com`
6. Assign a role (use "User" or whatever default role your app has)
7. Click **Assign**

### Assign User to Application (CLI)

```powershell
# Get your service principal ID
$spId = (az ad sp list --filter "appId eq 'ba58ec08-f9c8-4232-8a01-8e90c5e4de2a'" --query "[0].id" -o tsv)

# Get test user ID
$userId = (az ad user show --id "theragraf-e2e-test@[yourdomain].onmicrosoft.com" --query "id" -o tsv)

# Assign user to app
az ad app owner add --id $spId --owner-object-id $userId
```

---

## Step 3: Disable MFA/Passkey for Test User (Critical!)

By default, Azure AD may require MFA or passkey for new users. We need to disable this for the test account.

### Using Conditional Access (Recommended)

1. **Create Exclusion Policy**
   - Go to **Microsoft Entra ID** → **Security** → **Conditional Access**
   - Find your existing MFA policy (or create a new one)
   - Under **Exclude**, add the test user: `theragraf-e2e-test@[yourdomain].onmicrosoft.com`

2. **Alternative: Per-User MFA Settings**
   - Go to **Microsoft Entra ID** → **Users** → **Per-user MFA**
   - Find your test user
   - Click **Disable** next to "Multi-Factor Auth Status"

**⚠️ Security Note**: This is why we use a *dedicated* test account with minimal permissions. Never disable MFA on real user accounts.

---

## Step 4: Configure Test Environment

Update your `Theragraf.Web/.env.test` file:

```env
# Test User Credentials (local development only - DO NOT COMMIT)
TEST_USER_EMAIL=theragraf-e2e-test@[yourdomain].onmicrosoft.com
TEST_USER_PASSWORD=[YourStrongPassword123!]

# Test data configuration
TEST_CLIENT_ID_PREFIX=e2e-test-client
TEST_SESSION_CLEANUP_ENABLED=true
```

---

## Step 5: Test the Login

### First Sign-In (Browser Test)

1. Open an **Incognito/Private** browser window
2. Navigate to http://localhost:5173
3. Sign in with the test user credentials
4. **Verify**:
   - ✅ Login succeeds without passkey prompt
   - ✅ Dashboard loads
   - ✅ Can create a session
   - ✅ Can view client profiles

If this works, your automation will work too!

### Run E2E Auth Setup

```powershell
# Navigate to web project
cd Theragraf.Web

# Run setup project to create auth state
npx playwright test --project=setup

# Verify auth file was created
Test-Path "tests\e2e\.auth\user.json"
# Should return: True
```

### Run Full E2E Suite

```powershell
npm run test:e2e
```

---

## Step 6: CI/CD Setup (GitHub Actions)

When you're ready to run tests in CI/CD:

1. **Add GitHub Secrets**
   - Go to your repo → **Settings** → **Secrets and variables** → **Actions**
   - Add secrets:
	 ```
	 TEST_USER_EMAIL=theragraf-e2e-test@[yourdomain].onmicrosoft.com
	 TEST_USER_PASSWORD=[YourStrongPassword123!]
	 ```

2. **Update Workflow** (future step - not needed yet since you're running locally)

---

## Troubleshooting

### "Sign-in was blocked" Error

**Cause**: User not assigned to the application or Conditional Access policy blocking sign-in.

**Fix**:
1. Verify user is assigned to app (Step 2)
2. Check Conditional Access policies (Step 3)
3. Review sign-in logs: **Microsoft Entra ID** → **Sign-in logs** → filter by test user

### "Password expired" Error

**Cause**: Password expiration policy applied to test user.

**Fix**:
```powershell
# Set password to never expire
az ad user update --id "theragraf-e2e-test@[yourdomain].onmicrosoft.com" --password-policies DisablePasswordExpiration
```

### "MFA Required" Error

**Cause**: MFA still enforced on test user.

**Fix**: Return to Step 3 and ensure test user is excluded from MFA policies.

### Auth File Not Created

**Cause**: Login succeeded but storage state wasn't saved.

**Debug**:
```powershell
# Run setup with debug output
$env:DEBUG="pw:api"; npx playwright test --project=setup --headed
```

---

## Security Best Practices

✅ **DO**:
- Use a dedicated test account (don't reuse real user accounts)
- Store credentials in `.env.test` (local) or GitHub Secrets (CI/CD)
- Exclude `.env.test` from git (already in `.gitignore`)
- Limit test user permissions to minimum needed
- Rotate test user password periodically
- Monitor test user sign-in logs for anomalies

❌ **DON'T**:
- Commit credentials to git
- Use your personal account for automation
- Disable MFA on production user accounts
- Give test user admin permissions
- Share test credentials publicly

---

## Quick Reference

### Your Azure AD Configuration
```
Tenant ID: 9525f140-7768-4f65-8ebb-54bd5151f7cb
Client ID: ba58ec08-f9c8-4232-8a01-8e90c5e4de2a
Test User: theragraf-e2e-test@[yourdomain].onmicrosoft.com
```

### Useful Commands

```powershell
# Check if test user exists
az ad user show --id "theragraf-e2e-test@[yourdomain].onmicrosoft.com"

# Reset test user password
az ad user update --id "theragraf-e2e-test@[yourdomain].onmicrosoft.com" --password "[NewPassword123!]" --force-change-password-next-sign-in false

# Delete test user (if needed)
az ad user delete --id "theragraf-e2e-test@[yourdomain].onmicrosoft.com"

# View recent sign-ins
az ad user get-member-groups --id "theragraf-e2e-test@[yourdomain].onmicrosoft.com"
```

---

## Next Steps

Once your test user is set up and E2E tests pass:

1. ✅ Remove diagnostic tests (`diagnostic.spec.ts`)
2. ✅ Clean up debug logging in page objects
3. ✅ Document E2E workflow in main README
4. ✅ Set up GitHub Actions workflow (when ready for CI/CD)

---

**Need Help?**

If you encounter issues:
1. Check the sign-in logs in Azure Portal
2. Run setup with `--headed` flag to watch the login flow
3. Verify all conditional access exclusions are active
4. Test login manually in incognito browser first
