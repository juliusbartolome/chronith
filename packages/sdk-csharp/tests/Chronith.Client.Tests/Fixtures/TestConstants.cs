namespace Chronith.Client.Tests.Fixtures;

public static class TestConstants
{
    public const string JwtSigningKey = "functional-test-signing-key-at-least-32-chars!!";
    // 32 zero bytes — valid AES-256-GCM key for test use only
    public const string EncryptionKey = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";
    public static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public const string AdminUserId      = "user-admin-1";
    public const string StaffUserId      = "user-staff-1";
    public const string CustomerUserId   = "user-customer-1";
    public const string PaymentSvcUserId = "user-paymentsvc-1";
}
