# Quick Start: E2E Test User Setup

## TL;DR - Fastest Path

```powershell
# 1. Run the automated setup script
cd Theragraf.Web/tests/e2e/docs
./create-test-user.ps1

# 2. Copy the output credentials to .env.test
# (Script shows you exactly what to add)

# 3. Disable MFA for test user (Azure Portal)
# Go to: Entra ID → Users → Per-user MFA → Find test user → Disable

# 4. Test it
cd ../../..
npx playwright test --project=setup

# 5. Run full suite
npm run test:e2e
```

---

## Manual Setup (Portal)

### Create User (5 minutes)
1. **Azure Portal** → **Microsoft Entra ID** → **Users** → **+ New user**
2. Fill in:
   - UPN: `theragraf-e2e-test@[yourdomain].onmicrosoft.com`
   - Display name: `Theragraf E2E Test User`
   - Password: Generate and save
   - ⚠️ **Uncheck** "Require password change at first sign-in"
3. Click **Create**

### Disable MFA (CRITICAL!)
1. **Entra ID** → **Users** → **Per-user MFA**
2. Find `theragraf-e2e-test`
3. Click **Disable**

### Assign to App (Optional but Recommended)
1. **Entra ID** → **Enterprise Applications**
2. Find "Theragraf" (or search by ID: `ba58ec08-f9c8-4232-8a01-8e90c5e4de2a`)
3. **Users and groups** → **+ Add user/group**
4. Select test user → **Assign**

### Update .env.test
```env
TEST_USER_EMAIL=theragraf-e2e-test@[yourdomain].onmicrosoft.com
TEST_USER_PASSWORD=[your-generated-password]
```

---

## Verification Checklist

Before running E2E tests:

- [ ] Test user created in Azure AD
- [ ] MFA disabled for test user
- [ ] Password set to not expire
- [ ] User assigned to Theragraf app
- [ ] Credentials added to `.env.test`
- [ ] `.env.test` is in `.gitignore` (already done)
- [ ] Manual login works in incognito browser
- [ ] No passkey prompt appears during login

---

## Test Login Flow

### Manual Test (Browser)
```
1. Open incognito window
2. Go to http://localhost:5173
3. Sign in with test user
4. Should see: Email → Password → Dashboard
5. Should NOT see: Passkey prompt, MFA prompt, or password change
```

### Automated Test
```powershell
# Run auth setup
npx playwright test --project=setup

# Check if auth file was created
Test-Path "Theragraf.Web/tests/e2e/.auth/user.json"
# Should return: True
```

---

## Troubleshooting

| Error | Fix |
|-------|-----|
| "Sign-in blocked" | Assign user to app in Enterprise Applications |
| "MFA required" | Disable MFA in Per-user MFA settings |
| "Passkey prompt appears" | Disable security defaults or add CA exclusion |
| "Password expired" | Set password policy to never expire |
| "Auth file not created" | Run with `--headed` to watch login flow |

---

## Your Configuration

```
Tenant ID: 9525f140-7768-4f65-8ebb-54bd5151f7cb
Client ID: ba58ec08-f9c8-4232-8a01-8e90c5e4de2a
Test User: theragraf-e2e-test@[yourdomain].onmicrosoft.com
```

---

## Need Help?

Full documentation: `TEST_USER_SETUP.md`
Automated script: `create-test-user.ps1`
