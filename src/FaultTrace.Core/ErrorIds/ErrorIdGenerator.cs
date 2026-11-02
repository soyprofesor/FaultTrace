using System.Security.Cryptography;

namespace FaultTrace.Core.ErrorIds;

/// <summary>
/// Creates short user-facing error IDs in the form <c>XXXX-XXXX</c>.
/// </summary>
public static class ErrorIdGenerator
{
    // Uppercase letters and digits without the ambiguous 0, O, 1, I and L.
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    private const int IdLength = 8;
    private const int SeparatorIndex = 4;

    // Bytes at or above this limit are discarded, so every character is equally likely.
    private const int UnbiasedByteLimit = 256 - (256 % 31);

    private static readonly RandomNumberGenerator Random = RandomNumberGenerator.Create();

    /// <summary>
    /// Creates a new error ID from a cryptographic random source, so IDs can't be guessed.
    /// </summary>
    /// <returns>The new ID, for example <c>M3QD-7HZA</c>.</returns>
    public static string NewId()
    {
        var id = new char[IdLength + 1];
        id[SeparatorIndex] = '-';

        var buffer = new byte[IdLength * 2];
        var position = 0;
        while (position < id.Length)
        {
            Random.GetBytes(buffer);
            foreach (var value in buffer)
            {
                if (position == SeparatorIndex)
                {
                    position++;
                }

                if (position == id.Length)
                {
                    break;
                }

                if (value < UnbiasedByteLimit)
                {
                    id[position++] = Alphabet[value % Alphabet.Length];
                }
            }
        }

        return new string(id);
    }
}
