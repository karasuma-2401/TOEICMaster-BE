# Feature Specification: Authentication, Profile & Account Administration (Auth & Profile)

> **Status:** Implemented / Production Ready  
> **Figma Mapping:** Screen 01 (`/login`), Screen 02 (`/register`), Screen 03 (`/forgot-password`), Screen 05 (`/profile`), Screen 28 (`/admin/users`)  
> **API Contract:** [contracts/api-contract.md](../../contracts/api-contract.md)

---

## 1. Goal & Scope

### Primary Goal
Provide a secure identity solution for the TOEICMaster system including account registration via Email + OTP, Email/Password login, one-tap Google OAuth login, personal profile management, and admin user listing.

### Functional Scope (In-scope)
1. **User Authentication (`/api/v1/auth`):**
   - Registration: Accept email, full name, password -> send OTP verification code via SMTP (Mailpit/MailKit).
   - Login: Issue `accessToken` (15 minutes) and `refreshToken` (1-30 days).
   - Google Login: Accept Google Identity ID token -> verify signature -> automatically link Gmail/Workspace account.
   - Forgot Password & Reset Password: Reset via email token.
2. **Personal Profile (`/api/v1/profile`):**
   - View current account details (`displayName`, `avatarUrl`, `phone`, `dateOfBirth`, `targetScore`).
   - Update profile info and upload avatar image to MinIO bucket `profile-avatars`.
   - Change password -> automatically revoke all old refresh tokens.
3. **Account Administration (`/api/v1/accounts`):**
   - Admin views paginated account list (`pageNumber`, `pageSize`).
   - The `/admin/users` page uses `GET /api/v1/admin/users` (search, filter, sort, statistics).

---

## 2. Security Guardrails

1. **Token & Password Storage:**
   - PostgreSQL stores only the SHA-256 hash of `refreshToken`, never the raw token.
   - Passwords are one-way hashed using a strong cryptographic algorithm.
2. **Strict Rate Limiting:**
   - Email sending: 5 requests/minute/IP.
   - Auth/Login/Google: 10 requests/minute/IP.
   - Refresh/logout sessions: 30 requests/minute/IP.
3. **Avatar Upload:**
   - 2 MiB limit, mandatory actual file signature check (magic bytes) supporting `image/png`, `image/jpeg`, `image/webp`.

---

## 3. Acceptance Criteria

```gherkin
Scenario: Register and activate account with OTP
  Given the user submits a valid email that does not exist in the system
  When completing the registration form
  Then the system sends a 6-digit OTP code to the email inbox
  And the account remains inactive until OTP verification succeeds

Scenario: Change password successfully
  Given the user is signed in and submits the correct current password along with a new password
  When calling PUT /api/v1/profile/password
  Then the system updates the new password
  And revokes all active refresh sessions for the user
```
