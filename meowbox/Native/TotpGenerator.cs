using System.Security.Cryptography;

namespace meowbox.Native;

public static class TotpGenerator
{
    public static string GenerateCode(string base32Secret, int digits = 6, int period = 30)
    {
        var keyBytes = Base32Decode(base32Secret);

        var unixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var counter = unixTime / period;

        var counterBytes = BitConverter.GetBytes(counter);
        if(BitConverter.IsLittleEndian)
            Array.Reverse(counterBytes); 

        byte[] hash;
        using(var hmac = new HMACSHA1(keyBytes))
        {
            hash = hmac.ComputeHash(counterBytes);
        }

        var offset = hash[hash.Length - 1] & 0x0F;
        var binaryCode =
            ((hash[offset] & 0x7F) << 24) |
            ((hash[offset + 1] & 0xFF) << 16) |
            ((hash[offset + 2] & 0xFF) << 8) |
            (hash[offset + 3] & 0xFF);

        var otp = binaryCode % (int)Math.Pow(10, digits);
        return otp.ToString(new string('0', digits));
    }

    private static byte[] Base32Decode(string input)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

        input = input.TrimEnd('=').ToUpperInvariant().Replace(" ", "");
        if(input.Length == 0)
            return Array.Empty<byte>();

        var outputLength = input.Length * 5 / 8;
        var result = new byte[outputLength];

        var bitBuffer = 0;
        var bitsInBuffer = 0;
        var outputIndex = 0;

        foreach(var c in input)
        {
            var charValue = alphabet.IndexOf(c);
            if(charValue < 0) throw new FormatException($"Invalid Base32 character: '{c}'");

            bitBuffer = (bitBuffer << 5) | charValue;
            bitsInBuffer += 5;

            if(bitsInBuffer >= 8)
            {
                bitsInBuffer -= 8;
                result[outputIndex++] = (byte)((bitBuffer >> bitsInBuffer) & 0xFF);
            }
        }

        return result;
    }
}
