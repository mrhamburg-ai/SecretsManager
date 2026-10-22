using System.IO;
using System.Text;
using Org.BouncyCastle.Bcpg;
using Org.BouncyCastle.Bcpg.OpenPgp;
using Org.BouncyCastle.Security;

namespace SecretsManager.Passbolt.Internal;

internal static class PassboltPgpHelper
{
    public static PgpPublicKey ReadPublicKey(string armoredKey) =>
        ReadPublicKey(armoredKey, key => key.IsEncryptionKey, "encryption");

    public static PgpPublicKey ReadSigningPublicKey(string armoredKey) =>
        ReadPublicKey(armoredKey, key => key.IsMasterKey, "signing");

    public static PgpSecretKey ReadSecretKey(string armoredKey) =>
        ReadSecretKey(armoredKey, key => key.IsSigningKey, "signing");

    public static PgpSecretKey ReadEncryptionSecretKey(string armoredKey) =>
        ReadSecretKey(armoredKey, key => key.PublicKey.IsEncryptionKey, "encryption");

    private static PgpPublicKey ReadPublicKey(
        string armoredKey, Func<PgpPublicKey, bool> predicate, string purpose)
    {
        using var inputStream = new MemoryStream(Encoding.UTF8.GetBytes(armoredKey));
        var decoderStream = PgpUtilities.GetDecoderStream(inputStream);
        var factory = new PgpObjectFactory(decoderStream);

        PgpPublicKeyRing? keyRing = null;
        while (keyRing == null)
        {
            var obj = factory.NextPgpObject();
            if (obj is null)
                throw new ArgumentException("No PGP public key found in the provided key data.");
            if (obj is PgpPublicKeyRing kr)
                keyRing = kr;
        }

        var keys = keyRing.GetPublicKeys().Cast<PgpPublicKey>().ToList();
        foreach (var key in keys)
        {
            if (predicate(key))
                return key;
        }

        // Single-key rings are common: fall back to the first key rather than failing.
        return keys.FirstOrDefault()
            ?? throw new ArgumentException($"No {purpose} key found in the PGP public key ring.");
    }

    private static PgpSecretKey ReadSecretKey(
        string armoredKey, Func<PgpSecretKey, bool> predicate, string purpose)
    {
        using var inputStream = new MemoryStream(Encoding.UTF8.GetBytes(armoredKey));
        var decoderStream = PgpUtilities.GetDecoderStream(inputStream);
        var factory = new PgpObjectFactory(decoderStream);

        PgpSecretKeyRing? keyRing = null;
        while (keyRing == null)
        {
            var obj = factory.NextPgpObject();
            if (obj is null)
                throw new ArgumentException("No PGP secret key found in the provided key data.");
            if (obj is PgpSecretKeyRing kr)
                keyRing = kr;
        }

        var keys = keyRing.GetSecretKeys().Cast<PgpSecretKey>().ToList();
        foreach (var key in keys)
        {
            if (predicate(key))
                return key;
        }

        return keys.FirstOrDefault()
            ?? throw new ArgumentException($"No {purpose} key found in the PGP secret key ring.");
    }

    public static PgpPrivateKey ExtractPrivateKey(PgpSecretKey secretKey, string passphrase)
    {
        return secretKey.ExtractPrivateKey(passphrase.ToCharArray());
    }

    public static string EncryptAndSign(
        string plaintext,
        PgpPublicKey recipientPublicKey,
        PgpPublicKey senderPublicKey,
        PgpPrivateKey senderPrivateKey)
    {
        _ = senderPublicKey;
        var plainBytes = Encoding.UTF8.GetBytes(plaintext);

        using var outputStream = new MemoryStream();
        using (var armoredStream = new ArmoredOutputStream(outputStream))
        {
            var encryptedGenerator = new PgpEncryptedDataGenerator(
                SymmetricKeyAlgorithmTag.Aes256, true, new SecureRandom());
            encryptedGenerator.AddMethod(recipientPublicKey);

            // Use the length-unknown (partial packet) overload: the caller cannot know the
            // final size because compression, signatures, and packet headers add bytes.
            using var encryptedStream = encryptedGenerator.Open(armoredStream, new byte[1 << 16]);

            var compressedGenerator = new PgpCompressedDataGenerator(CompressionAlgorithmTag.Zip);
            var compressedStream = compressedGenerator.Open(encryptedStream);
            try
            {
                var signatureGenerator = new PgpSignatureGenerator(
                    senderPrivateKey.PublicKeyPacket.Algorithm, HashAlgorithmTag.Sha256);
                signatureGenerator.InitSign(PgpSignature.BinaryDocument, senderPrivateKey);

                signatureGenerator.GenerateOnePassVersion(false).Encode(compressedStream);

                var literalGenerator = new PgpLiteralDataGenerator();
                var literalStream = literalGenerator.Open(
                    compressedStream,
                    PgpLiteralData.Binary,
                    "_CONSOLE",
                    plainBytes.Length,
                    DateTime.UtcNow);
                try
                {
                    literalStream.Write(plainBytes, 0, plainBytes.Length);
                }
                finally
                {
                    literalGenerator.Close();
                }

                signatureGenerator.Update(plainBytes, 0, plainBytes.Length);
                signatureGenerator.Generate().Encode(compressedStream);
            }
            finally
            {
                compressedGenerator.Close();
            }
        }

        return Encoding.ASCII.GetString(outputStream.ToArray());
    }

    public static string DecryptAndVerify(
        string armoredMessage,
        PgpSecretKey recipientSecretKey,
        PgpPrivateKey recipientPrivateKey,
        PgpPublicKey? verifierPublicKey = null)
    {
        using var inputStream = new MemoryStream(Encoding.UTF8.GetBytes(armoredMessage));
        var decoderStream = PgpUtilities.GetDecoderStream(inputStream);
        var factory = new PgpObjectFactory(decoderStream);

        var encryptedList = factory.NextPgpObject() as PgpEncryptedDataList
            ?? throw new ArgumentException("No encrypted data found in the message.");

        PgpPublicKeyEncryptedData? encryptedData = null;
        foreach (PgpPublicKeyEncryptedData pked in encryptedList.GetEncryptedDataObjects())
        {
            if (pked.KeyId == recipientSecretKey.KeyId)
            {
                encryptedData = pked;
                break;
            }
        }

        if (encryptedData is null)
            throw new ArgumentException("No encrypted data found for the provided key.");

        using var clearStream = encryptedData.GetDataStream(recipientPrivateKey);
        var clearFactory = new PgpObjectFactory(clearStream);
        var message = clearFactory.NextPgpObject();

        if (message is PgpCompressedData compressedData)
        {
            clearFactory = new PgpObjectFactory(compressedData.GetDataStream());
            message = clearFactory.NextPgpObject();
        }

        PgpOnePassSignature? onePassSignature = null;
        if (message is PgpOnePassSignatureList onePassList)
        {
            if (onePassList.Count == 0)
                throw new ArgumentException("The signed message does not contain a signature.");

            onePassSignature = onePassList[0];
            message = clearFactory.NextPgpObject();
        }

        if (message is not PgpLiteralData literalData)
            throw new ArgumentException("Unexpected message format in decrypted data.");

        var plainBytes = ReadAllBytes(literalData.GetInputStream());
        var next = clearFactory.NextPgpObject();
        var signatureList = next as PgpSignatureList;

        if (verifierPublicKey is not null)
            VerifySignature(plainBytes, onePassSignature, signatureList, verifierPublicKey);

        return Encoding.UTF8.GetString(plainBytes);
    }

    private static void VerifySignature(
        byte[] plainBytes,
        PgpOnePassSignature? onePassSignature,
        PgpSignatureList? signatureList,
        PgpPublicKey verifierPublicKey)
    {
        if (onePassSignature is not null && signatureList is { Count: > 0 })
        {
            onePassSignature.InitVerify(verifierPublicKey);
            onePassSignature.Update(plainBytes, 0, plainBytes.Length);
            if (!onePassSignature.Verify(signatureList[0]))
                throw new SecretProviderException("PGP signature verification failed.");
            return;
        }

        if (signatureList is { Count: > 0 })
        {
            var signature = signatureList[0];
            signature.InitVerify(verifierPublicKey);
            signature.Update(plainBytes, 0, plainBytes.Length);
            if (!signature.Verify())
                throw new SecretProviderException("PGP signature verification failed.");
            return;
        }

        throw new SecretProviderException("PGP message is not signed.");
    }

    private static byte[] ReadAllBytes(Stream stream)
    {
        using (stream)
        using (var buffer = new MemoryStream())
        {
            stream.CopyTo(buffer);
            return buffer.ToArray();
        }
    }

    public static string GetFingerprint(PgpPublicKey publicKey)
    {
        return Convert.ToHexString(publicKey.GetFingerprint());
    }
}
