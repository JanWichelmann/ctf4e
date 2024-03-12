using System;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Ctf4e.Api.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Ctf4e.Api.Services
{
    public interface ICryptoService
    {
        /// <summary>
        ///     Adds a timestamp to the given plain text, encrypts it and returns string representation of the cipher text.
        /// </summary>
        /// <param name="plain">Plain text.</param>
        /// <returns></returns>
        string Encrypt(string plain);

        /// <summary>
        ///     Decodes and decrypts the given cipher text.
        ///     This function also checks the included timestamp; if it exceeds the internal validity period, an <see cref="CryptographicException" /> is thrown.
        /// </summary>
        /// <param name="cipher"></param>
        /// <returns></returns>
        string Decrypt(string cipher);
    }

    /// <summary>
    ///     Provides functions to encrypt/decrypt strings while guaranteeing freshness and integrity.
    /// </summary>
    public class CryptoService : ICryptoService
    {
        private static readonly TimeSpan _timestampValidityDuration = new(0, 3, 0);

        private readonly byte[] _key;

        public CryptoService(IOptions<CtfApiOptions> options)
            : this(options.Value.LabApiCode)
        {
        }

        public CryptoService(string labApiCode)
        {
            // Derive key
            // We rely on the randomness of the API code here
            using var keyGen = new Rfc2898DeriveBytes(labApiCode, new byte[8], 32, HashAlgorithmName.SHA256);
            _key = keyGen.GetBytes(16);
        }

        /// <summary>
        ///     Adds a timestamp to the given plain text, encrypts it and returns string representation of the cipher text.
        /// </summary>
        /// <param name="plain">Plain text.</param>
        /// <returns></returns>
        public string Encrypt(string plain)
        {
            // Get bytes of string and current time
            byte[] plainBytes = Encoding.UTF8.GetBytes(new string(' ', 8) + plain);
            BitConverter.GetBytes(DateTime.UtcNow.ToBinary()).CopyTo(plainBytes, 0);

            // Get parameter sizes
            int nonceSize = AesGcm.NonceByteSizes.MaxSize;
            int tagSize = AesGcm.TagByteSizes.MaxSize;
            int cipherSize = plainBytes.Length;

            // We write everything into one big array for easier encoding
            int encryptedDataLength = 4 + nonceSize + 4 + tagSize + cipherSize;
            Span<byte> encryptedData = encryptedDataLength < 1024 ? stackalloc byte[encryptedDataLength] : new byte[encryptedDataLength].AsSpan();

            // Copy parameters
            BinaryPrimitives.WriteInt32LittleEndian(encryptedData.Slice(0, 4), nonceSize);
            BinaryPrimitives.WriteInt32LittleEndian(encryptedData.Slice(4 + nonceSize, 4), tagSize);
            var nonce = encryptedData.Slice(4, nonceSize);
            var tag = encryptedData.Slice(4 + nonceSize + 4, tagSize);
            var cipherBytes = encryptedData.Slice(4 + nonceSize + 4 + tagSize, cipherSize);

            // Generate secure nonce
            RandomNumberGenerator.Fill(nonce);

            // Encrypt
            using var aesGcm = new AesGcm(_key, tagSize);
            aesGcm.Encrypt(nonce, plainBytes.AsSpan(), cipherBytes, tag);

            // Encode
            return Base64UrlEncoder.Encode(encryptedData.ToArray());
        }

        /// <summary>
        ///     Decodes and decrypts the given cipher text.
        ///     This function also checks the included timestamp; if it exceeds the internal validity period, an <see cref="CryptographicException" /> is thrown.
        /// </summary>
        /// <param name="cipher"></param>
        /// <returns></returns>
        public string Decrypt(string cipher)
        {
            // Decode
            Span<byte> encryptedData = Base64UrlEncoder.DecodeBytes(cipher).AsSpan();

            // Extract parameter sizes
            int nonceSize = BinaryPrimitives.ReadInt32LittleEndian(encryptedData.Slice(0, 4));
            int tagSize = BinaryPrimitives.ReadInt32LittleEndian(encryptedData.Slice(4 + nonceSize, 4));
            int cipherSize = encryptedData.Length - 4 - nonceSize - 4 - tagSize;

            // Extract parameters
            var nonce = encryptedData.Slice(4, nonceSize);
            var tag = encryptedData.Slice(4 + nonceSize + 4, tagSize);
            var cipherBytes = encryptedData.Slice(4 + nonceSize + 4 + tagSize, cipherSize);

            // Decrypt
            using var aesGcm = new AesGcm(_key, tagSize);
            Span<byte> plainBytes = cipherSize < 1024 ? stackalloc byte[cipherSize] : new byte[cipherSize];
            aesGcm.Decrypt(nonce, cipherBytes, tag, plainBytes);

            // Extract time stamp and verify
            DateTime timestamp = DateTime.FromBinary(BitConverter.ToInt64(plainBytes.Slice(0, 8)));
            if(DateTime.UtcNow - timestamp > _timestampValidityDuration)
                throw new CryptographicException("Timestamp validation failed.");

            // Convert plain bytes back into string
            return Encoding.UTF8.GetString(plainBytes.Slice(8));
        }
    }
}