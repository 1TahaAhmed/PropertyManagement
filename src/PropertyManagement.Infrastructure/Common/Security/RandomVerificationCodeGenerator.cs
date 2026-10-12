using PropertyManagement.Application.Abstractions.Security;
using System.Security.Cryptography;

namespace PropertyManagement.Infrastructure.Common.Security
{
    public sealed class RandomVerificationCodeGenerator
        : IVerificationCodeGenerator
    {
        public string Generate(int length)
        {
            if(length <= 0)
            {
                throw new ArgumentOutOfRangeException
                    (nameof(length), "Length must be greater than zero.");
            }

            var characters = new char[length];

            for(var index = 0; index < characters.Length; index++)
            {
                var digit = RandomNumberGenerator.GetInt32(
                    fromInclusive: 0,
                    toExclusive: 10
                    );

                characters[index] = (char)('0' + digit);
            }

            return new string(characters);
        }
    }
}
