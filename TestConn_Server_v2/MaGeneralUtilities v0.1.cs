namespace MaGeneralUtilities
{
    // All the code in this file is included in all platforms.
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    namespace GeneralUtilities
    {
        public class GeneralUtilities
        {
            //swaps the data for interpretation in a big endian environment
            //param arr is assumed to be little endian
            public static byte[] EndianRightArrange(byte[] arr)
            {
                byte[] werk = new byte[arr.Length];
                Array.Copy(arr, werk, arr.Length);

                if (!BitConverter.IsLittleEndian)
                {
                    Array.Reverse(werk);
                }
                return werk;
            }

            /// <summary>
            /// Transforms byte array into an enumeration of blocks of 'blockSize' bytes
            /// </summary>
            /// <param name="inputAsBytes"></param>
            /// <param name="blockSize"></param>
            /// <returns></returns>
            private static IEnumerable<UInt64> Blockify(byte[] inputAsBytes, int blockSize)
            {
                int i = 0;

                //UInt64 used since that is the biggest possible value we can return.
                //Using an unsigned type is important - otherwise an arithmetic overflow will result
                UInt64 block = 0;

                //Run through all the bytes         
                while (i < inputAsBytes.Length)
                {
                    //Keep stacking them side by side by shifting left and OR-ing               
                    block = block << 8 | inputAsBytes[i];

                    i++;

                    //Return a block whenever we meet a boundary
                    if (i % blockSize == 0 || i == inputAsBytes.Length)
                    {
                        yield return block;

                        //Set to 0 for next iteration
                        block = 0;
                    }
                }
            }

            /// <summary>
            /// Get Fletcher's checksum, n can be either 16, 32 or 64
            /// </summary>
            /// <param name="inputAsBytes"></param>
            /// <param name="n"></param>
            /// <returns></returns>
            public static UInt64 GetChecksum(byte[] inputAsBytes, int n)
            {
                //Fletcher 16: Read a single byte
                //Fletcher 32: Read a 16 bit block (two bytes)
                //Fletcher 64: Read a 32 bit block (four bytes)
                int bytesPerCycle = n / 16;

                //2^x gives max value that can be stored in x bits
                //no of bits here is 8 * bytesPerCycle (8 bits to a byte)
                UInt64 modValue = (UInt64)(Math.Pow(2, 8 * bytesPerCycle) - 1);

                UInt64 sum1 = 0;
                UInt64 sum2 = 0;
                foreach (UInt64 block in Blockify(inputAsBytes, bytesPerCycle))
                {
                    sum1 = (sum1 + block) % modValue;
                    sum2 = (sum2 + sum1) % modValue;
                }

                return sum1 + (sum2 * (modValue + 1));
            }

            public class RandomGenerator
            {
                // Instantiate random number generator.
                // It is better to keep a single Random instance
                // and keep using Next on the same instance.
                private readonly Random _random = new(Guid.NewGuid().GetHashCode());

                // Generates a random number within a range.
                public int RandomNumber(int min, int max)
                {
                    return _random.Next(min, max);
                }

                // Generates a random string with a given size.
                public string RandomString(int size, bool lowerCase = false)
                {
                    var builder = new StringBuilder(size);

                    // Unicode/ASCII Letters are divided into two blocks
                    // (Letters 65–90 / 97–122):
                    // The first group containing the uppercase letters and
                    // the second group containing the lowercase.

                    // char is a single Unicode character
                    char offset = lowerCase ? 'a' : 'A';
                    const int lettersOffset = 26; // A...Z or a..z: length = 26

                    for (var i = 0; i < size; i++)
                    {
                        var @char = (char)_random.Next(offset, offset + lettersOffset);
                        builder.Append(@char);
                    }

                    return lowerCase ? builder.ToString().ToLower() : builder.ToString();
                }

                // Generates a random password.
                // 4-LowerCase + 4-Digits + 2-UpperCase
                public string RandomPassword()
                {
                    var passwordBuilder = new StringBuilder();

                    // 4-Letters lower case
                    passwordBuilder.Append(RandomString(4, true));

                    // 4-Digits between 1000 and 9999
                    passwordBuilder.Append(RandomNumber(1000, 9999));

                    // 2-Letters upper case
                    passwordBuilder.Append(RandomString(2));
                    return passwordBuilder.ToString();
                }
            }


        }

    }

}
