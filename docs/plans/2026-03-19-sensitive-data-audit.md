# Sensitive Data Audit Report

**Date:** 2026-03-19
**Scope:** Chronith API — all database entities and in-flight data
**Author:** Security audit as part of v0.7 pre-work
**Status:** Draft — pending engineering review

---

## Executive Summary

The Chronith API uses a layered encryption strategy for the most sensitive credentials (payment keys, notification provider secrets, webhook signing secrets). Passwords are hashed using a memory-hard algorithm and never stored in plaintext. However, a significant number of PII fields (email addresses, phone numbers) are stored in plaintext, and at least one credential field (`CustomerCallbackSecret`) is unencrypted despite being functionally identical to an already-encrypted field.

This report catalogues the current state, identifies gaps, and recommends a prioritised remediation path.

---

## 1. What Is Currently Protected

### 1.1 Passwords — Hashed (Never Stored)

All user passwords (tenant admin, staff, customer) are hashed before storage. The raw password is never persisted.

| Entity | Field | Algorithm | Notes |
|--------|-------|-----------|-------|
| `TenantUserEntity` | `PasswordHash` | BCrypt WF12 (→ Argon2id) | Being upgraded — see `2026-03-19-argon2id-password-upgrade-design.md` |
| `CustomerEntity` | `PasswordHash` | BCrypt WF12 (→ Argon2id) | Being upgraded |

BCrypt WF12 provides approximately 200–300ms hash time, making offline brute-force attacks expensive. The Argon2id upgrade adds memory-hardness (64 MB per attempt), further increasing the cost for GPU-based attacks.

### 1.2 Webhook Secrets — AES-256-GCM Encrypted

Webhook signing secrets are encrypted at rest using `IEncryptionService` (AES-256-GCM implementation). The key is stored in `Security:EncryptionKey` (a Base64-encoded 32-byte key from environment/Key Vault — never committed to source control).

| Entity | Field | Protection |
|--------|-------|------------|
| `WebhookEntity` | `Secret` | AES-256-GCM encrypted/decrypted in `WebhookRepository` |

### 1.3 Notification Provider Credentials — AES-256-GCM Encrypted

Notification channel settings (SMTP credentials, Twilio auth tokens, Firebase service account keys) are stored as encrypted blobs.

| Entity | Field | Protection |
|--------|-------|------------|
| `NotificationChannelEntity` | `EncryptedSettings` | AES-256-GCM encrypted blob; deserialized per channel type |

Covered by `EncryptionKeyRotationService` which re-encrypts all blobs when the key is rotated.

### 1.4 Payment Provider Credentials — AES-256-GCM Encrypted

Payment provider API keys and secrets are stored as encrypted blobs.

| Entity | Field | Protection |
|--------|-------|------------|
| `PaymentProviderEntity` | `EncryptedSettings` | AES-256-GCM encrypted blob |

### 1.5 API Keys — SHA-256 Hashed

Raw API keys are shown to the tenant once at creation and never stored. Only the SHA-256 hash is persisted.

| Entity | Field | Protection |
|--------|-------|------------|
| `ApiKeyEntity` | `KeyHash` | SHA-256 hash — raw key never stored |

**Threat model note:** SHA-256 is appropriate here because API keys are long random values (high entropy). The attack surface is fundamentally different from passwords, where users choose low-entropy strings. SHA-256 without salt is acceptable for high-entropy random tokens.

### 1.6 Refresh Tokens — SHA-256 Hashed

Same pattern as API keys — raw token shown once, SHA-256 hash stored.

| Entity | Field | Protection |
|--------|-------|------------|
| `RefreshTokenEntity` | `TokenHash` | SHA-256 hash — raw token never stored |

---

## 2. Gaps — Plaintext Sensitive Data

### 2.1 Risk Matrix

| Risk | Entity | Field(s) | Category | Notes |
|------|--------|----------|----------|-------|
| **HIGH** | `BookingTypeEntity` | `CustomerCallbackSecret` | Credential | Webhook signing secret — functionally identical to `WebhookEntity.Secret` which IS encrypted. Appears to be an oversight. |
| **HIGH** | `CustomerEntity` | `Email`, `Phone` | PII | Customer contact details stored in plaintext |
| **MEDIUM** | `BookingEntity` | `CustomerEmail` | PII | Denormalized customer email on every booking row |
| **MEDIUM** | `WaitlistEntryEntity` | `CustomerEmail` | PII | Same concern as `BookingEntity` |
| **MEDIUM** | `TenantUserEntity` | `Email` | PII | Admin/staff email stored in plaintext |
| **MEDIUM** | `StaffMemberEntity` | `Email` | PII | Staff email stored in plaintext |
| **LOW-MEDIUM** | `AuditEntryEntity` | `OldValues`, `NewValues` | PII (indirect) | JSON diffs of entity changes — may contain email, phone, or other PII captured during state transitions |
| **LOW-MEDIUM** | `WebhookOutboxEntryEntity` | `Payload` | PII (indirect) | Outbound webhook payloads likely include customer name, email, booking details |
| **LOW-MEDIUM** | `IdempotencyKeyEntity` | `ResponseBody` | PII (indirect) | Cached HTTP response bodies — may contain PII if the original request/response included customer data |
| **LOW** | `TenantAuthConfigEntity` | `OidcClientId` | Semi-sensitive | Identifies the tenant's OIDC integration; not a secret by itself but worth noting |

### 2.2 Detailed Analysis

#### `BookingTypeEntity.CustomerCallbackSecret` — HIGH

This field stores a signing secret used to verify customer-facing webhook callbacks (the mechanism by which third-party systems can verify that a callback originated from Chronith). This is a credential, not PII.

The `WebhookEntity.Secret` field — which serves the same purpose for outbound webhooks — is already AES-256-GCM encrypted in `WebhookRepository`. The `CustomerCallbackSecret` appears to have been added later without applying the same treatment. This is an oversight and should be fixed immediately.

**Evidence of inconsistency:**
- `WebhookRepository.CreateAsync()` → calls `_encryption.Encrypt(webhook.Secret)` ✓
- `BookingTypeRepository.CreateAsync()` → stores `bookingType.CustomerCallbackSecret` as plaintext ✗

#### `CustomerEntity.Email` and `CustomerEntity.Phone` — HIGH

Customer email and phone number are PII under GDPR, PDPA (Philippines), and most other privacy regulations. They are stored as plaintext `varchar` columns with no additional protection. This means:

- A database dump exposes all customer contact details
- A SQL injection vulnerability would expose all customer contact details
- Any staff member with direct database access can read all customer PII

**Query complexity note:** Field-level encryption of `Email` prevents equality queries (`WHERE email = ?`) unless a deterministic (HMAC) index token is stored alongside the encrypted value. The remediation section covers this.

#### `BookingEntity.CustomerEmail` and `WaitlistEntryEntity.CustomerEmail` — MEDIUM

These fields denormalize the customer email at the time of booking/waitlist entry. They exist so booking records remain coherent even if the customer record changes. If `CustomerEntity.Email` is encrypted, these denormalized copies must be treated consistently.

#### `TenantUserEntity.Email` and `StaffMemberEntity.Email` — MEDIUM

Admin and staff emails are PII. The risk is slightly lower than customer PII because these are internal users (tenant operators), but the same regulatory obligations apply.

#### `AuditEntryEntity.OldValues` / `NewValues` — LOW-MEDIUM

The audit log captures JSON snapshots of entity state before and after changes. Any change to a customer record (e.g., updating email or phone) will appear in plaintext in the audit log's `OldValues`/`NewValues` columns, even if the entity fields themselves are later encrypted.

This creates a secondary PII exposure surface that is often overlooked. Audit log data is typically retained longer than operational data.

#### `WebhookOutboxEntryEntity.Payload` — LOW-MEDIUM

Outbound webhook payloads almost certainly include customer identifiers, booking details, and potentially customer email/phone. These are stored in the outbox table until delivered (and potentially for some retention period after).

#### `IdempotencyKeyEntity.ResponseBody` — LOW-MEDIUM

Cached HTTP response bodies are stored to support idempotency. Responses from booking creation, customer registration, etc. are likely to include PII in their JSON bodies.

---

## 3. Recommended Remediation

Prioritised by risk and implementation effort.

### Priority 1 — Immediate (HIGH risk, low effort)

**Encrypt `BookingTypeEntity.CustomerCallbackSecret`**

Follow the exact same pattern as `WebhookRepository`:

1. Inject `IEncryptionService` into `BookingTypeRepository`
2. Call `_encryption.Encrypt(entity.CustomerCallbackSecret)` on write in `CreateAsync` and `UpdateAsync`
3. Call `_encryption.Decrypt(entity.CustomerCallbackSecret)` on read in `GetByIdAsync`, `GetBySlugAsync`, and any other read path
4. Add `BookingTypeEntity` to `EncryptionKeyRotationService` (so the secret is re-encrypted on key rotation)

This is a one-afternoon change with no schema migration needed (the field is already `text` or sufficient length for the encrypted envelope).

### Priority 2 — Short-term (HIGH risk, medium effort)

**Evaluate field-level encryption for `Customer.Email` and `Customer.Phone`**

Options:

| Option | Pros | Cons |
|--------|------|------|
| **AES-GCM (like existing secrets)** | Consistent pattern, strong encryption | Cannot query by email — breaks login and lookup flows |
| **Deterministic HMAC index token** | Allows equality queries (`WHERE email_token = HMAC(email)`) | Slightly weaker than probabilistic encryption; leaks equality |
| **Tokenisation / external vault** | Strongest isolation | Significant infrastructure complexity |
| **Application-level hashing for lookup + encrypted store** | Both searchable and encrypted | More complex code; two fields per protected value |

The recommended approach for `Customer.Email` is the **HMAC index token pattern**:
- Store `EmailEncrypted` (AES-GCM) and `EmailToken` (HMAC-SHA256 with a separate key) 
- Query by `EmailToken` for login/lookup
- Read `EmailEncrypted` for display/notification

This preserves all existing query patterns while protecting the plaintext value at rest.

### Priority 3 — Medium-term (MEDIUM risk, medium effort)

**Apply consistent email protection to `Booking`, `WaitlistEntry`, `TenantUser`, `StaffMember`**

After the `Customer` pattern is established, apply it consistently to the denormalized `CustomerEmail` fields and to staff/admin email fields.

For denormalized `CustomerEmail` in bookings: since these are point-in-time copies (capturing the email at booking time), they should be AES-GCM encrypted without needing a search token (bookings are not queried by customer email directly).

### Priority 4 — Long-term (LOW-MEDIUM risk, higher effort)

**Audit log PII scrubbing**

Options:
- **Field masking at write time:** Before storing `OldValues`/`NewValues`, strip or mask known PII fields from the JSON snapshot
- **Audit log retention policy:** Automatically purge audit entries older than N days (reduces the window of PII exposure)
- **Separate audit log store:** Move audit data to a purpose-built system with field-level access controls

**Webhook payload and idempotency cache TTL**

Ensure `WebhookOutboxEntry` rows are deleted after successful delivery (not merely marked as delivered). Confirm `IdempotencyKeyEntity` has a TTL and entries are purged after expiry (not just ignored).

---

## 4. Out of Scope

### Transport-level encryption (TLS)

All API traffic is encrypted in transit via TLS. This is handled at the reverse proxy / Azure App Service layer and is not in scope for this audit.

### Application-level token security

JWT access tokens and refresh tokens follow standard patterns (short-lived JWTs, hashed refresh tokens, rotation on use). This was covered in the v0.4 auth design.

### Secrets management

`Security:EncryptionKey`, database connection strings, and other secrets are managed via Azure Key Vault references in App Service configuration. This is covered in the Azure Key Vault deployment plan (`2026-03-19-azure-key-vault-plan.md`).

---

## 5. Compliance Notes

The Philippines Data Privacy Act (RA 10173 / PDPA) classifies email addresses and phone numbers as personal information requiring reasonable security measures. Field-level encryption is not mandated by PDPA but is considered a reasonable technical safeguard, particularly for a multi-tenant SaaS where one tenant's misconfiguration should not expose another tenant's customer data.

GDPR Article 32 requires "appropriate technical and organisational measures" proportionate to the risk. For a booking system handling customer contact details, field-level encryption of email and phone is proportionate.

---

## 6. Summary Table

| Finding | Severity | Effort | Remediation |
|---------|----------|--------|-------------|
| `BookingTypeEntity.CustomerCallbackSecret` plaintext | HIGH | Low (1–2 hours) | Encrypt in repository — follow `WebhookRepository` pattern |
| `CustomerEntity.Email` / `Phone` plaintext | HIGH | Medium (1–2 days) | HMAC token + AES-GCM encryption |
| `BookingEntity.CustomerEmail` plaintext | MEDIUM | Low (after Customer pattern) | AES-GCM on denormalized field |
| `WaitlistEntryEntity.CustomerEmail` plaintext | MEDIUM | Low (after Customer pattern) | AES-GCM on denormalized field |
| `TenantUserEntity.Email` plaintext | MEDIUM | Low (after Customer pattern) | AES-GCM + token |
| `StaffMemberEntity.Email` plaintext | MEDIUM | Low (after Customer pattern) | AES-GCM + token |
| Audit log PII in `OldValues`/`NewValues` | LOW-MEDIUM | High (requires audit refactor) | Field masking or retention policy |
| `WebhookOutboxEntryEntity.Payload` PII | LOW-MEDIUM | Low (verify delete-on-delivery) | Confirm TTL/deletion behaviour |
| `IdempotencyKeyEntity.ResponseBody` PII | LOW-MEDIUM | Low (verify TTL) | Confirm purge on expiry |
| `TenantAuthConfigEntity.OidcClientId` semi-sensitive | LOW | None (accept risk) | No action required |
