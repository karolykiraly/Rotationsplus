using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace RotationsPlus.Common.Data;

/// <summary>
/// Pluggable column-level protector. The real implementation wraps a Key Vault-managed key;
/// it is injected where sensitive columns (e.g. preceptor bank details, SSN) are configured.
/// </summary>
public interface IColumnProtector
{
    string Protect(string plaintext);
    string Unprotect(string ciphertext);
}

/// <summary>
/// EF Core value converter that encrypts a string column on write and decrypts on read.
/// Apply in entity configurations: <c>builder.Property(x =&gt; x.Secret).HasConversion(new EncryptedStringConverter(protector));</c>
/// </summary>
public sealed class EncryptedStringConverter : ValueConverter<string, string>
{
    public EncryptedStringConverter(IColumnProtector protector)
        : base(plaintext => protector.Protect(plaintext),
               ciphertext => protector.Unprotect(ciphertext))
    {
    }
}
