using Chronith.Application.Services;
using Isopoh.Cryptography.Argon2;

namespace Chronith.Infrastructure.Security;

public sealed class Argon2idPasswordHasher : IPasswordHasher
{
    // OWASP recommended parameters for Argon2id (2023+)
    // m=65536 (64 MB), t=3 iterations, p=4 parallelism
    private const int MemorySize = 65536;
    private const int Iterations = 3;
    private const int DegreeOfParallelism = 4;

    public string Hash(string password) =>
        Argon2.Hash(
            password,
            timeCost: Iterations,
            memoryCost: MemorySize,
            parallelism: DegreeOfParallelism,
            type: Argon2Type.HybridAddressing
        );

    public bool Verify(string password, string hash) =>
        Argon2.Verify(hash, password);
}
