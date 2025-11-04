using System;
using System.Security.Cryptography;
using System.Text;

namespace IoTPowerShellAgent.IoT
{



    public static class SasTokenGenerator
    {







        public static string GenerateSasToken(string resourceUri, string sharedAccessKey, TimeSpan duration)
        {

            long expiration = DateTimeOffset.UtcNow.Add(duration).ToUnixTimeSeconds();


            string stringToSign = $"{resourceUri}\n{expiration}";


            byte[] keyBytes;
            try
            {
                keyBytes = Convert.FromBase64String(sharedAccessKey);
            }
            catch (FormatException ex)
            {
                throw new ArgumentException("Failed to decode shared access key. It must be base64 encoded.", nameof(sharedAccessKey), ex);
            }


            using (var hmac = new HMACSHA256(keyBytes))
            {
                byte[] signatureBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign));
                string signature = Convert.ToBase64String(signatureBytes);



                string token = $"SharedAccessSignature sr={resourceUri}&sig={signature}&se={expiration}";
                return token;
            }
        }









        public static string GenerateDeviceSasToken(string hostName, string deviceId, string sharedAccessKey, TimeSpan? duration = null)
        {
            duration ??= TimeSpan.FromHours(1);
            string resourceUri = $"{hostName}/devices/{deviceId}";
            return GenerateSasToken(resourceUri, sharedAccessKey, duration.Value);
        }
    }
}

