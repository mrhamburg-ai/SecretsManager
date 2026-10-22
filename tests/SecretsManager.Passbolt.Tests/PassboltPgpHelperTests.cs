using System.Text;
using Org.BouncyCastle.Bcpg;
using Org.BouncyCastle.Bcpg.OpenPgp;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using SecretsManager.Passbolt.Internal;

namespace SecretsManager.Passbolt.Tests;

public sealed class PassboltPgpHelperTests
{
    [Fact]
    public void EncryptAndSign_ArmoredMessage_RoundTripsAndVerifies()
    {
        var keyPair = GenerateKeyPair();
        var secretKey = new PgpSecretKey(
            PgpSignature.DefaultCertification,
            keyPair,
            "Test <test@example.com>",
            SymmetricKeyAlgorithmTag.Null,
            (char[]?)null,
            false,
            (PgpSignatureSubpacketVector?)null,
            (PgpSignatureSubpacketVector?)null,
            new SecureRandom());

        var armoredSecret = Armor(stream => new PgpSecretKeyRing(secretKey.GetEncoded()).Encode(stream));
        var armoredPublic = Armor(stream => new PgpPublicKeyRing(secretKey.PublicKey.GetEncoded()).Encode(stream));

        var recipientPublic = PassboltPgpHelper.ReadPublicKey(armoredPublic);
        var senderSecret = PassboltPgpHelper.ReadSecretKey(armoredSecret);
        var senderPrivate = PassboltPgpHelper.ExtractPrivateKey(senderSecret, "");

        var message = PassboltPgpHelper.EncryptAndSign(
            "hello, passbolt", recipientPublic, recipientPublic, senderPrivate);

        Assert.StartsWith("-----BEGIN PGP MESSAGE-----", message);

        var decrypted = PassboltPgpHelper.DecryptAndVerify(
            message, senderSecret, senderPrivate, recipientPublic);

        Assert.Equal("hello, passbolt", decrypted);
    }

    [Fact]
    public void GetFingerprint_ReturnsUppercaseHex()
    {
        var keyPair = GenerateKeyPair();
        var secretKey = new PgpSecretKey(
            PgpSignature.DefaultCertification,
            keyPair,
            "Test <test@example.com>",
            SymmetricKeyAlgorithmTag.Null,
            (char[]?)null,
            false,
            (PgpSignatureSubpacketVector?)null,
            (PgpSignatureSubpacketVector?)null,
            new SecureRandom());

        var fingerprint = PassboltPgpHelper.GetFingerprint(secretKey.PublicKey);

        Assert.Matches("^[0-9A-F]+$", fingerprint);
        Assert.Equal(40, fingerprint.Length);
    }

    private static PgpKeyPair GenerateKeyPair()
    {
        var generator = new RsaKeyPairGenerator();
        generator.Init(new RsaKeyGenerationParameters(
            BigInteger.ValueOf(0x10001), new SecureRandom(), 2048, 80));

        return new PgpKeyPair(
            PublicKeyAlgorithmTag.RsaGeneral,
            generator.GenerateKeyPair(),
            DateTime.UtcNow);
    }

    private static string Armor(Action<Stream> encode)
    {
        using var output = new MemoryStream();
        using (var armored = new ArmoredOutputStream(output))
        {
            encode(armored);
        }

        return Encoding.ASCII.GetString(output.ToArray());
    }
}
